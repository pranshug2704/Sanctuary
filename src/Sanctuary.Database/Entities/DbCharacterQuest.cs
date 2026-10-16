namespace Sanctuary.Database.Entities;

/// <summary>
/// A quest a character has accepted. Same shape as Raising Kaines' FreeRealms-Legacy
/// CharacterQuests table, so quest progress can move between the two servers.
/// </summary>
public sealed class DbCharacterQuest
{
    public int QuestId { get; set; }

    public ulong CharacterId { get; set; }
    public DbCharacter Character { get; set; } = null!;

    public bool Completed { get; set; }

    /// <summary>How many of the quest's goals are finished.</summary>
    public int GoalProgress { get; set; }

    /// <summary>Progress within the active goal, for goals that count towards a total.</summary>
    public int GoalCount { get; set; }
}
