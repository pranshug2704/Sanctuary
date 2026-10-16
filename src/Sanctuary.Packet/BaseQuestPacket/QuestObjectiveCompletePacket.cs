using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Completes an objective (status 2) and sets the quest's completed percentage.
/// Client: QuestObjectiveCompletePacket (49.10), reader FUN_00c7b650, applied by FUN_00c7aa10,
/// notified by FUN_00a936e0.
/// </summary>
public class QuestObjectiveCompletePacket : BaseQuestPacket, ISerializablePacket
{
    public new const uint OpCode = 10;

    public int QuestId;
    public int ObjectiveId;
    public float CompletedPercentage;

    /// <summary>Looked up in a client table when non-zero (FUN_007c4710), then FUN_00969590.</summary>
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
