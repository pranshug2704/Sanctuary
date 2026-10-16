using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Completes an objective (status 2) and sets the quest's completed percentage.
/// Client: QuestObjectiveCompletePacket (49.10).
/// </summary>
public class QuestObjectiveCompletePacket : BaseQuestPacket, ISerializablePacket
{
    public new const uint OpCode = 10;

    public int QuestId;
    public int ObjectiveId;
    public float CompletedPercentage;

    public int Unknown;

    /// <summary>Same role as <see cref="QuestObjectiveUpdatePacket.Silent"/>.</summary>
    public bool Silent;

    public QuestObjectiveCompletePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(QuestId);
        writer.Write(ObjectiveId);
        writer.Write(CompletedPercentage);
        writer.Write(Unknown);
        writer.Write(Silent);

        return writer.Buffer;
    }
}
