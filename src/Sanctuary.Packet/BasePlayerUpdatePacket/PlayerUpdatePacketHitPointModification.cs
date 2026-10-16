using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// A hit landing (FUN_0092f460 case 0x23 -> FUN_008d3020), confirmed in game:
/// the target's hit points become CurrentHitpoints / MaxHitpoints (the local player's combat dock,
/// or an npc's nameplate bar), and with ShowDelta the client floats Delta over the target when the
/// local player is the attacker or the target - yellow for damage dealt, red for damage taken.
/// Floating numbers need overworld combat on (EncounterOverworldCombatPacket, client +0x80).
/// </summary>
public class PlayerUpdatePacketHitPointModification : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 35;

    public ulong AttackerGuid;
    public ulong TargetGuid;
    public bool ShowDelta;
    public int MaxHitpoints;
    public int CurrentHitpoints;
    public int Delta;
    public bool Unknown7;

    public PlayerUpdatePacketHitPointModification() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);
        writer.Write(AttackerGuid);
        writer.Write(TargetGuid);
        writer.Write(ShowDelta);
        writer.Write(MaxHitpoints);
        writer.Write(CurrentHitpoints);
        writer.Write(Delta);
        writer.Write(Unknown7);

        return writer.Buffer;
    }
}
