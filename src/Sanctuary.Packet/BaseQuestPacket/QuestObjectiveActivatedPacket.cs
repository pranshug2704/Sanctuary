using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Activates an objective of a journal quest (status 1) and sets its total, so the journal
/// shows its progress as count/total.
/// Client: QuestObjectiveActivatedPacket (49.8).
/// </summary>
public class QuestObjectiveActivatedPacket : BaseQuestPacket, ISerializablePacket
{
    public new const uint OpCode = 8;

    public int QuestId;
    public int ObjectiveId;
    public int Total;

    public bool Unknown;

    public QuestObjectiveActivatedPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(QuestId);
        writer.Write(ObjectiveId);
        writer.Write(Total);
        writer.Write(Unknown);

        return writer.Buffer;
    }
}
