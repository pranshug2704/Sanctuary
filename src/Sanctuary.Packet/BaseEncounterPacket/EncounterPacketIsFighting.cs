using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>Tells the client whether the player is in combat.</summary>
public class EncounterPacketIsFighting : BaseEncounterPacket, ISerializablePacket
{
    public new const short OpCode = 133;

    public bool IsFighting;

    public EncounterPacketIsFighting() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);
        writer.Write(IsFighting);

        return writer.Buffer;
    }
}
