using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>Encounter state (handler FUN_00aa36c0, sub-opcode 0x6a).</summary>
public class EncounterStatePacket : BaseEncounterPacket, ISerializablePacket
{
    public new const short OpCode = 106;

    public int State;

    public EncounterStatePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);
        writer.Write(State);

        return writer.Buffer;
    }
}
