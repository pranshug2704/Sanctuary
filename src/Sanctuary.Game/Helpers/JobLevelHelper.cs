using System;

namespace Sanctuary.Game.Helpers;

/// <summary>
/// Job (profile) levels: rank 1-20 plus experience into the current rank.
/// </summary>
/// <remarks>
/// The client has no experience table (it only shows rank and a 0-100 bar, Profile_DS
/// Rank/RankPercent), so the curve is ours: rank L needs 25*L + 50 experience to reach
/// L+1, about 5,700 in total from 1 to 20. Quests reward 25-50, enemies ~10.
/// </remarks>
public static class JobLevelHelper
{
    public const int MaxLevel = 20;

    public static int ClampLevel(int level) => Math.Clamp(level, 1, MaxLevel);

    public static int ExperienceToNextLevel(int level) => 25 * level + 50;

    /// <summary>Combat hit points at a level: 1075 at 1, 2500 at 20.</summary>
    public static int MaxHitpoints(int level) => 1000 + 75 * ClampLevel(level);

    /// <summary>Scales ability damage (CombatAbilities.json values are for level 20): x0.525 at 1, x1 at 20.</summary>
    public static float DamageMultiplier(int level) => 0.5f + ClampLevel(level) / 40f;

    /// <summary>RankPercent for the client's level bar (full at max level).</summary>
    public static int GetPercent(int level, int experience)
    {
        if (level >= MaxLevel)
            return 100;

        return Math.Clamp(experience * 100 / ExperienceToNextLevel(level), 0, 99);
    }

    /// <summary>Adds experience, levelling up as many times as it covers.</summary>
    public static (int Level, int Experience) Add(int level, int experience, int amount)
    {
        level = ClampLevel(level);
        experience = Math.Max(0, experience) + amount;

        while (level < MaxLevel && experience >= ExperienceToNextLevel(level))
        {
            experience -= ExperienceToNextLevel(level);
            level++;
        }

        return level >= MaxLevel ? (MaxLevel, 0) : (level, experience);
    }
}
