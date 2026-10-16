using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Scales an actor's head ("big head" effect).
/// Client: PlayerUpdatePacketHeadInflationScale (35.30), read layout i16 i16 u64 f32.
/// </summary>
public class PlayerUpdatePacketHeadInflationScale : BasePlayerUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 30;

    public ulong Guid;

    public float Scale = 1.0f;

    public PlayerUpdatePacketHeadInflationScale() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(Guid);
        writer.Write(Scale);

        return writer.Buffer;
    }
}
