using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Quests;
using Sanctuary.Game.Resources.Definitions;
using Sanctuary.Game.Resources.Definitions.Rewards;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game;

/// <summary>
/// Quests are offered by their giver npc (QuestInfoPacket); accepting (QuestReplyPacket) adds
/// them to the journal (QuestAddPacket). Goals are done in order and shown as objectives:
/// done = status 2, active = status 1, later goals = status 0. When every goal is done the
/// turn-in npc opens the reward window (QuestEndPacket); closing it (QuestEndReplyPacket)
/// grants the rewards and completes the quest. The client keeps the quest giver close-up after
/// Accept and turn-in, so both are followed by CommandPacketQuestDialogDone.
/// Interact goals spawn <see cref="QuestObject"/>s in the world zone, shown only to players on
/// that goal; each one counts once and disappears for the player who used it.
/// </summary>
public class QuestManager : IQuestManager
{
    /// <summary>Chance (0-1) that turning in a quest also grants gear for the active job (JobGearHelper).</summary>
    private const double QuestGearChance = 0.25;

    /// <summary>Quests live in the open world (FabledRealms), not in houses or combat instances.</summary>
    private const int QuestZoneDefinitionId = 1;

    private const int ObjectInteractRange = 12;
    private const byte ObjectCursorId = 18;

    private readonly ILogger _logger;
    private readonly IResourceManager _resourceManager;
    private readonly IRewardManager _rewardManager;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;

    private readonly ConcurrentDictionary<IZone, SpawnedQuestObjects> _zoneObjects = new();
    private readonly object _objectSpawnLock = new();

    private sealed record SpawnedQuestObjects(int Version, List<QuestObject> Objects);

    public QuestManager(ILogger<QuestManager> logger, IResourceManager resourceManager,
        IRewardManager rewardManager, IDbContextFactory<DatabaseContext> dbContextFactory)
    {
        _logger = logger;
        _resourceManager = resourceManager;
        _rewardManager = rewardManager;
        _dbContextFactory = dbContextFactory;

        Player.NpcsBecameVisible += (player, npcs) => RefreshMarkers(player, npcs);
    }

    #region Npc interaction

    public bool TryInteract(Player player, Npc npc)
    {
        if (npc is QuestObject questObject)
        {
            UseObject(player, questObject);
            return true;
        }

        lock (player.Quests)
        {
            // 1. A talk goal this npc completes.
            foreach (var (quest, definition) in ActiveQuests(player))
            {
                var goal = CurrentGoal(quest, definition);
                if (goal is null || goal.Type != QuestGoalType.TalkToNpc || goal.TargetGuid != npc.Guid)
                    continue;

                Advance(player, quest, definition, 1);

                if (goal.DialogueId != 0)
                    npc.SayLocalized(goal.DialogueId);

                if (IsReadyToTurnIn(quest, definition) && definition.EffectiveTurnInGuid == npc.Guid)
                    OpenTurnIn(player, definition, npc);

                return true;
            }

            // 2. A finished quest handed in here.
            foreach (var (quest, definition) in ActiveQuests(player))
            {
                if (IsReadyToTurnIn(quest, definition) && definition.EffectiveTurnInGuid == npc.Guid)
                {
                    OpenTurnIn(player, definition, npc);
                    return true;
                }
            }

            // 3. A reminder from the giver or turn-in npc of an unfinished quest.
            foreach (var (_, definition) in ActiveQuests(player))
            {
                if (definition.ProgressDialogueId != 0 &&
                    (definition.GiverGuid == npc.Guid || definition.EffectiveTurnInGuid == npc.Guid))
                {
                    npc.SayLocalized(definition.ProgressDialogueId);
                    return true;
                }
            }

            // 4. A quest this npc can offer.
            var offer = _resourceManager.Quests.Values
                .Where(x => x.GiverGuid == npc.Guid && IsOfferable(player, x))
                .OrderBy(x => x.QuestId)
                .FirstOrDefault();

            if (offer is null)
                return false;

            player.PendingQuestOfferId = offer.QuestId;

            var packet = new QuestInfoPacket
            {
                QuestId = offer.QuestId,
                IntroTextId = offer.GiverDialogueId,
                NameId = offer.TitleId,
                DescriptionId = offer.DescriptionId,
                MembersOnly = offer.MembersOnly,
                QuestGiverGuid = npc.Guid
            };
            FillReward(packet.Reward, offer);

            player.SendTunneled(packet);
            return true;
        }
    }

    private static bool IsOfferable(Player player, QuestDefinition definition)
    {
        if (player.Quests.ContainsKey(definition.QuestId))
            return false;

        if (definition.PrerequisiteQuestId == 0)
            return true;

        return player.Quests.TryGetValue(definition.PrerequisiteQuestId, out var prerequisite) && prerequisite.Completed;
    }

    #endregion

    #region Replies

    public void OnOfferReply(Player player, int questId, bool accepted)
    {
        lock (player.Quests)
        {
            if (player.PendingQuestOfferId != questId)
            {
                _logger.LogWarning("{player} answered quest {questId} without an open offer.", player.Guid, questId);
                return;
            }

            player.PendingQuestOfferId = 0;

            if (!accepted)
                return;

            player.SendTunneled(new CommandPacketQuestDialogDone());

            if (!_resourceManager.Quests.TryGetValue(questId, out var definition) || !IsOfferable(player, definition))
                return;

            var quest = new PlayerQuest { QuestId = questId };

            if (!Save(player, quest))
                return;

            player.Quests[questId] = quest;
            player.SendTunneled(CreateQuestAdd(quest, definition));
            RefreshMarkers(player);
            RefreshQuestObjects(player);

            _logger.LogInformation("{player} accepted quest {questId}.", player.Guid, questId);

            // A reach goal may already be satisfied where the player stands.
            CheckReachGoal(player, quest, definition);
        }
    }

    public void OnTurnInReply(Player player, int questId)
    {
        lock (player.Quests)
        {
            if (player.PendingQuestTurnInId != questId)
            {
                _logger.LogWarning("{player} turned in quest {questId} without an open turn-in.", player.Guid, questId);
                return;
            }

            player.PendingQuestTurnInId = 0;
            player.SendTunneled(new CommandPacketQuestDialogDone());

            if (!player.Quests.TryGetValue(questId, out var quest) ||
                !_resourceManager.Quests.TryGetValue(questId, out var definition) ||
                !IsReadyToTurnIn(quest, definition))
                return;

            quest.Completed = true;

            if (!Save(player, quest))
            {
                quest.Completed = false;
                return;
            }

            if (definition.RewardCoins > 0)
                _rewardManager.TryGrantCurrency(player, CurrencyType.Coins, definition.RewardCoins);

            foreach (var item in definition.RewardItems)
                _rewardManager.TryGrantItem(player, item.ItemDefinitionId, 0, item.Quantity, definition.EffectiveTurnInGuid);

            if (definition.RewardExperience > 0)
                _rewardManager.TryGrantExperience(player, definition.RewardExperience);

            // A bonus piece of gear for the player's job now and then, so quests feed the level system.
            if (Random.Shared.NextDouble() < QuestGearChance)
                _rewardManager.TryGrantJobGear(player, definition.EffectiveTurnInGuid);

            player.SendTunneled(new QuestCompletePacket { QuestId = questId });
            RefreshMarkers(player);

            _logger.LogInformation("{player} completed quest {questId}.", player.Guid, questId);
        }
    }

    public void Abandon(Player player, int questId)
    {
        lock (player.Quests)
        {
            if (!player.Quests.TryGetValue(questId, out var quest) || quest.Completed)
            {
                _logger.LogWarning("{player} abandoned quest {questId}, which is not active.", player.Guid, questId);
                return;
            }

            if (!Delete(player, questId))
                return;

            player.Quests.TryRemove(questId, out _);
            player.SendTunneled(new QuestAbandonedPacket { QuestId = questId });
            RefreshMarkers(player);
            RefreshQuestObjects(player);

            _logger.LogInformation("{player} abandoned quest {questId}.", player.Guid, questId);
        }
    }

    #endregion

    #region Goal events

    public void OnCollect(Player player, string nodeType, int itemDefinitionId)
    {
        lock (player.Quests)
        {
            foreach (var (quest, definition) in ActiveQuests(player).ToList())
            {
                var goal = CurrentGoal(quest, definition);
                if (goal is null || goal.Type != QuestGoalType.Collect)
                    continue;

                if (goal.CollectNodeType.Length > 0 && !string.Equals(goal.CollectNodeType, nodeType, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (goal.CollectItemDefinitionId != 0 && goal.CollectItemDefinitionId != itemDefinitionId)
                    continue;

                Advance(player, quest, definition, 1);
            }
        }
    }

    public void OnDefeat(Player player, Npc npc)
    {
        lock (player.Quests)
        {
            foreach (var (quest, definition) in ActiveQuests(player).ToList())
            {
                var goal = CurrentGoal(quest, definition);
                if (goal is not { Type: QuestGoalType.Defeat } ||
                    !goal.DefeatNames.Any(x => string.Equals(x, npc.Name, StringComparison.OrdinalIgnoreCase)))
                    continue;

                Advance(player, quest, definition, 1);
            }
        }
    }

    public void OnPositionChanged(Player player)
    {
        EnsureQuestObjects(player);

        if (player.Quests.IsEmpty)
            return;

        lock (player.Quests)
        {
            foreach (var (quest, definition) in ActiveQuests(player).ToList())
                CheckReachGoal(player, quest, definition);
        }
    }

    private void CheckReachGoal(Player player, PlayerQuest quest, QuestDefinition definition)
    {
        var goal = CurrentGoal(quest, definition);
        if (goal is null || goal.Type != QuestGoalType.ReachLocation)
            return;

        var target = new Vector3(goal.ReachPosition[0], goal.ReachPosition[1], goal.ReachPosition[2]);
        var position = new Vector3(player.Position.X, player.Position.Y, player.Position.Z);

        if (Vector3.DistanceSquared(target, position) <= goal.ReachRadius * goal.ReachRadius)
            Advance(player, quest, definition, goal.RequiredCount);
    }

    #endregion

    #region Progress

    private void Advance(Player player, PlayerQuest quest, QuestDefinition definition, int amount)
    {
        var goal = CurrentGoal(quest, definition);
        if (goal is null)
            return;

        var objectiveId = quest.GoalProgress + 1;
        quest.GoalCount = Math.Min(goal.RequiredCount, quest.GoalCount + amount);

        if (quest.GoalCount < goal.RequiredCount)
        {
            player.SendTunneled(new QuestObjectiveUpdatePacket
            {
                QuestId = quest.QuestId,
                ObjectiveId = objectiveId,
                Count = quest.GoalCount,
                CompletedPercentage = Percentage(quest, definition)
            });
        }
        else
        {
            quest.GoalProgress++;
            quest.GoalCount = 0;
            quest.UsedObjects.Clear();

            player.SendTunneled(new QuestObjectiveCompletePacket
            {
                QuestId = quest.QuestId,
                ObjectiveId = objectiveId,
                CompletedPercentage = Percentage(quest, definition)
            });

            var next = CurrentGoal(quest, definition);
            if (next is not null)
            {
                player.SendTunneled(new QuestObjectiveActivatedPacket
                {
                    QuestId = quest.QuestId,
                    ObjectiveId = quest.GoalProgress + 1,
                    Total = next.RequiredCount
                });
            }

            RefreshMarkers(player);
            RefreshQuestObjects(player);
        }

        Save(player, quest);
    }

    private void OpenTurnIn(Player player, QuestDefinition definition, Npc npc)
    {
        player.PendingQuestTurnInId = definition.QuestId;

        var packet = new QuestEndPacket
        {
            QuestGiverGuid = npc.Guid,
            QuestId = definition.QuestId,
            EndTextId = definition.TurnInDialogueId
        };
        FillReward(packet.Reward, definition);

        player.SendTunneled(packet);
    }

    private static QuestGoal? CurrentGoal(PlayerQuest quest, QuestDefinition definition)
    {
        return quest.GoalProgress < definition.Goals.Count ? definition.Goals[quest.GoalProgress] : null;
    }

    private static bool IsReadyToTurnIn(PlayerQuest quest, QuestDefinition definition)
    {
        return !quest.Completed && quest.GoalProgress >= definition.Goals.Count;
    }

    private static float Percentage(PlayerQuest quest, QuestDefinition definition)
    {
        var goal = CurrentGoal(quest, definition);
        var partial = goal is null ? 0f : (float)quest.GoalCount / goal.RequiredCount;

        return Math.Min(1f, (quest.GoalProgress + partial) / definition.Goals.Count);
    }

    private IEnumerable<(PlayerQuest Quest, QuestDefinition Definition)> ActiveQuests(Player player)
    {
        foreach (var quest in player.Quests.Values.OrderBy(x => x.QuestId))
        {
            if (quest.Completed || !_resourceManager.Quests.TryGetValue(quest.QuestId, out var definition))
                continue;

            yield return (quest, definition);
        }
    }

    #endregion

    #region Quest objects

    private void UseObject(Player player, QuestObject questObject)
    {
        lock (player.Quests)
        {
            if (!player.Quests.TryGetValue(questObject.QuestId, out var quest) || quest.Completed ||
                quest.GoalProgress != questObject.GoalIndex ||
                !_resourceManager.Quests.TryGetValue(questObject.QuestId, out var definition))
                return;

            var goal = CurrentGoal(quest, definition);
            if (goal is not { Type: QuestGoalType.Interact })
                return;

            var position = new Vector3(player.Position.X, player.Position.Y, player.Position.Z);
            var objectPosition = new Vector3(questObject.Position.X, questObject.Position.Y, questObject.Position.Z);
            var range = questObject.InteractRange + 3f;

            if (Vector3.DistanceSquared(position, objectPosition) > range * range ||
                !quest.UsedObjects.TryAdd(questObject.Guid, 0))
                return;

            // Spoken before the object fades, so the bubble has something to sit on.
            if (goal.DialogueId != 0)
                player.SendTunneled(new ChatPacketFromStringId { SpeakerGuid = questObject.Guid, StringId = goal.DialogueId });

            player.OnRemoveVisibleNpcGracefully(questObject, animate: true, delay: goal.DialogueId != 0 ? 1500 : 0,
                effectDelay: 0, compositeEffectId: 0, duration: 1000);
            questObject.OnRemoveVisiblePlayers(player);

            Advance(player, quest, definition, 1);
        }
    }

    private bool IsObjectVisibleTo(Player player, QuestObject questObject)
    {
        return player.Quests.TryGetValue(questObject.QuestId, out var quest) && !quest.Completed &&
            quest.GoalProgress == questObject.GoalIndex && !quest.UsedObjects.ContainsKey(questObject.Guid);
    }

    /// <summary>Spawns the Interact goals' objects in the player's zone, once per Quests.json load.</summary>
    private void EnsureQuestObjects(Player player)
    {
        var zone = player.Zone;
        if (zone is null || zone.DefinitionId != QuestZoneDefinitionId || zone.OwnerId is not null)
            return;

        var version = _resourceManager.Quests.Version;
        if (_zoneObjects.TryGetValue(zone, out var spawned) && spawned.Version == version)
            return;

        lock (_objectSpawnLock)
        {
            if (_zoneObjects.TryGetValue(zone, out spawned))
            {
                if (spawned.Version == version)
                    return;

                // Quests.json was reloaded: rebuild the objects from the new definitions.
                foreach (var old in spawned.Objects)
                    old.Dispose();
            }

            var objects = new List<QuestObject>();

            foreach (var definition in _resourceManager.Quests.Values)
            {
                for (var goalIndex = 0; goalIndex < definition.Goals.Count; goalIndex++)
                {
                    var goal = definition.Goals[goalIndex];
                    if (goal.Type != QuestGoalType.Interact || goal.Objects is null)
                        continue;

                    foreach (var spot in goal.Objects.Positions)
                    {
                        var questObject = new QuestObject(zone, definition.QuestId, goalIndex, IsObjectVisibleTo)
                        {
                            Guid = zone.AllocateNpcGuid(),
                            NameId = goal.Objects.NameId,
                            ModelId = goal.Objects.ModelId,
                            Scale = goal.Objects.Scale,
                            CompositeEffectId = goal.Objects.CompositeEffectId,
                            HideNamePlate = goal.Objects.NameId == 0,
                            InteractRange = ObjectInteractRange,
                            CursorId = ObjectCursorId,
                            HasCursor = true,
                            IsInteractable = true,
                            Visible = true
                        };

                        if (!zone.TryAddNpc(questObject))
                            continue;

                        var heading = spot.Length > 3 ? spot[3] : 0f;
                        questObject.UpdatePosition(new Vector4(spot[0], spot[1], spot[2], 1f),
                            new Quaternion(MathF.Sin(heading), 0f, MathF.Cos(heading), 0f));

                        objects.Add(questObject);
                    }
                }
            }

            _zoneObjects[zone] = new SpawnedQuestObjects(version, objects);
            _logger.LogInformation("Spawned {count} quest object(s) in zone {zone}.", objects.Count, zone.DefinitionId);
        }

        // Players already standing next to the new objects only see them after a refresh.
        foreach (var other in zone.Players)
            RefreshQuestObjects(other);
    }

    /// <summary>
    /// Shows or hides nearby quest objects after the player's goals change. Zone tiles only
    /// re-check visibility when the player crosses a tile, so this does it for the current one.
    /// </summary>
    private void RefreshQuestObjects(Player player)
    {
        if (player.Zone is null || !_zoneObjects.TryGetValue(player.Zone, out var spawned))
            return;

        var visibleTiles = player.ZoneTile.VisibleTiles;

        foreach (var questObject in spawned.Objects)
        {
            var shown = player.VisibleNpcs.ContainsKey(questObject.Guid);
            var show = questObject.IsVisibleTo(player) && visibleTiles.Contains(questObject.ZoneTile);

            if (show && !shown)
            {
                player.OnAddVisibleNpcs(questObject);
                questObject.OnAddVisiblePlayers(player);
            }
            else if (!show && shown)
            {
                player.OnRemoveVisibleNpcs(questObject);
                questObject.OnRemoveVisiblePlayers(player);
            }
        }
    }

    #endregion

    #region Markers

    // NotificationImages.txt ids, drawn over the npc by PlayerUpdatePacketAddNotifications
    //.
    private const int AvailableMarkerImage = 2;    // "!" icon_UI_objective_task_64
    private const int TalkMarkerImage = 6;         // "?" icon_UI_objective_quest_64
    private const int MarkerNotificationType = 1;

    /// <summary>Shows "!" over givers with an offerable quest and "?" over the npc to talk to or hand in to.</summary>
    private void RefreshMarkers(Player player, IEnumerable<Npc>? npcs = null)
    {
        if (_resourceManager.Quests.Count == 0)
            return;

        var add = new PlayerUpdatePacketAddNotifications();
        var remove = new PlayerUpdatePacketRemoveNotifications();

        lock (player.Quests)
        {
            foreach (var npc in npcs ?? player.VisibleNpcs.Values)
            {
                var image = MarkerFor(player, npc.Guid);
                player.QuestMarkers.TryGetValue(npc.Guid, out var shown);

                if (image == shown)
                    continue;

                if (shown != 0)
                {
                    remove.Notifications.Add(new RemoveNotificationInfo { Guid = npc.Guid, ThoughtBubbleIconId = shown });
                    player.QuestMarkers.TryRemove(npc.Guid, out _);
                }

                if (image != 0)
                {
                    player.QuestMarkers[npc.Guid] = image;

                    add.Notifications.Add(new NotificationInfo
                    {
                        Guid = npc.Guid,
                        NotificationType = MarkerNotificationType,
                        IconId = image,
                        IconState = image,
                        NameId = npc.NameId
                    });
                }
            }
        }

        if (remove.Notifications.Count > 0)
            player.SendTunneled(remove);

        if (add.Notifications.Count > 0)
            player.SendTunneled(add);
    }

    private int MarkerFor(Player player, ulong npcGuid)
    {
        foreach (var (quest, definition) in ActiveQuests(player))
        {
            var goal = CurrentGoal(quest, definition);

            if (goal is { Type: QuestGoalType.TalkToNpc } && goal.TargetGuid == npcGuid)
                return TalkMarkerImage;

            if (goal is null && definition.EffectiveTurnInGuid == npcGuid)
                return TalkMarkerImage;
        }

        foreach (var definition in _resourceManager.Quests.Values)
        {
            if (definition.GiverGuid == npcGuid && IsOfferable(player, definition))
                return AvailableMarkerImage;
        }

        return 0;
    }

    #endregion

    #region Packets

    public void SendJournal(Player player)
    {
        if (player.QuestJournalSent)
            return;

        player.QuestJournalSent = true;

        EnsureQuestObjects(player);

        lock (player.Quests)
        {
            // Quiet: the client decides the "Quest Started" toast from QuestData.Quiet alone (it
            // still toasts during the loading screen, tested), so restored quests are sent quiet.
            // The client then also mutes these quests' objective popups for the session; their
            // counters still update.
            foreach (var (quest, definition) in ActiveQuests(player))
                player.SendTunneled(CreateQuestAdd(quest, definition, quiet: true));

            RefreshQuestObjects(player);
        }
    }

    private QuestAddPacket CreateQuestAdd(PlayerQuest quest, QuestDefinition definition, bool quiet = false)
    {
        var packet = new QuestAddPacket { Unknown = true };
        var data = packet.Quest;

        data.QuestId = definition.QuestId;
        data.NameId = definition.TitleId;
        data.DescriptionId = definition.DescriptionId;
        data.HelperTextId = definition.HelperTextId;
        data.MembersOnly = definition.MembersOnly;
        data.Quiet = quiet;
        data.CompletedPercentage = Percentage(quest, definition);
        data.IconId = definition.IconId;
        FillReward(data.Reward, definition);

        for (var i = 0; i < definition.Goals.Count; i++)
        {
            var goal = definition.Goals[i];
            var done = i < quest.GoalProgress;
            var active = i == quest.GoalProgress;

            data.Objectives.Add(new QuestObjectiveData
            {
                ObjectiveId = i + 1,
                NameId = goal.NameId,
                DescriptionId = goal.DescriptionId,
                Status = done ? 2 : active ? 1 : 0,
                Count = done ? goal.RequiredCount : active ? quest.GoalCount : 0,
                Total = goal.RequiredCount
            });
        }

        return packet;
    }

    private void FillReward(QuestRewardData reward, QuestDefinition definition)
    {
        var bundle = reward.Bundle;
        bundle.Coins = definition.RewardCoins;
        bundle.Experience = definition.RewardExperience;

        foreach (var item in definition.RewardItems)
        {
            if (!_resourceManager.ClientItemDefinitions.TryGetValue(item.ItemDefinitionId, out var itemDefinition))
            {
                _logger.LogWarning("Quest {questId} rewards unknown item {itemDefinitionId}.", definition.QuestId, item.ItemDefinitionId);
                continue;
            }

            bundle.Entries.Add(new RewardBundleEntryItem
            {
                IconId = itemDefinition.Icon.Id,
                TintId = itemDefinition.Icon.TintId,
                NameId = itemDefinition.NameId,
                DefinitionId = item.ItemDefinitionId,
                Quantity = item.Quantity
            });
        }
    }

    #endregion

    #region Persistence

    private bool Delete(Player player, int questId)
    {
        var characterId = GuidHelper.GetPlayerId(player.Guid);

        try
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            dbContext.CharacterQuests.Where(x => x.CharacterId == characterId && x.QuestId == questId).ExecuteDelete();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete quest {questId} for character {characterId}.", questId, characterId);
            return false;
        }
    }

    private bool Save(Player player, PlayerQuest quest)
    {
        var characterId = GuidHelper.GetPlayerId(player.Guid);

        try
        {
            using var dbContext = _dbContextFactory.CreateDbContext();

            var dbQuest = dbContext.CharacterQuests.SingleOrDefault(x => x.CharacterId == characterId && x.QuestId == quest.QuestId);

            if (dbQuest is null)
            {
                dbQuest = new DbCharacterQuest { CharacterId = characterId, QuestId = quest.QuestId };
                dbContext.CharacterQuests.Add(dbQuest);
            }

            dbQuest.Completed = quest.Completed;
            dbQuest.GoalProgress = quest.GoalProgress;
            dbQuest.GoalCount = quest.GoalCount;

            dbContext.SaveChanges();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save quest {questId} for character {characterId}.", quest.QuestId, characterId);
            return false;
        }
    }

    #endregion
}
