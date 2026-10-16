using System.Globalization;
using System.Collections.Generic;
using System.Linq;

using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;

namespace Sanctuary.Game.ChatCommands;

/// <summary>
/// Sends packets recovered from the client but not yet used by game logic, so
/// their effect can be checked in game.
/// </summary>
public class ReverseEngineeringChatCommand : IChatCommand
{
    public string KeyWord => "re";
    public string Usage => "scale <factor> | head <factor> | notify <code_string> | bubble <image_id> [seconds] | bubble clear | idle <anim_id> | knock [distance] [anim_id] | wp [label] | node <type> | mark <type> <icon> [key] [off] | quest [goto <npc> | talk [name] | accept [id] | turnin [id] | offer [undeclinable] [b6] [b9] [u2=n] [u5=n] [npc] | add | progress <n> [silent] | objdone | end [npc] | done | camera | reset | skip | obj | giver <questId>] | combat ...";
    public string Description => "Test recovered client packets on yourself.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    private readonly IQuestManager _questManager;
    private readonly IResourceManager _resourceManager;
    private readonly ICombatManager _combatManager;
    private readonly IRewardManager _rewardManager;

    public ReverseEngineeringChatCommand(IQuestManager questManager, IResourceManager resourceManager, ICombatManager combatManager,
        IRewardManager rewardManager)
    {
        _rewardManager = rewardManager;
        _questManager = questManager;
        _resourceManager = resourceManager;
        _combatManager = combatManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        var needsValue = args.Length > 0 && args[0].ToLowerInvariant() is not ("knock" or "quest" or "wp" or "combat");
        if (args.Length < 1 || (needsValue && args.Length < 2))
        {
            ChatHelper.SendSystemMessage(invoker, $"Invalid usage. Use: !{KeyWord} {Usage}");
            return false;
        }

        switch (args[0].ToLowerInvariant())
        {
            case "bubble":
                {
                    var clear = args[1].Equals("clear", System.StringComparison.OrdinalIgnoreCase);
                    var effectId = 0;
                    var seconds = 5;
                    if (!clear && !int.TryParse(args[1], out effectId)
                        || args.Length > 2 && !int.TryParse(args[2], out seconds))
                    {
                        ChatHelper.SendSystemMessage(invoker, "Use: !re bubble <notification_image_id> [seconds], e.g. 42 (sad face), 77 (moon), 81 (sun). See RE/analysis/notification_images.txt.");
                        return false;
                    }

                    invoker.SendTunneledToVisible(new PlayerUpdatePacketThoughtBubble
                    {
                        Guid = invoker.Guid,
                        IconId = effectId,
                        DurationSeconds = seconds,
                        Clear = clear
                    }, sendToSelf: true);
                    break;
                }

            case "quest":
                return HandleQuest(invoker, args);

            case "combat":
                return HandleCombat(invoker, args);

            case "wp":
                {
                    // Records the player's position (e.g. quest ReachLocation goals) to waypoints.txt
                    // in the Gateway's working directory. !re wp [label]
                    var position = invoker.Position;
                    var label = args.Length > 1 ? string.Join(' ', args[1..]) : "";
                    var line = string.Create(CultureInfo.InvariantCulture,
                        $"{System.DateTime.Now:HH:mm:ss}	{position.X:0.##}	{position.Y:0.##}	{position.Z:0.##}	{label}");
                    System.IO.File.AppendAllText("waypoints.txt", line + System.Environment.NewLine);
                    ChatHelper.SendSystemMessage(invoker, $"Waypoint saved: {position.X:0.#}, {position.Y:0.#}, {position.Z:0.#} {label}");
                    return true;
                }

            case "node":
                {
                    // Teleports next to the nearest active collection node of a type
                    // (e.g. sparkling-water); rare pools ("...-rare") are a different type.
                    var type = args[1].ToLowerInvariant();
                    var node = invoker.Zone.GetCollectionNodeSpawnStatuses()
                        .Where(x => x.Active && x.Pool.EndsWith("-" + type, System.StringComparison.OrdinalIgnoreCase))
                        .OrderBy(x => System.Numerics.Vector4.DistanceSquared(x.Position, invoker.Position))
                        .FirstOrDefault();
                    if (node is null)
                    {
                        ChatHelper.SendSystemMessage(invoker, $"No active '{type}' node in this zone.");
                        return false;
                    }

                    var destination = node.Position with { X = node.Position.X + 3f, W = invoker.Position.W };
                    invoker.UpdatePosition(destination, invoker.Rotation, false);
                    invoker.SendTunneled(new ClientUpdatePacketUpdateLocation
                    {
                        Position = destination,
                        Rotation = invoker.Rotation,
                        Teleport = true
                    });
                    ChatHelper.SendSystemMessage(invoker, $"Next to {node.Pool} #{node.Id}.");
                    return true;
                }

            case "mark":
                {
                    // Marker over the nearest npc (PlayerUpdatePacketAddNotifications), to find the
                    // values for "quest available" / "quest to turn in".
                    // !re mark <type> <icon> [key] [off]: the client registers <icon> (a
                    // NotificationImages.txt id) under <key> (wire IconState, must be > 0) with
                    // FUN_00960170, then draws the key's icon over the npc (FUN_01025670/FUN_00a91800).
                    if (!int.TryParse(args[1], out var notificationType) ||
                        args.Length < 3 || !int.TryParse(args[2], out var iconId))
                    {
                        ChatHelper.SendSystemMessage(invoker, "Use: !re mark <notification_type> <icon_id> [key] [off]");
                        return false;
                    }

                    var npc = NearestNpc(invoker);
                    if (npc is null)
                    {
                        ChatHelper.SendSystemMessage(invoker, "No NPC in view.");
                        return false;
                    }

                    if (args.Any(x => x.Equals("off", System.StringComparison.OrdinalIgnoreCase)))
                    {
                        var removal = new PlayerUpdatePacketRemoveNotifications();
                        removal.Notifications.Add(new Sanctuary.Packet.Common.RemoveNotificationInfo
                        {
                            Guid = npc.Guid,
                            ThoughtBubbleIconId = args.Length > 3 && int.TryParse(args[3], out var removeKey) ? removeKey : 1
                        });
                        invoker.SendTunneled(removal);
                        ChatHelper.SendSystemMessage(invoker, $"Removed marker from {npc.Name ?? "?"}.");
                        return true;
                    }

                    var packet = new PlayerUpdatePacketAddNotifications();
                    packet.Notifications.Add(new Sanctuary.Packet.Common.NotificationInfo
                    {
                        Guid = npc.Guid,
                        NotificationType = notificationType,
                        IconId = iconId,
                        IconState = args.Length > 3 && int.TryParse(args[3], out var key) ? key : 1,
                        NameId = npc.NameId,
                        IsCompact = args.Any(x => x.Equals("off", System.StringComparison.OrdinalIgnoreCase)),
                        Enabled = !args.Any(x => x.Equals("off", System.StringComparison.OrdinalIgnoreCase))
                    });
                    invoker.SendTunneled(packet);
                    ChatHelper.SendSystemMessage(invoker, $"Marker type {notificationType} icon {iconId} on {npc.Name ?? "?"}.");
                    return true;
                }

            case "idle":
                if (!int.TryParse(args[1], out var animationId))
                {
                    ChatHelper.SendSystemMessage(invoker, "Use: !re idle <anim_id> (0 restores the default).");
                    return false;
                }

                invoker.SendTunneledToVisible(new PlayerUpdatePacketUpdateIdleAnim
                {
                    Guid = invoker.Guid,
                    AnimationId = animationId
                }, sendToSelf: true);
                break;

            case "knock":
                {
                    var distance = 5f;
                    var knockAnimation = 0;
                    if (args.Length > 1 && !float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out distance)
                        || args.Length > 2 && !int.TryParse(args[2], out knockAnimation))
                    {
                        ChatHelper.SendSystemMessage(invoker, "Use: !re knock [distance] [anim_id]");
                        return false;
                    }

                    // Land `distance` units along +X, facing back toward where we started.
                    var start = invoker.Position;
                    var destination = start with { X = start.X + distance };
                    invoker.SendTunneledToVisible(new PlayerUpdatePacketKnockback
                    {
                        Guid = invoker.Guid,
                        AnimationId = knockAnimation,
                        Destination = destination,
                        FacingDirection = new System.Numerics.Vector4(-1f, 0f, 0f, 0f),
                        LaunchStrength = 1.0f
                    }, sendToSelf: true);

                    // The knockback leaves the local player's movement in a knocked-back state
                    // that the client only clears on a server location update
                    // (ClientUpdatePacketUpdateLocation -> FUN_0094e2b0 -> FUN_00963840).
                    // Send one at the landing spot once the arc has finished.
                    invoker.SendTunneledDelayed(new ClientUpdatePacketUpdateLocation
                    {
                        Position = destination,
                        Rotation = invoker.Rotation,
                        Teleport = false
                    }, 1500);
                    break;
                }

            case "scale":
                if (!TryParseFactor(invoker, args[1], out var scale))
                    return false;

                invoker.SendTunneledToVisible(new PlayerUpdatePacketUpdateScale
                {
                    Guid = invoker.Guid,
                    Scale = scale
                }, sendToSelf: true);
                break;

            case "head":
                if (!TryParseFactor(invoker, args[1], out var head))
                    return false;

                invoker.SendTunneledToVisible(new PlayerUpdatePacketHeadInflationScale
                {
                    Guid = invoker.Guid,
                    Scale = head
                }, sendToSelf: true);
                break;

            case "notify":
                // A CodeStringMappings.txt name such as JobLevelUp, not free text.
                invoker.SendTunneled(new ClientUpdatePacketNotifyPlayer
                {
                    CodeString = args[1]
                });
                break;

            default:
                ChatHelper.SendSystemMessage(invoker, $"Unknown test '{args[0]}'. Use: !{KeyWord} {Usage}");
                return false;
        }

        ChatHelper.SendSystemMessage(invoker, $"Sent '{args[0]}' test packet.");
        return true;
    }

    public const int TestQuestId = 9001;
    private const int TestObjectiveId = 1;
    private const int TestObjectiveTotal = 5;

    /// <summary>
    /// Test reward: every unidentified number is distinct so the dev client's DevTrace.log
    /// (SetRewardCoin / SetRewardExp / AddRewardItem) shows which one the screen uses.
    /// </summary>
    private static void FillTestReward(Sanctuary.Packet.Common.QuestRewardData reward)
    {
        var bundle = reward.Bundle;
        bundle.Coins = 101;
        bundle.Experience = 108;
        bundle.Unknown2 = 102;
        bundle.Unknown4 = 104;
        bundle.Unknown5 = 105;
        bundle.Unknown6 = 106;
        bundle.Unknown7 = 107;
        bundle.IconId = 190;           // Penguin Badge icon
        bundle.NameId = 3258;          // "Penguin Badge"
        bundle.Entries.Add(new Sanctuary.Packet.Common.RewardBundleEntryItem
        {
            IconId = 190,
            TintId = 231,
            NameId = 3258,
            DefinitionId = 1,          // Penguin Badge
            Quantity = 2
        });
        reward.Unknown = 109;
    }

    /// <summary>
    /// !re combat ...: send the combat packets by hand to learn what the client does with them.
    /// Targets the nearest real npc (not quest objects or collection nodes).
    /// </summary>
    private bool HandleCombat(Player invoker, string[] args)
    {
        var step = args.Length > 1 ? args[1].ToLowerInvariant() : "";
        int Arg(int index, int fallback) => args.Length > index && int.TryParse(args[index], out var v) ? v : fallback;
        float FloatArg(int index, float fallback) =>
            args.Length > index && float.TryParse(args[index], System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : fallback;
        bool Flag(string name) => args.Skip(2).Any(x => x.Equals(name, System.StringComparison.OrdinalIgnoreCase));

        var target = invoker.VisibleNpcs.Values
            .Where(x => x is not (QuestObject or CollectionNode or CollectionCoin or Mount))
            .OrderBy(x => System.Numerics.Vector4.DistanceSquared(x.Position, invoker.Position))
            .FirstOrDefault();

        switch (step)
        {
            case "ow":
                // EncounterOverworldCombatPacket: overworld combat mode on/off.
                invoker.SendTunneled(new EncounterOverworldCombatPacket { InWorldCombat = Arg(2, 1) != 0 });
                break;

            case "attack":
                // !re combat attack [slot]: use the combat bar ability as if the key was pressed
                // (0 = basic, 1 = special), so tests need no keyboard.
                if (!_combatManager.TryUseAbility(invoker, ICombatManager.CombatActionBarId, Arg(2, 0)))
                {
                    ChatHelper.SendSystemMessage(invoker, "No combat ability in that slot (switch to a combat job).");
                    return false;
                }
                break;

            case "job":
                // !re combat job <profileId>: switch job as CommandPacketSetProfile does
                // (32 warrior, 35 archer, 2 ninja, 11 medic, 12 wizard, 43 brawler).
                {
                    var profile = invoker.Profiles.FirstOrDefault(x => x.Id == Arg(2, 32));
                    if (profile is null)
                    {
                        ChatHelper.SendSystemMessage(invoker, "You don't have that job.");
                        return false;
                    }

                    invoker.ClearWeaponFlairOverride();
                    invoker.ActiveProfileId = profile.Id;

                    using var writer = new Sanctuary.Core.IO.PacketWriter();
                    profile.Serialize(writer);
                    invoker.SendTunneled(new ClientUpdatePacketActivateProfile
                    {
                        Payload = writer.Buffer,
                        Attachments = invoker.GetAttachments(),
                        Animation = 3001,
                        CompositeEffect = 4005
                    });
                    invoker.SendToolbar();
                    break;
                }

            case "goto":
                // !re combat goto [name]: teleport next to the nearest living enemy.
                {
                    var enemy = _combatManager.FindNearestEnemy(invoker, args.Length > 2 ? string.Join(' ', args.Skip(2)) : null);
                    if (enemy is null)
                    {
                        ChatHelper.SendSystemMessage(invoker, "No living enemy found in this zone.");
                        return false;
                    }

                    var destination = enemy.Position with { X = enemy.Position.X + 3f, W = invoker.Position.W };
                    invoker.UpdatePosition(destination, invoker.Rotation, false);
                    invoker.SendTunneled(new ClientUpdatePacketUpdateLocation { Position = destination, Rotation = invoker.Rotation, Teleport = true });
                    ChatHelper.SendSystemMessage(invoker, $"{enemy.Name} ({enemy.Guid}).");
                    break;
                }

            case "xp":
                // !re combat xp <amount>: experience for the active job, as a quest or kill grants.
                if (!_rewardManager.TryGrantExperience(invoker, Arg(2, 50)))
                {
                    ChatHelper.SendSystemMessage(invoker, "No experience granted (job already level 20?).");
                    return false;
                }
                ChatHelper.SendSystemMessage(invoker, $"Level {invoker.ActiveProfile.Rank}, {invoker.ProfileExperience.GetValueOrDefault(invoker.ActiveProfileId)}/{JobLevelHelper.ExperienceToNextLevel(invoker.ActiveProfile.Rank)} xp.");
                break;

            case "gear":
                // !re combat gear: a piece of gear for the active job, as a lucky drop would give.
                if (!_rewardManager.TryGrantJobGear(invoker, target?.Guid ?? 0))
                {
                    ChatHelper.SendSystemMessage(invoker, "No job gear for this job and level.");
                    return false;
                }
                break;

            case "loot":
                // !re combat loot <table>: roll a Rewards.json table as a defeat would (e.g. loot-robgoblin).
                if (args.Length < 3 || !_rewardManager.TryRollReward(args[2], out var drop) || drop is null ||
                    !_rewardManager.TryGrantReward(invoker, drop, target?.Guid ?? 0))
                {
                    ChatHelper.SendSystemMessage(invoker, "Use: !re combat loot <Rewards.json table key>");
                    return false;
                }
                break;

            case "level":
                // !re combat level <1-20> [xp]: set the active job's level directly.
                if (!_rewardManager.TrySetJobLevel(invoker, invoker.ActiveProfileId, Arg(2, 1), Arg(3, 0)))
                    return false;
                break;

            case "pxp":
                // !re combat pxp <rank> <pct> [a] [b] [c]: raw ClientUpdatePacketUpdateProfileExperience.
                invoker.SendTunneled(new ClientUpdatePacketUpdateProfileExperience
                {
                    ProfileId = invoker.ActiveProfileId,
                    Rank = Arg(2, 1),
                    RankPercent = Arg(3, 0),
                    StarsAvailable = Arg(4, 0),
                    StarsEarned = Arg(5, 0),
                    Unknown = Arg(6, 0)
                });
                break;

            case "prank":
                // !re combat prank <rank>: raw ClientUpdatePacketUpdateProfileRank (level-up fanfare when higher).
                invoker.SendTunneled(new ClientUpdatePacketUpdateProfileRank { ProfileId = invoker.ActiveProfileId, Rank = Arg(2, 2) });
                break;

            case "estate":
                // !re combat estate <state> [u1] [u2]: EncounterStatePacket.
                invoker.SendTunneled(new EncounterStatePacket { State = Arg(2, 3), Unknown = Arg(3, 0), Unknown2 = Arg(4, 0) });
                break;

            case "fight":
                invoker.SendTunneled(new EncounterPacketIsFighting { IsFighting = Arg(2, 1) != 0 });
                break;

            case "myhp":
                // !re combat myhp <current> <max>: the local player's own hit points.
                invoker.SendTunneled(new ClientUpdatePacketHitpoints { CurrentHitpoints = Arg(2, 500), MaxHitpoints = Arg(3, 1000) });
                break;

            case "hp":
                // !re combat hp <current> <max> [me]: an entity's nameplate health bar.
                {
                    var current = Arg(2, 500);
                    var max = System.Math.Max(1, Arg(3, 1000));
                    invoker.SendTunneled(new PlayerUpdatePacketUpdateHitpoints
                    {
                        Guid = Flag("me") ? invoker.Guid : target?.Guid ?? 0,
                        Percent = current * 100 / max,
                        MaxHitpoints = max,
                        CurrentHitpoints = current
                    });
                }
                break;

            case "hit":
                // !re combat hit <delta> [current] [max] [me]: the player hits the nearest npc
                // (with "me" the npc hits the player); shows the floating number.
                {
                    var me = Flag("me");
                    invoker.SendTunneled(new PlayerUpdatePacketHitPointModification
                    {
                        AttackerGuid = me ? target?.Guid ?? 0 : invoker.Guid,
                        TargetGuid = me ? invoker.Guid : target?.Guid ?? 0,
                        ShowDelta = true,
                        Delta = Arg(2, -100),
                        CurrentHitpoints = Arg(3, 500),
                        MaxHitpoints = Arg(4, 1000)
                    });
                    break;
                }

            case "cast":
                // !re combat cast <animation> [effect] [castSeconds] [bar] [npc]: StartCasting
                // (player -> nearest npc, or the npc -> player with "npc").
                {
                    var npcCasts = Flag("npc");
                    invoker.SendTunneled(new AbilityPacketStartCasting
                    {
                        CasterGuid = npcCasts ? target?.Guid ?? 0 : invoker.Guid,
                        Unused = npcCasts ? invoker.Guid : target?.Guid ?? 0,
                        Animation = Arg(2, 1099),
                        CompositeEffectId = Arg(3, 0),
                        ActionTime = FloatArg(4, 0f),
                        HasActionProgress = Arg(5, 0) != 0
                    });
                    break;
                }

            case "state":
                // !re combat state <n> [me]: PlayerUpdatePacketUpdateCharacterState.
                invoker.SendTunneled(new PlayerUpdatePacketUpdateCharacterState
                {
                    Guid = Flag("me") ? invoker.Guid : target?.Guid ?? 0,
                    State = Arg(2, 1)
                });
                break;

            case "look":
                // !re combat look [distance] [degrees]: stand <distance> from the target, facing it (camera follows).
                {
                    if (target is null)
                        return false;

                    var distance = FloatArg(2, 6f);
                    var dx = invoker.Position.X - target.Position.X;
                    var dz = invoker.Position.Z - target.Position.Z;
                    var length = System.MathF.Max(0.01f, System.MathF.Sqrt(dx * dx + dz * dz));
                    var position = target.Position with
                    {
                        X = target.Position.X + dx / length * distance,
                        Y = target.Position.Y + 0.5f,
                        Z = target.Position.Z + dz / length * distance,
                        W = invoker.Position.W
                    };
                    // Same yaw convention as PathFollower: facing (dx, dz) = atan2(dx, dz).
                    var heading = System.MathF.Atan2(-dx, -dz) + FloatArg(3, 0f) * System.MathF.PI / 180f;
                    var rotation = System.Numerics.Quaternion.CreateFromYawPitchRoll(heading, 0f, 0f);

                    invoker.UpdatePosition(position, rotation, false);
                    invoker.SendTunneled(new ClientUpdatePacketUpdateLocation { Position = position, Rotation = rotation, Teleport = true });
                    break;
                }

            case "enemy":
                // !re combat enemy [disposition] [healthbar]: re-send the nearest npc as hostile
                // (Disposition 0 = hostile, 1 = neutral, 2 = ally) with a hit point bar.
                if (target is null)
                    return false;

                target.Disposition = Arg(2, 0);
                target.ShowHealthBar = Arg(3, 1) != 0;
                invoker.OnRemoveVisibleNpcs(target);
                invoker.OnAddVisibleNpcs(target);
                break;

            case "fx":
                // !re combat fx <compositeEffect> [me]: a composite effect on the target (hit sparks etc.).
                invoker.SendTunneled(new PlayerUpdatePacketPlayCompositeEffect
                {
                    Guid = Flag("me") ? invoker.Guid : target?.Guid ?? 0,
                    CompositeEffectId = Arg(2, 7)
                });
                break;

            default:
                ChatHelper.SendSystemMessage(invoker, "Use: !re combat ow [0|1] | fight [0|1] | myhp <cur> <max> | hp <cur> <max> [u] [me] | " +
                    "hit <delta> [b3] [u5] [u6] [b7] [me] | cast <anim> [effect] [secs] [bar] [npc] | state <n> [me] | fx <effect> [me]");
                return false;
        }

        ChatHelper.SendSystemMessage(invoker, $"combat {string.Join(' ', args.Skip(1))} -> {target?.Name ?? "no npc"} ({target?.Guid})");
        return true;
    }

    private static Entities.Npc? NearestNpc(Player player)
    {
        return player.VisibleNpcs.Values
            .OrderBy(x => System.Numerics.Vector4.DistanceSquared(x.Position, player.Position))
            .FirstOrDefault();
    }

    /// <summary>The test quest, also sent when its offer is accepted.</summary>
    public static QuestAddPacket CreateTestQuestAdd()
    {
        var packet = new QuestAddPacket { Unknown = true };
        var quest = packet.Quest;
        quest.QuestId = TestQuestId;
        quest.NameId = 100;            // "Hello World!"
        quest.DescriptionId = 1000;    // "Learn about Collections."
        quest.HelperTextId = 426635;   // "The Welcome Screen displays updates..."
        quest.Objectives.Add(new Sanctuary.Packet.Common.QuestObjectiveData
        {
            ObjectiveId = TestObjectiveId,
            NameId = 419030,           // "What I've Caught"
            DescriptionId = 3742,      // "Unlocked Ability"
            Status = 1,                // active: name shows Count/Total
            Count = 0,
            Total = TestObjectiveTotal,
            Unknown8 = 442222,         // "Achievement Points"
            Unknown10 = 96             // "Trophies"
        });
        return packet;
    }

    /// <summary>
    /// Walks a test quest through its lifecycle:
    /// [offer -> accept] or add -> progress &lt;n&gt; ... -> objdone -> done.
    /// </summary>
    private bool HandleQuest(Player invoker, string[] args)
    {
        var step = args.Length > 1 ? args[1].ToLowerInvariant() : "add";
        switch (step)
        {
            case "add":
                invoker.SendTunneled(CreateTestQuestAdd());
                break;

            // Stand-ins for the client's clicks, so a real quest can be played without input:
            // talk [name] (click the nearest npc), accept <id> (offer window "Yes"), turnin <id> (turn-in window).
            case "talk":
                {
                    var name = args.Length > 2 ? string.Join(' ', args.Skip(2)) : null;
                    var npc = invoker.VisibleNpcs.Values
                        .Where(x => x is not (QuestObject or CollectionNode or CollectionCoin or Mount) &&
                                    (name is null || (x.Name?.Contains(name, System.StringComparison.OrdinalIgnoreCase) ?? false)))
                        .OrderBy(x => System.Numerics.Vector4.DistanceSquared(x.Position, invoker.Position))
                        .FirstOrDefault();
                    if (npc is null || !_questManager.TryInteract(invoker, npc))
                    {
                        ChatHelper.SendSystemMessage(invoker, $"No quest interaction with {npc?.Name ?? "a nearby npc"}.");
                        return false;
                    }
                    return true;
                }

            case "accept":
                _questManager.OnOfferReply(invoker, args.Length > 2 && int.TryParse(args[2], out var acceptId) ? acceptId : invoker.PendingQuestOfferId, true);
                return true;

            case "turnin":
                _questManager.OnTurnInReply(invoker, args.Length > 2 && int.TryParse(args[2], out var turnInId) ? turnInId : invoker.PendingQuestTurnInId);
                return true;

            case "offer":
                {
                    // Opens the quest start screen. Each unidentified field carries a distinct
                    // known string id so the screen (and RE/dev/client/DevTrace.log) shows what
                    // it is. Accept/Decline comes back as QuestReplyPacket (QuestReplyPacketHandler).
                    var packet = new QuestInfoPacket
                    {
                        QuestId = TestQuestId,
                        IntroTextId = 100,     // "Hello World!"
                        Unknown2 = 1000,       // "Learn about Collections." (not shown as text)
                        NameId = 426635,       // "The Welcome Screen displays updates..."
                        DescriptionId = 3742,  // "Unlocked Ability"
                        Unknown5 = 419030,     // "What I've Caught" (not shown as text)
                        Unknown8 = 0.25f
                    };

                    FillTestReward(packet.Reward);

                    // Switches for the unidentified fields, e.g. "!re quest offer u2=1 b6".
                    foreach (var option in args[2..])
                    {
                        var parts = option.ToLowerInvariant().Split('=');
                        _ = int.TryParse(parts.Length > 1 ? parts[1] : "1", out var value);
                        switch (parts[0])
                        {
                            case "undeclinable": packet.Undeclinable = true; break;
                            case "b6": packet.MembersOnly = value != 0; break;
                            case "b9": packet.Unknown9 = value != 0; break;
                            case "u2": packet.Unknown2 = value; break;
                            case "u5": packet.Unknown5 = value; break;
                            case "npc":
                                {
                                    // Quest giver candidate: the nearest NPC the player can see.
                                    var npc = NearestNpc(invoker);
                                    if (npc is null)
                                    {
                                        ChatHelper.SendSystemMessage(invoker, "No NPC in view.");
                                        return false;
                                    }

                                    packet.QuestGiverGuid = npc.Guid;
                                    ChatHelper.SendSystemMessage(invoker, $"Offering from NPC {npc.Name ?? "?"} ({npc.Guid}).");
                                    break;
                                }
                            default:
                                ChatHelper.SendSystemMessage(invoker, "Offer options: undeclinable b6 b9 u2=<n> u5=<n> npc");
                                return false;
                        }
                    }

                    invoker.SendTunneled(packet);
                    break;
                }

            case "progress":
                {
                    if (args.Length < 3 || !int.TryParse(args[2], out var count))
                    {
                        ChatHelper.SendSystemMessage(invoker, $"Use: !re quest progress <0-{TestObjectiveTotal}> [silent]");
                        return false;
                    }

                    invoker.SendTunneled(new QuestObjectiveUpdatePacket
                    {
                        QuestId = TestQuestId,
                        ObjectiveId = TestObjectiveId,
                        Count = count,
                        CompletedPercentage = (float)count / TestObjectiveTotal,
                        Silent = args.Length > 3 && args[3].Equals("silent", System.StringComparison.OrdinalIgnoreCase)
                    });
                    break;
                }

            case "objdone":
                invoker.SendTunneled(new QuestObjectiveCompletePacket
                {
                    QuestId = TestQuestId,
                    ObjectiveId = TestObjectiveId,
                    CompletedPercentage = 1f
                });
                break;

            case "end":
                {
                    // Turn-in screen with the test reward; closing it comes back as
                    // QuestEndReplyPacket (QuestEndReplyPacketHandler), which completes the quest.
                    var packet = new QuestEndPacket
                    {
                        QuestId = TestQuestId,
                        EndTextId = 1000       // "Learn about Collections."
                    };
                    FillTestReward(packet.Reward);

                    if (args.Length > 2 && args[2].Equals("npc", System.StringComparison.OrdinalIgnoreCase))
                    {
                        var npc = NearestNpc(invoker);
                        if (npc is null)
                        {
                            ChatHelper.SendSystemMessage(invoker, "No NPC in view.");
                            return false;
                        }

                        packet.QuestGiverGuid = npc.Guid;
                    }

                    invoker.SendTunneled(packet);
                    break;
                }

            case "skip":
                if (!_questManager.CompleteCurrentGoal(invoker))
                {
                    ChatHelper.SendSystemMessage(invoker, "No quest goal to complete.");
                    return false;
                }

                return true;

            case "reset":
                ChatHelper.SendSystemMessage(invoker, $"Cleared {_questManager.ResetAll(invoker)} saved quest(s). Relog to clear the journal.");
                return true;

            case "goto":
                {
                    // Teleports next to the nearest npc whose name contains the text (quest targets, turn-ins).
                    var name = string.Join(' ', args.Skip(2));
                    var npc = name.Length == 0 ? null : invoker.Zone.Npcs
                        .Where(x => x.Name?.Contains(name, System.StringComparison.OrdinalIgnoreCase) ?? false)
                        .OrderBy(x => System.Numerics.Vector4.DistanceSquared(x.Position, invoker.Position))
                        .FirstOrDefault();
                    if (npc is null)
                    {
                        ChatHelper.SendSystemMessage(invoker, "Use: !re quest goto <npc name>");
                        return false;
                    }

                    var destination = npc.Position with { X = npc.Position.X + 2f, W = invoker.Position.W };
                    invoker.UpdatePosition(destination, invoker.Rotation, false);
                    invoker.SendTunneled(new ClientUpdatePacketUpdateLocation { Position = destination, Rotation = invoker.Rotation, Teleport = true });
                    ChatHelper.SendSystemMessage(invoker, $"{npc.Name} ({npc.Guid}).");
                    return true;
                }

            case "giver":
                {
                    // Teleports next to a quest's giver (QuestIds are listed in analysis/generated_quests.tsv).
                    if (args.Length < 3 || !int.TryParse(args[2], out var questId) ||
                        !_resourceManager.Quests.TryGetValue(questId, out var quest))
                    {
                        ChatHelper.SendSystemMessage(invoker, "Use: !re quest giver <questId>");
                        return false;
                    }

                    if (!invoker.Zone.TryGetNpc(quest.GiverGuid, out var giver))
                    {
                        ChatHelper.SendSystemMessage(invoker, $"Quest {questId}'s giver isn't in this zone.");
                        return false;
                    }

                    var destination = giver.Position with { X = giver.Position.X + 2f, W = invoker.Position.W };
                    invoker.UpdatePosition(destination, invoker.Rotation, false);
                    invoker.SendTunneled(new ClientUpdatePacketUpdateLocation
                    {
                        Position = destination,
                        Rotation = invoker.Rotation,
                        Teleport = true
                    });
                    ChatHelper.SendSystemMessage(invoker, $"{giver.Name}: quest {questId} ({quest.Goals.Count} goal(s)).");
                    return true;
                }

            case "obj":
                {
                    // Teleports next to the nearest quest object the current goal still needs.
                    var questObject = invoker.Zone.Npcs
                        .OfType<QuestObject>()
                        .Where(x => x.IsVisibleTo(invoker))
                        .OrderBy(x => System.Numerics.Vector4.DistanceSquared(x.Position, invoker.Position))
                        .FirstOrDefault();
                    if (questObject is null)
                    {
                        ChatHelper.SendSystemMessage(invoker, "No quest object for your current goals.");
                        return false;
                    }

                    var destination = questObject.Position with { X = questObject.Position.X + 2f, W = invoker.Position.W };
                    invoker.UpdatePosition(destination, invoker.Rotation, false);
                    invoker.SendTunneled(new ClientUpdatePacketUpdateLocation
                    {
                        Position = destination,
                        Rotation = invoker.Rotation,
                        Teleport = true
                    });
                    return true;
                }

            case "camera":
                // Releases a stuck quest giver close-up.
                invoker.SendTunneled(new CommandPacketQuestDialogDone());
                break;

            case "done":
                invoker.SendTunneled(new QuestCompletePacket { QuestId = TestQuestId });
                break;

            default:
                ChatHelper.SendSystemMessage(invoker, "Use: !re quest [goto <npc> | talk [name] | accept [id] | turnin [id] | offer [undeclinable] [b6] [b9] [u2=n] [u5=n] [npc] | add | progress <n> [silent] | objdone | end [npc] | done | camera | reset | skip | obj | giver <questId>]");
                return false;
        }

        ChatHelper.SendSystemMessage(invoker, $"Sent quest '{step}' test packet.");
        return true;
    }

    private static bool TryParseFactor(Player invoker, string text, out float value)
    {
        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value > 0f && value <= 20f)
            return true;

        ChatHelper.SendSystemMessage(invoker, "Factor must be a number between 0 and 20, e.g. 1.5.");
        return false;
    }
}
