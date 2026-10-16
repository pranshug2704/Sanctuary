using System.Collections.Generic;

namespace Sanctuary.Game.Resources.Definitions;

/// <summary>
/// A quest from Resources/Quests.json. Text fields are string ids from the client's locale.
/// Field names follow Raising Kaines' FreeRealms-Legacy quest model where they overlap.
/// </summary>
public sealed class QuestDefinition
{
    public int QuestId { get; set; }

    /// <summary>Quest name: journal, tracker, offer and "Quest Started" toast.</summary>
    public int TitleId { get; set; }

    /// <summary>Journal description and the offer's description line.</summary>
    public int DescriptionId { get; set; }

    /// <summary>Text in the on-screen quest tracker.</summary>
    public int HelperTextId { get; set; }

    /// <summary>What the quest giver says in the offer bubble.</summary>
    public int GiverDialogueId { get; set; }

    /// <summary>What the turn-in npc says on the reward window.</summary>
    public int TurnInDialogueId { get; set; }

    /// <summary>Said by the giver or turn-in npc when clicked while the quest is unfinished; 0 = nothing.</summary>
    public int ProgressDialogueId { get; set; }

    public int IconId { get; set; }

    public bool MembersOnly { get; set; }

    /// <summary>The npc that offers the quest.</summary>
    public ulong GiverGuid { get; set; }

    /// <summary>The npc the quest is handed in to; 0 = the giver.</summary>
    public ulong TurnInGuid { get; set; }

    /// <summary>A quest that must be completed first; 0 = none.</summary>
    public int PrerequisiteQuestId { get; set; }

    /// <summary>Done in order; the journal lists them as objectives.</summary>
    public List<QuestGoal> Goals { get; set; } = [];

    public int RewardCoins { get; set; }
    public int RewardExperience { get; set; }
    public List<QuestRewardItem> RewardItems { get; set; } = [];

    public ulong EffectiveTurnInGuid => TurnInGuid != 0 ? TurnInGuid : GiverGuid;
}

public enum QuestGoalType
{
    TalkToNpc = 0,
    ReachLocation = 1,
    Collect = 2,
    Interact = 3,
    Defeat = 4
}

public sealed class QuestGoal
{
    /// <summary>Objective line in the journal and tracker.</summary>
    public int NameId { get; set; }

    /// <summary>Smaller line under the objective in the journal.</summary>
    public int DescriptionId { get; set; }

    /// <summary>Spoken by the target npc when a talk goal is completed, or by an Interact object when used; 0 = nothing.</summary>
    public int DialogueId { get; set; }

    public QuestGoalType Type { get; set; } = QuestGoalType.TalkToNpc;

    /// <summary>TalkToNpc: the npc to talk to.</summary>
    public ulong TargetGuid { get; set; }

    /// <summary>How many times the goal must happen (collect N things); shown as count/total.</summary>
    public int RequiredCount { get; set; } = 1;

    /// <summary>Collect: collection node type key (e.g. "sparkling-water"), or empty for any.</summary>
    public string CollectNodeType { get; set; } = string.Empty;

    /// <summary>Collect: item definition id that counts, or 0 for any item from a matching node.</summary>
    public int CollectItemDefinitionId { get; set; }

    /// <summary>ReachLocation: x, y, z.</summary>
    public float[] ReachPosition { get; set; } = [];

    /// <summary>ReachLocation: distance from <see cref="ReachPosition"/> that counts as arrived.</summary>
    public float ReachRadius { get; set; } = 10f;

    /// <summary>Defeat: npc names (Npcs.json Name, enemies from Enemies.json) that count when defeated.</summary>
    public List<string> DefeatNames { get; set; } = [];

    /// <summary>Interact: the objects spawned for this goal; each counts once per player.</summary>
    public QuestObjectDefinition? Objects { get; set; }
}

public sealed class QuestObjectDefinition
{
    /// <summary>Name plate text; 0 = no name.</summary>
    public int NameId { get; set; }

    /// <summary>Models.txt id.</summary>
    public int ModelId { get; set; }

    public float Scale { get; set; } = 1f;

    /// <summary>Effect played on the object, e.g. 5742 (node sparkle); 0 = none.</summary>
    public int CompositeEffectId { get; set; }

    /// <summary>x, y, z (and optional heading) of each object.</summary>
    public List<float[]> Positions { get; set; } = [];
}

public sealed class QuestRewardItem
{
    public int ItemDefinitionId { get; set; }
    public int Quantity { get; set; } = 1;
}
