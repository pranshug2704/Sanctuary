using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Quest packet family. Unlike most families the sub-opcode is 32-bit:
/// header is i16 OpCode, u32 SubOpCode (both directions).
/// </summary>
public class BaseQuestPacket
{
    public const short OpCode = 49;

    private uint SubOpCode;

    public BaseQuestPacket(uint subOpCode)
    {
        SubOpCode = subOpCode;
    }

    public void Write(PacketWriter writer)
    {
        writer.Write(OpCode);
        writer.Write(SubOpCode);
    }

    public bool TryRead(ref PacketReader reader)
    {
        if (!reader.TryRead(out short opCode) || opCode != OpCode)
            return false;

        if (!reader.TryRead(out uint subOpCode) || subOpCode != SubOpCode)
            return false;

        return true;
    }
}
