using Sanctuary.Game.Entities;

namespace Sanctuary.Game;

/// <summary>
/// Server-side quests from Resources/Quests.json: offers from quest givers, goal progress
/// (talk, reach, collect), turn-in and rewards. Drives the client quest packets (family 49).
/// </summary>
public interface IQuestManager
{
    /// <summary>Handles a click on an npc if it matters to a quest; false lets the npc's normal interaction run.</summary>
    bool TryInteract(Player player, Npc npc);

    void OnOfferReply(Player player, int questId, bool accepted);

    void OnTurnInReply(Player player, int questId);

    /// <summary>The player abandoned an unfinished quest from the journal; it can be offered again.</summary>
    void Abandon(Player player, int questId);

    void OnCollect(Player player, string nodeType, int itemDefinitionId);

    /// <summary>The player defeated an enemy npc (CombatManager).</summary>
    void OnDefeat(Player player, Npc npc);

    void OnPositionChanged(Player player);

    /// <summary>Sends the player's active quests to the journal (once per login).</summary>
    void SendJournal(Player player);

}
