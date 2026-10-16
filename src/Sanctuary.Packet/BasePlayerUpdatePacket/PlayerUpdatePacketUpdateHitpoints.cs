using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Hit points of another entity (FUN_0092f460 case 5; ignored for the local player, who gets
/// ClientUpdatePacketHitpoints). Raises a PlayerHitpointEvent whose apply (FUN_008ba940) draws the
/// nameplate bar of npcs/other players from Percent (0-100, clamped), confirmed in game. Below 1
/// hit point the actor's nameplate pieces are hidden (death).
/// </summary>
public class PlayerUpdatePacketUpdateHitpoints : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 5;

    public ulong Guid;
    public int Percent;
    public int MaxHitpoints;
    public int CurrentHitpoints;

    public PlayerUpdatePacketUpdateHitpoints() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);
        writer.Write(Guid);
        writer.Write(Percent);
        writer.Write(MaxHitpoints);
        writer.Write(CurrentHitpoints);

        return writer.Buffer;
    }
}
