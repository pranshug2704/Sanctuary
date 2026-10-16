using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Completes a quest: the client shows its "Quest Complete" notice and drops it from the journal.
/// Client: QuestCompletePacket (49.4).
/// </summary>
public class QuestCompletePacket : BaseQuestPacket, ISerializablePacket
{
    public new const uint OpCode = 4;

    public int QuestId;

    public QuestCompletePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(QuestId);

        return writer.Buffer;
    }
}
