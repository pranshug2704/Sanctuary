using System;

using Sanctuary.Game.Zones;

namespace Sanctuary.Game.Entities;

/// <summary>
/// A clickable object spawned for an Interact quest goal (a cat bowl, a sparkling flower, ...).
/// It is only shown to players working on that goal who haven't used it yet.
/// </summary>
public sealed class QuestObject : Npc
{
    private readonly Func<Player, QuestObject, bool> _isVisibleTo;

    public int QuestId { get; }

    /// <summary>Index of the goal in the quest definition.</summary>
    public int GoalIndex { get; }

    public QuestObject(IZone zone, int questId, int goalIndex, Func<Player, QuestObject, bool> isVisibleTo)
        : base(zone)
    {
        QuestId = questId;
        GoalIndex = goalIndex;
        _isVisibleTo = isVisibleTo;
    }

    public override bool IsVisibleTo(Player player) => _isVisibleTo(player, this);
}
