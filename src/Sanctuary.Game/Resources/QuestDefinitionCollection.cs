using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;
using Sanctuary.Game.Resources.Definitions;

namespace Sanctuary.Game.Resources;

/// <summary>
/// Quests from two files next to each other:
/// QuestsGenerated.json, built from the client locale (invalid entries are skipped), and Quests.json, written by hand (strict; its quests replace generated
/// ones with the same QuestId or TitleId).
/// </summary>
public sealed class QuestDefinitionCollection : ObservableConcurrentDictionary<int, QuestDefinition>
{
    public const string GeneratedFileName = "QuestsGenerated.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ILogger _logger;

    /// <summary>Bumped on every successful (re)load, so spawned quest objects can be rebuilt.</summary>
    public int Version { get; private set; }

    public QuestDefinitionCollection(ILogger logger)
    {
        _logger = logger;
    }

    public bool Load(string filePath)
    {
        var generatedPath = Path.Combine(Path.GetDirectoryName(filePath) ?? string.Empty, GeneratedFileName);
        var loaded = new Dictionary<int, QuestDefinition>();

        try
        {
            if (File.Exists(generatedPath) && !TryRead(generatedPath, loaded, strict: false))
                return false;

            if (File.Exists(filePath))
            {
                if (!TryRead(filePath, loaded, strict: true))
                    return false;
            }
            else
            {
                // Quest content is optional: without it the server offers no quests.
                _logger.LogWarning("Failed to find file \"{file}\". No hand-written quests will be loaded.", filePath);
            }

            // Chains whose earlier quest is missing (or was skipped) can never be offered.
            bool removed;
            do
            {
                removed = false;

                foreach (var entry in loaded.Values.Where(x => x.PrerequisiteQuestId != 0 && !loaded.ContainsKey(x.PrerequisiteQuestId)).ToList())
                {
                    _logger.LogWarning("Quest {id} requires unknown quest {prerequisite}; skipped.", entry.QuestId, entry.PrerequisiteQuestId);
                    loaded.Remove(entry.QuestId);
                    removed = true;
                }
            } while (removed);

            Clear();

            foreach (var entry in loaded)
                TryAdd(entry.Key, entry.Value);

            Version++;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load quests from \"{file}\".", filePath);
            return false;
        }

        _logger.LogInformation("Loaded {count} quest definitions.", Count);

        return true;
    }

    private bool TryRead(string filePath, Dictionary<int, QuestDefinition> loaded, bool strict)
    {
        List<QuestDefinition>? entries;

        using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            entries = JsonSerializer.Deserialize<List<QuestDefinition>>(stream, JsonOptions);

        if (entries is null)
            return false;

        var seen = new HashSet<int>();
        var skipped = 0;

        foreach (var entry in entries)
        {
            var error = Validate(entry);

            if (error is null && !seen.Add(entry.QuestId))
                error = "duplicate QuestId";

            if (error is not null)
            {
                if (strict)
                {
                    _logger.LogError("Quest {id} in \"{file}\": {error}.", entry.QuestId, filePath, error);
                    return false;
                }

                _logger.LogWarning("Quest {id} in \"{file}\" skipped: {error}.", entry.QuestId, filePath, error);
                skipped++;
                continue;
            }

            if (strict)
            {
                // A hand-written quest replaces the generated version of the same quest.
                foreach (var generated in loaded.Values.Where(x => x.TitleId == entry.TitleId && x.QuestId != entry.QuestId).ToList())
                    loaded.Remove(generated.QuestId);
            }

            loaded[entry.QuestId] = entry;
        }

        if (skipped > 0)
            _logger.LogWarning("Skipped {count} invalid quest(s) in \"{file}\".", skipped, filePath);

        return true;
    }

    private static string? Validate(QuestDefinition entry)
    {
        if (entry.QuestId <= 0 || entry.GiverGuid == 0 || entry.Goals.Count == 0)
            return "needs QuestId, GiverGuid and Goals";

        foreach (var goal in entry.Goals)
        {
            goal.RequiredCount = Math.Max(1, goal.RequiredCount);

            var valid = goal.Type switch
            {
                QuestGoalType.TalkToNpc => goal.TargetGuid != 0,
                QuestGoalType.ReachLocation => goal.ReachPosition.Length == 3 && goal.ReachRadius > 0,
                QuestGoalType.Collect => true,
                QuestGoalType.Defeat => goal.DefeatNames.Count > 0,
                QuestGoalType.Interact => goal.Objects is { ModelId: > 0 } objects &&
                    objects.Positions.Count >= goal.RequiredCount &&
                    objects.Positions.All(x => x.Length is 3 or 4),
                _ => false
            };

            if (!valid)
                return $"invalid {goal.Type} goal";
        }

        return null;
    }
}
