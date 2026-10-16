using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Updates an objective's count and the quest's completed percentage.
/// Client: QuestObjectiveUpdatePacket (49.9), reader FUN_00c7b5e0, applied by FUN_00c7a990;
/// unless Silent, FUN_00a931b0 shows the "UpdateObjective" popup and a chat message.
/// </summary>
public class QuestObjectiveUpdatePacket : BaseQuestPacket, ISerializablePacket
{
    public new const uint OpCode = 9;

    public int QuestId;
    public int ObjectiveId;
    public int Count;
    public float CompletedPercentage;

    /// <summary>Skips the popup and chat message (also skipped for quests with QuestData.Unknown4).</summary>
    public bool Silent;

    /// <summary>Changes the popup for some values (1, 3, 10, 11, 30, 40, 51).</summary>
    public int NotifyType;

    public QuestObjectiveUpdatePacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(QuestId);
        writer.Write(ObjectiveId);
        writer.Write(Count);
        writer.Write(CompletedPercentage);
        writer.Write(Silent);
        writer.Write(NotifyType);

        return writer.Buffer;
    }
}
