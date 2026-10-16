using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Removes a quest from the journal (FUN_00c7c1f0) and, unless <see cref="Silent"/>, shows the
/// "Quest Abandoned" notice (FUN_00a92cd0). Client: QuestAbandonedPacket (49.6), reader FUN_00c7b500.
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
