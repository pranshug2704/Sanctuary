using System.Collections.Concurrent;

namespace Sanctuary.Game.Quests;

/// <summary>A player's progress on one accepted quest (persisted as DbCharacterQuest).</summary>
public sealed class PlayerQuest
{
    public int QuestId { get; init; }

    public bool Completed { get; set; }

    /// <summary>How many of the quest's goals are finished; the active goal's index.</summary>
    public int GoalProgress { get; set; }

    /// <summary>Progress within the active goal.</summary>
    public int GoalCount { get; set; }

    /// <summary>Quest objects already used for the active goal (not persisted; cleared when the goal changes). Read by
    /// zone threads deciding object visibility, hence concurrent..</summary>
    public ConcurrentDictionary<ulong, byte> UsedObjects { get; } = [];
}
