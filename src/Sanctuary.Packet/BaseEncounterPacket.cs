using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

public class BaseEncounterPacket
{
    public const short OpCode = 41;

    private short SubOpCode;

    public int Unknown;
    public int Unknown2;

    public BaseEncounterPacket(short subOpCode)
    {
        SubOpCode = subOpCode;
    }

    public void Write(PacketWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(SubOpCode);

        // Every encounter packet starts with these two (client schema "i16 i16 u32 u32 ...").
        writer.Write(Unknown);
        writer.Write(Unknown2);
    }
}