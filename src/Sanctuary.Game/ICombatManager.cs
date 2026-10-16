using Sanctuary.Game.Entities;

namespace Sanctuary.Game;

/// <summary>
/// Overworld combat: enemies from Resources/Enemies.json, the job combat bar
/// (AbilityPacketClientRequestStartAbility on action bar 1) and enemies fighting back.
/// </summary>
public interface ICombatManager
{
    /// <summary>Combat bar action bar id sent by the client (slot 0 = basic, 1 = special).</summary>
    const int CombatActionBarId = 1;

    /// <summary>Handles a combat bar ability; false if the player has no combat ability in that slot.</summary>
    bool TryUseAbility(Player player, int actionBarId, int slot);

    /// <summary>The player clicked (or pressed the interact key on) an npc: attacks it if it's an enemy.</summary>
    bool TryAttackTarget(Player player, Entities.Npc npc);

    /// <summary>The player entered the world: overworld combat on (needed for floating hit numbers).</summary>
    void OnPlayerReady(Player player);
}
