using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Removes a quest from the journal and, unless <see cref="Silent"/>, shows the
/// "Quest Abandoned" notice. Client: QuestAbandonedPacket (49.6).
/// </summary>
public class QuestAbandonedPacket : BaseQuestPacket, ISerializablePacket
{
    public new const uint OpCode = 6;

    public int QuestId;

    public bool Silent;

    public QuestAbandonedPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(QuestId);
        writer.Write(Silent);

        return writer.Buffer;
    }
}
