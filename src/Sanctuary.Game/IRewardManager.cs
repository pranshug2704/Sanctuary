using Sanctuary.Game.Entities;
using Sanctuary.Game.Resources.Definitions.Rewards;

namespace Sanctuary.Game;

public interface IRewardManager
{
    bool TryRollReward(string rewardTableKey, out RewardDropDefinition? drop);

    bool TryGrantReward(Player player, RewardDropDefinition drop, ulong sourceGuid = 0);

    bool TryGrantItem(Player player, int itemDefinitionId, int tint, int quantity = 1, ulong sourceGuid = 0);

    bool TryGrantCurrency(Player player, CurrencyType currencyType, int amount);

    /// <summary>Adds experience to the active job, levelling it up (with the client fanfare) as needed.</summary>
    bool TryGrantExperience(Player player, int amount);

    /// <summary>A random piece of combat gear for the active job near its level (JobGearHelper); false if it has none.</summary>
    bool TryGrantJobGear(Player player, ulong sourceGuid = 0);

    /// <summary>Sets a job's level and experience directly (dev command / admin).</summary>
    bool TrySetJobLevel(Player player, int profileId, int level, int experience = 0);
}
