using System.Collections.Generic;

namespace Sanctuary.Game.Resources.Definitions.Combat;

/// <summary>
/// Overworld enemies (Resources/Enemies.json): npcs whose name is listed spawn hostile
/// (AddNpc Disposition 0) with a hit point bar, and fight back when attacked.
/// </summary>
public sealed class EnemyDefinition
{
    public string Comment { get; set; } = string.Empty;

    /// <summary>Npc names (Npcs.json Name) this entry applies to.</summary>
    public List<string> Names { get; set; } = [];

    public int MaxHitpoints { get; set; } = 1000;

    /// <summary>Damage per hit on a player.</summary>
    public int Damage { get; set; } = 100;

    public int AttackIntervalMs { get; set; } = 2000;

    /// <summary>How close the player must be for the enemy to hit them (ranged models: at least 18).</summary>
    public float AttackRange { get; set; } = 8f;

    /// <summary>Attacks players who come this close; 0 = only fights back when attacked.</summary>
    public float AggroRange { get; set; }

    /// <summary>Gives up (walks home and heals) when its target is this far from its spawn point.</summary>
    public float LeashRange { get; set; } = 45f;

    /// <summary>
    /// AnimationTypes.xml slot played when it attacks (1001 = com_h2h_attack_01); 0 = the model's
    /// own attacks from EnemyAnimations.json, or 1001 if it has none listed.
    /// </summary>
    public int AttackAnimationId { get; set; }

    /// <summary>Runs after its target (within LeashRange) instead of only hitting from where it stands.</summary>
    public bool Chases { get; set; } = true;

    /// <summary>Movement speed (players run at ~6-7).</summary>
    public float MoveSpeed { get; set; } = 5f;

    /// <summary>Strolls up to this far from its spawn point while idle; 0 = stands still.</summary>
    public float WanderRadius { get; set; } = 5f;

    public int RespawnSeconds { get; set; } = 60;

    public int Coins { get; set; } = 10;
}
