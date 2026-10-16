using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

/// <summary>
/// Opens the quest turn-in screen (QuestHandler:ShowEndScreen) with the quest's rewards,
/// answered by <see cref="QuestEndReplyPacket"/> carrying <see cref="QuestId"/>.
/// Client: QuestEndPacket (49.13).
/// </summary>
public class QuestEndPacket : BaseQuestPacket, ISerializablePacket
{
    public new const uint OpCode = 13;

    /// <summary>Quest giver, framed by the camera ("HEAD") like the offer's.</summary>
    public ulong QuestGiverGuid;

    /// <summary>Echoed back in <see cref="QuestEndReplyPacket"/>.</summary>
    public int QuestId;

    /// <summary>String id of the giver's turn-in dialog.</summary>
    public int EndTextId;

    public int Unknown;

    public QuestRewardData Reward { get; } = new();

    public float Unknown2;

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
