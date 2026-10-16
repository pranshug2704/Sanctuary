using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

/// <summary>
/// Opens the quest turn-in screen (QuestHandler:ShowEndScreen) with the quest's rewards,
/// answered by <see cref="QuestEndReplyPacket"/> carrying <see cref="QuestId"/>.
/// Client: QuestEndPacket (49.13), reader FUN_00c7b990, shown by FUN_00a95420.
/// </summary>
/// <remarks>Offsets are where the client stores each field in the packet object.</remarks>
public class QuestEndPacket : BaseQuestPacket, ISerializablePacket
{
    public new const uint OpCode = 13;

    /// <summary>Quest giver, framed by the camera ("HEAD") like the offer's.</summary>
    public ulong QuestGiverGuid;   // +0x10

    /// <summary>Echoed back in <see cref="QuestEndReplyPacket"/> (kept at +0x1c).</summary>
    public int QuestId;            // +0x18

    /// <summary>String id of the giver's turn-in dialog.</summary>
    public int EndTextId;          // +0x1c

    /// <summary>Used by FUN_0096c780 when greater than 0.</summary>
    public int Unknown;            // +0x20

    public QuestRewardData Reward { get; } = new();

    public float Unknown2;         // +0xe0

    public QuestEndPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(QuestGiverGuid);
        writer.Write(QuestId);
        writer.Write(EndTextId);
        writer.Write(Unknown);

        Reward.Serialize(writer);

        writer.Write(Unknown2);

        return writer.Buffer;
    }
}
