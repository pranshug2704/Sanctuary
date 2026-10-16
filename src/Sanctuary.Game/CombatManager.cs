using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Extensions;
using Sanctuary.Core.IO;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Resources.Definitions.Combat;
using Sanctuary.Game.Resources.Definitions.Rewards;
using Sanctuary.Game.Zones;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game;

/// <summary>
/// Overworld combat, built from what the client does with each packet:
/// <list type="bullet">
/// <item>enemies are sent with AddNpc Disposition 0 (hostile name style) and Unknown41 (hit point bar);</item>
/// <item>a hit is PlayerUpdatePacketHitPointModification: the target's bar or combat dock follows
/// Current/Max and, with overworld combat on, the client floats Delta (yellow dealt, red taken);</item>
/// <item>the client's combat bar sends AbilityPacketClientRequestStartAbility (bar 1, slot 0/1) with no
/// target, so the server picks targets within the job kit's AutoTargetReach;</item>
/// <item>EncounterPacketIsFighting toggles the dock's combat indicator.</item>
/// </list>
/// </summary>
public sealed class CombatManager : ICombatManager, IDisposable
{
    private const int WorldZoneDefinitionId = 1;
    private const int TickMs = 250;
    private const int OutOfCombatMs = 8000;
    private const int RecoilAnimationId = 1121;      // com_recoil_01
    private const int DeathAnimationId = 1151;       // com_death_01
    private const int DeathFadeDelayMs = 2500;
    private const int EnergyPerHit = 5;
    private const float ReachTolerance = 2f;
    private const int ChaseRepathMs = 500;
    private const float WanderWatchRange = 80f;
    private const float RangedAttackRange = 18f;

    // Basic attack swings by job (AnimationTypes.xml): the ability data's 1099 isn't an animation slot.
    private static readonly Dictionary<int, int[]> BasicSwings = new()
    {
        [32] = [1021, 1022, 1023, 1024],   // warrior: com_1hs_attack_0x
        [35] = [1101, 1102, 1103, 1104],   // archer: com_range_attack_0x
        [12] = [1111, 1112],               // wizard: com_cast_0x
        [2] = [1061, 1062, 1063, 1064],    // ninja: com_dws_attack_0x
    };
    private static readonly int[] DefaultSwings = [1001, 1002, 1003, 1004];  // com_h2h_attack_0x

    private readonly ILogger _logger;
    private readonly IZoneManager _zoneManager;
    private readonly IRewardManager _rewardManager;
    private readonly IQuestManager _questManager;
    private readonly Timer _timer;
    private readonly object _sync = new();

    private readonly Dictionary<ulong, Enemy> _enemies = [];
    private readonly HashSet<IZone> _initializedZones = [];
    private List<EnemyDefinition> _definitions = [];
    private Dictionary<int, EnemyAnimationSet> _animations = [];
    private long _lastSecond;
    private int _swing;

    private sealed class Enemy
    {
        public required Npc Npc { get; init; }
        public required EnemyDefinition Definition { get; init; }
        public EnemyAnimationSet? Animations { get; init; }
        public float AttackRange { get; init; }
        public int Swing { get; set; }
        public int Hitpoints { get; set; }
        public bool Dead { get; set; }
        public long RespawnTick { get; set; }
        public ulong AggroGuid { get; set; }
        public long NextAttackTick { get; set; }

        /// <summary>Where it was spawned; it wanders around here and walks back here when it gives up.</summary>
        public Vector4 Home { get; init; }
        public Quaternion HomeRotation { get; init; }
        public long NextMoveTick { get; set; }
        public bool Returning { get; set; }
    }

    public CombatManager(ILogger<CombatManager> logger, IZoneManager zoneManager, IRewardManager rewardManager,
        IQuestManager questManager)
    {
        _logger = logger;
        _zoneManager = zoneManager;
        _rewardManager = rewardManager;
        _questManager = questManager;

        LoadDefinitions();

        _timer = new Timer(_ => Tick(), null, TickMs, TickMs);
    }

    public void Dispose() => _timer.Dispose();

    #region Definitions

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private void LoadDefinitions()
    {
        var animationsPath = Path.Combine(ResourceManager.BaseDirectory, "EnemyAnimations.json");

        try
        {
            if (File.Exists(animationsPath))
            {
                using var animationsStream = File.OpenRead(animationsPath);
                _animations = JsonSerializer.Deserialize<Dictionary<int, EnemyAnimationSet>>(animationsStream, JsonOptions) ?? [];
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load \"{file}\".", animationsPath);
        }

        var path = Path.Combine(ResourceManager.BaseDirectory, "Enemies.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning("No \"{file}\": no enemies.", path);
            return;
        }

        try
        {
            using var stream = File.OpenRead(path);
            _definitions = JsonSerializer.Deserialize<List<EnemyDefinition>>(stream, JsonOptions) ?? [];

            _logger.LogInformation("Loaded {count} enemy definitions, animations for {models} models.",
                _definitions.Count, _animations.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load \"{file}\".", path);
        }
    }

    /// <summary>Turns the world zone's matching npcs into enemies (once per zone).</summary>
    private void EnsureEnemies(IZone zone)
    {
        if (zone.DefinitionId != WorldZoneDefinitionId || zone.OwnerId is not null || !_initializedZones.Add(zone))
            return;

        var byName = new Dictionary<string, EnemyDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in _definitions)
        {
            foreach (var name in definition.Names)
                byName.TryAdd(name, definition);
        }

        foreach (var npc in zone.Npcs)
        {
            if (npc is QuestObject or CollectionNode or CollectionCoin or Mount || npc.Name is null ||
                !byName.TryGetValue(npc.Name, out var definition))
                continue;

            npc.Disposition = 0;
            npc.ShowHealthBar = true;
            npc.MovementController = null;   // CombatManager walks it (wander, chase, return home)

            npc.Speed = definition.MoveSpeed;
            var animations = _animations.GetValueOrDefault(npc.ModelId);
            _enemies[npc.Guid] = new Enemy
            {
                Npc = npc,
                Definition = definition,
                Animations = animations,
                AttackRange = animations?.IsRanged == true ? Math.Max(definition.AttackRange, RangedAttackRange) : definition.AttackRange,
                Hitpoints = definition.MaxHitpoints,
                Home = npc.Position,
                HomeRotation = npc.Rotation,
                NextMoveTick = Environment.TickCount64 + Random.Shared.Next(2000, 15000)
            };

            // Players who already see it get it again as an enemy.
            foreach (var player in npc.VisiblePlayers.Values.ToList())
            {
                player.OnRemoveVisibleNpcs(npc);
                player.OnAddVisibleNpcs(npc);
            }
        }

        _logger.LogInformation("Zone {zone}: {count} enemies.", zone.DefinitionId, _enemies.Count);
    }

    #endregion

    #region Player actions

    public void OnPlayerReady(Player player)
    {
        player.SendTunneled(new EncounterOverworldCombatPacket { InWorldCombat = true });
        player.Hitpoints = Math.Min(player.Hitpoints, player.MaxHitpoints);
        player.SendTunneled(new ClientUpdatePacketHitpoints { CurrentHitpoints = player.Hitpoints, MaxHitpoints = player.MaxHitpoints });
    }

    public bool TryAttackTarget(Player player, Npc npc)
    {
        lock (_sync)
        {
            if (!_enemies.TryGetValue(npc.Guid, out var enemy) || enemy.Dead)
                return false;

            player.CombatTargetGuid = npc.Guid;
        }

        // Out of reach the click only selects it; the next basic attack (or click) in reach hits it.
        TryUseAbility(player, ICombatManager.CombatActionBarId, 0);
        return true;
    }

    public bool TryUseAbility(Player player, int actionBarId, int slot)
    {
        if (actionBarId != ICombatManager.CombatActionBarId || !player.TryGetCombatAbility(slot, out var kit, out var ability))
            return false;

        lock (_sync)
        {
            EnsureEnemies(player.Zone);

            var now = Environment.TickCount64;

            if (slot == 0)
            {
                if (now < player.NextBasicAttackTick)
                    return true;

                player.NextBasicAttackTick = now + kit.BasicRecastMs;
            }
            else if (!player.TrySpendEnergy(ability.EnergyCost))
            {
                player.SendTunneled(new AbilityPacketFailed { StringId = 3079 });
                return true;
            }

            var reach = (slot == 0 && kit.BasicAutoTargetReach > 0 ? kit.BasicAutoTargetReach : kit.AutoTargetReach) + ReachTolerance;
            var inReach = LivingEnemiesNear(player.Zone, player.Position, Math.Max(reach, ability.AoeRadius))
                .OrderBy(x => Distance(x.Npc.Position, player.Position))
                .ToList();

            // Single target: the clicked enemy if it's in reach, else the nearest.
            var preferred = inReach.FirstOrDefault(x => x.Npc.Guid == player.CombatTargetGuid);
            var targets = ability.EffectType.StartsWith("Aoe", StringComparison.OrdinalIgnoreCase)
                ? inReach
                : preferred is not null ? [preferred] : inReach.Take(1).ToList();

            PlayAnimation(player, SwingAnimation(player, ability, slot));

            if (ability.CastEffectId != 0)
                PlayEffect(player, player.Guid, ability.CastEffectId);

            foreach (var enemy in targets)
                Hit(player, enemy, (int)(ability.Damage * JobLevelHelper.DamageMultiplier(player.ActiveProfile.Rank)), ability.HitEffectId, now);

            if (targets.Count > 0)
            {
                if (slot == 0)
                    player.AddEnergy(EnergyPerHit);

                EnterCombat(player, now);
            }

            return true;
        }
    }

    private int SwingAnimation(Player player, AbilityDefinition ability, int slot)
    {
        if (slot != 0 && ability.AnimationId != 0)
            return ability.AnimationId;

        var swings = BasicSwings.TryGetValue(player.ActiveProfileId, out var jobSwings) ? jobSwings : DefaultSwings;
        return swings[_swing++ % swings.Length];
    }

    private void Hit(Player player, Enemy enemy, int damage, int hitEffectId, long now)
    {
        enemy.Hitpoints = Math.Max(0, enemy.Hitpoints - damage);

        if (enemy.AggroGuid == 0)
            enemy.NextAttackTick = now + enemy.Definition.AttackIntervalMs / 2;

        enemy.AggroGuid = player.Guid;

        var packet = new PlayerUpdatePacketHitPointModification
        {
            AttackerGuid = player.Guid,
            TargetGuid = enemy.Npc.Guid,
            ShowDelta = true,
            MaxHitpoints = enemy.Definition.MaxHitpoints,
            CurrentHitpoints = enemy.Hitpoints,
            Delta = -damage
        };
        SendToViewers(enemy.Npc, packet, player);

        if (hitEffectId != 0)
            PlayEffect(enemy.Npc, enemy.Npc.Guid, hitEffectId, player);

        if (enemy.Hitpoints > 0)
        {
            if (Pick(enemy.Animations?.Recoil, RecoilAnimationId) is var recoil and not 0)
                PlayAnimation(enemy.Npc, recoil, player);
            return;
        }

        Kill(player, enemy, now);
    }

    private void Kill(Player player, Enemy enemy, long now)
    {
        enemy.Dead = true;
        enemy.AggroGuid = 0;
        enemy.Npc.StopMoving();
        enemy.RespawnTick = now + enemy.Definition.RespawnSeconds * 1000L;

        if (Pick(enemy.Animations?.Death, DeathAnimationId) is var death and not 0)
            PlayAnimation(enemy.Npc, death, player);

        foreach (var viewer in Viewers(enemy.Npc, player))
        {
            viewer.OnRemoveVisibleNpcGracefully(enemy.Npc, animate: true, delay: DeathFadeDelayMs, effectDelay: 0,
                compositeEffectId: 0, duration: 1000);
        }

        foreach (var viewer in enemy.Npc.VisiblePlayers.Values.ToList())
            enemy.Npc.OnRemoveVisiblePlayers(viewer);

        // Hidden until it respawns, also from players who walk up later.
        enemy.Npc.Visible = false;

        if (enemy.Definition.Coins > 0)
            _rewardManager.TryGrantCurrency(player, CurrencyType.Coins, enemy.Definition.Coins);

        if (enemy.Definition.Experience > 0)
            _rewardManager.TryGrantExperience(player, enemy.Definition.Experience);

        if (!string.IsNullOrEmpty(enemy.Definition.Loot) && Random.Shared.NextDouble() < enemy.Definition.LootChance &&
            _rewardManager.TryRollReward(enemy.Definition.Loot, out var drop) && drop is not null)
            _rewardManager.TryGrantReward(player, drop, enemy.Npc.Guid);

        if (Random.Shared.NextDouble() < enemy.Definition.GearChance)
            _rewardManager.TryGrantJobGear(player, enemy.Npc.Guid);

        _questManager.OnDefeat(player, enemy.Npc);

        _logger.LogDebug("{player} defeated {npc} ({guid}).", player.Guid, enemy.Npc.Name, enemy.Npc.Guid);
    }

    #endregion

    #region Enemies' turn

    private void Tick()
    {
        try
        {
            lock (_sync)
            {
                var now = Environment.TickCount64;

                foreach (var zone in _zoneManager.Zones)
                    EnsureEnemies(zone);

                foreach (var enemy in _enemies.Values)
                {
                    if (enemy.Dead)
                    {
                        if (now >= enemy.RespawnTick)
                            Respawn(enemy);

                        continue;
                    }

                    if (enemy.AggroGuid == 0 && enemy.Definition.AggroRange > 0)
                        enemy.AggroGuid = enemy.Npc.Zone.Players
                            .Where(x => x.Hitpoints > 0 && Distance(x.Position, enemy.Npc.Position) <= enemy.Definition.AggroRange)
                            .Select(x => x.Guid)
                            .FirstOrDefault();

                    if (enemy.AggroGuid != 0)
                        Fight(enemy, now);
                    else
                        Idle(enemy, now);
                }

                if (now - _lastSecond >= 1000)
                {
                    _lastSecond = now;
                    EverySecond(now);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Combat tick failed.");
        }
    }

    private void Fight(Enemy enemy, long now)
    {
        // Leashed to home: it gives up once the player has led it too far away.
        if (!enemy.Npc.Zone.TryGetPlayer(enemy.AggroGuid, out var player) ||
            Distance(player.Position, enemy.Home) > enemy.Definition.LeashRange)
        {
            Reset(enemy);
            return;
        }

        var distance = Distance(player.Position, enemy.Npc.Position);

        if (distance > enemy.AttackRange)
        {
            // Chase: run at the player, stopping a little inside attack range.
            if (enemy.Definition.Chases && now >= enemy.NextMoveTick)
            {
                enemy.NextMoveTick = now + ChaseRepathMs;
                WalkTo(enemy.Npc, PointShortOf(enemy.Npc.Position, player.Position, enemy.AttackRange * 0.6f));
            }

            return;
        }

        if (enemy.Npc.IsMoving)
            enemy.Npc.StopMoving();

        if (now < enemy.NextAttackTick)
            return;

        enemy.NextAttackTick = now + enemy.Definition.AttackIntervalMs;

        enemy.Npc.FaceTowards(new Vector3(player.Position.X, player.Position.Y, player.Position.Z));
        PlayAnimation(enemy.Npc, NextSwing(enemy), player);

        var damage = enemy.Definition.Damage;
        player.Hitpoints = Math.Max(0, player.Hitpoints - damage);

        var packet = new PlayerUpdatePacketHitPointModification
        {
            AttackerGuid = enemy.Npc.Guid,
            TargetGuid = player.Guid,
            ShowDelta = true,
            MaxHitpoints = player.MaxHitpoints,
            CurrentHitpoints = player.Hitpoints,
            Delta = -damage
        };
        player.SendTunneledToVisible(packet, sendToSelf: true);
        player.SendTunneled(new ClientUpdatePacketHitpoints { CurrentHitpoints = player.Hitpoints, MaxHitpoints = player.MaxHitpoints });

        EnterCombat(player, now);

        if (player.Hitpoints <= 0)
            KnockOut(player);
    }

    /// <summary>Out of combat: home after giving up, then an occasional stroll near home while someone is around.</summary>
    private void Idle(Enemy enemy, long now)
    {
        if (enemy.Returning)
        {
            if (!enemy.Npc.IsMoving)
            {
                enemy.Returning = false;
                enemy.Npc.UpdatePosition(enemy.Home, enemy.HomeRotation);
            }

            return;
        }

        if (enemy.Definition.WanderRadius <= 0 || now < enemy.NextMoveTick || enemy.Npc.IsMoving)
            return;

        enemy.NextMoveTick = now + Random.Shared.Next(6000, 16000);

        if (!enemy.Npc.Zone.Players.Any(x => Distance(x.Position, enemy.Home) <= WanderWatchRange))
            return;

        var angle = Random.Shared.NextSingle() * MathF.Tau;
        var radius = Random.Shared.NextSingle() * enemy.Definition.WanderRadius;
        WalkTo(enemy.Npc, new Vector3(enemy.Home.X + MathF.Cos(angle) * radius, enemy.Home.Y, enemy.Home.Z + MathF.Sin(angle) * radius));
    }

    /// <summary>The enemy's next attack animation: its configured one, else its model's swings in turn.</summary>
    private static int NextSwing(Enemy enemy)
    {
        if (enemy.Definition.AttackAnimationId != 0)
            return enemy.Definition.AttackAnimationId;

        var attacks = enemy.Animations?.Attacks;
        if (attacks is not { Count: > 0 })
            return DefaultSwings[0];

        return attacks[enemy.Swing++ % attacks.Count];
    }

    /// <summary>A random one of the model's animations; the fallback when none are listed (null list).</summary>
    private static int Pick(List<int>? animations, int fallback) => animations switch
    {
        null => fallback,
        { Count: 0 } => 0,
        _ => animations[Random.Shared.Next(animations.Count)]
    };

    /// <summary>
    /// Walks along the zone navmesh (around trees, rocks and buildings) when it has a path,
    /// otherwise straight at the goal.
    /// </summary>
    private static void WalkTo(Npc npc, Vector3 goal)
    {
        var waypoints = new Queue<Vector3>();

        var found = npc.Zone.TryFindNavMeshPath(npc.Position.ToVector3(), goal, waypoints) && waypoints.Count > 0;
        if (found)
            npc.MoveTo(waypoints);
        else
            npc.MoveTo(goal, direct: true);
    }

    private static Vector3 PointShortOf(Vector4 from, Vector4 to, float shortBy)
    {
        var start = new Vector3(from.X, from.Y, from.Z);
        var end = new Vector3(to.X, to.Y, to.Z);
        var toEnd = end - start;
        var length = toEnd.Length();

        return length <= shortBy ? start : start + toEnd * ((length - shortBy) / length);
    }

    /// <summary>The player ran out of hit points: back on their feet, and every enemy forgets them.</summary>
    private void KnockOut(Player player)
    {
        foreach (var enemy in _enemies.Values.Where(x => x.AggroGuid == player.Guid))
            Reset(enemy);

        player.Hitpoints = player.MaxHitpoints;
        player.SendTunneled(new ClientUpdatePacketHitpoints { CurrentHitpoints = player.Hitpoints, MaxHitpoints = player.MaxHitpoints });
        LeaveCombat(player);

        ChatHelper.SendSystemMessage(player, "You were knocked out!");
        _logger.LogDebug("{player} was knocked out.", player.Guid);
    }

    private void Reset(Enemy enemy)
    {
        enemy.AggroGuid = 0;
        enemy.Hitpoints = enemy.Definition.MaxHitpoints;
        enemy.Returning = true;
        WalkTo(enemy.Npc, new Vector3(enemy.Home.X, enemy.Home.Y, enemy.Home.Z));

        SendToViewers(enemy.Npc, new PlayerUpdatePacketHitPointModification
        {
            AttackerGuid = enemy.Npc.Guid,
            TargetGuid = enemy.Npc.Guid,
            MaxHitpoints = enemy.Definition.MaxHitpoints,
            CurrentHitpoints = enemy.Hitpoints
        });
    }

    private void Respawn(Enemy enemy)
    {
        enemy.Dead = false;
        enemy.AggroGuid = 0;
        enemy.Hitpoints = enemy.Definition.MaxHitpoints;
        enemy.Returning = false;
        enemy.Npc.StopMoving();
        enemy.Npc.UpdatePosition(enemy.Home, enemy.HomeRotation);
        enemy.Npc.Visible = true;

        var tile = enemy.Npc.ZoneTile;

        foreach (var player in enemy.Npc.Zone.Players)
        {
            if (!player.ZoneTile.VisibleTiles.Contains(tile) || player.VisibleNpcs.ContainsKey(enemy.Npc.Guid))
                continue;

            player.OnAddVisibleNpcs(enemy.Npc);
            enemy.Npc.OnAddVisiblePlayers(player);
        }
    }

    private void EverySecond(long now)
    {
        foreach (var zone in _zoneManager.Zones)
        {
            foreach (var player in zone.Players)
            {
                if (player.TryGetCombatAbility(0, out var kit, out _))
                    player.AddEnergy(kit.Energy.RegenPerSecond);

                if (player.InCombat && now - player.LastCombatTick > OutOfCombatMs)
                    LeaveCombat(player);

                // Max hit points follow the active job's level (job switch, level up).
                if (player.Hitpoints > player.MaxHitpoints)
                {
                    player.Hitpoints = player.MaxHitpoints;
                    player.SendTunneled(new ClientUpdatePacketHitpoints { CurrentHitpoints = player.Hitpoints, MaxHitpoints = player.MaxHitpoints });
                }

                // Out of combat, hit points come back over ~20 seconds.
                if (!player.InCombat && player.Hitpoints < player.MaxHitpoints)
                {
                    player.Hitpoints = Math.Min(player.MaxHitpoints, player.Hitpoints + player.MaxHitpoints / 20);
                    player.SendTunneled(new ClientUpdatePacketHitpoints { CurrentHitpoints = player.Hitpoints, MaxHitpoints = player.MaxHitpoints });
                }
            }
        }
    }

    private static void EnterCombat(Player player, long now)
    {
        player.LastCombatTick = now;

        if (player.InCombat)
            return;

        player.InCombat = true;
        player.SendTunneled(new EncounterPacketIsFighting { IsFighting = true });
    }

    private static void LeaveCombat(Player player)
    {
        if (!player.InCombat)
            return;

        player.InCombat = false;
        player.SendTunneled(new EncounterPacketIsFighting { IsFighting = false });
    }

    #endregion

    #region Helpers

    private IEnumerable<Enemy> LivingEnemiesNear(IZone zone, Vector4 position, float radius)
    {
        return _enemies.Values.Where(x => !x.Dead && x.Npc.Zone == zone && Distance(x.Npc.Position, position) <= radius);
    }

    private static float Distance(Vector4 a, Vector4 b)
    {
        return Vector3.Distance(new Vector3(a.X, a.Y, a.Z), new Vector3(b.X, b.Y, b.Z));
    }

    /// <summary>Players who can see the npc, plus the acting player.</summary>
    private static IEnumerable<Player> Viewers(Npc npc, Player? actor = null)
    {
        var viewers = npc.VisiblePlayers.Values.ToList();

        if (actor is not null && !viewers.Contains(actor))
            viewers.Add(actor);

        return viewers;
    }

    private static void SendToViewers(Npc npc, ISerializablePacket packet, Player? actor = null)
    {
        foreach (var viewer in Viewers(npc, actor))
            viewer.SendTunneled(packet);
    }

    private static void PlayAnimation(Npc npc, int animationId, Player? actor = null)
    {
        SendToViewers(npc, new PlayerUpdatePacketSetAnimation { Guid = npc.Guid, AnimationId = animationId }, actor);
    }

    private static void PlayAnimation(Player player, int animationId)
    {
        player.SendTunneledToVisible(new PlayerUpdatePacketSetAnimation { Guid = player.Guid, AnimationId = animationId }, sendToSelf: true);
    }

    private static void PlayEffect(Npc npc, ulong guid, int effectId, Player? actor = null)
    {
        SendToViewers(npc, new PlayerUpdatePacketPlayCompositeEffect { Guid = guid, CompositeEffectId = effectId }, actor);
    }

    private static void PlayEffect(Player player, ulong guid, int effectId)
    {
        player.SendTunneledToVisible(new PlayerUpdatePacketPlayCompositeEffect { Guid = guid, CompositeEffectId = effectId }, sendToSelf: true);
    }

    #endregion
}
