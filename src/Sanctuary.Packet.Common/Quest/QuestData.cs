using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

/// <summary>
/// Quest record sent in QuestAddPacket (client reader FUN_0093c840).
/// </summary>
/// <remarks>
/// Wire layout measured with the emulator harness. Struct offsets in comments are where the
/// client stores each field; the quest journal datasource (BaseClient.QuestData) reads
/// Members Only from +0x10, Time Started +0x18, Completed Percentage +0x28, Objective Count
/// from the list at +0xe8, Icon Id +0x150 and System Quest +0x154. Quest Title/Description/
/// Helper Text are not on the wire and are presumably resolved from the string ids.
/// </remarks>
public sealed class QuestData : ISerializableType
{
    public int QuestId;            // +0x00
    public int NameId;             // +0x04  string id; shown in the "Quest Started" toast (confirmed in game)
    public int DescriptionId;      // +0x08  string id; journal description (confirmed in game)
    public int HelperTextId;       // +0x0c  string id; quest tracker text (confirmed in game)
    public bool MembersOnly;       // +0x10
    public ulong TimeStarted;      // +0x18
    public int JobId;              // +0x20  (drives Job Badge/Icon/Level columns)
    /// <summary>
    /// No "Quest Started" toast on add, and no objective popups for this quest (the client ORs it
    /// into the objective notifiers' silent flag). Client +0x24; read by FUN_00a92680 / FUN_00a931b0.
    /// </summary>
    public bool Quiet;             // +0x24
    public float CompletedPercentage; // +0x28

    public QuestRewardData Reward { get; } = new();

    public List<QuestObjectiveData> Objectives { get; } = [];

    public int IconId;             // +0x150
    public bool SystemQuest;       // +0x154
    public bool Unknown5;          // +0x11

    public void Serialize(PacketWriter writer)
    {
        writer.Write(QuestId);
        writer.Write(NameId);
        writer.Write(DescriptionId);
        writer.Write(HelperTextId);
        writer.Write(MembersOnly);
        writer.Write(TimeStarted);
        writer.Write(JobId);
        writer.Write(Quiet);
        writer.Write(CompletedPercentage);

        Reward.Serialize(writer);

        writer.Write(Objectives.Count);
        // A map on the wire: key, then the objective (which repeats its id). The client files
        // objectives under the id inside the record (confirmed live with harness/trace_quest.py).
        foreach (var objective in Objectives)
        {
            writer.Write(objective.ObjectiveId);
            objective.Serialize(writer);
        }

        writer.Write(IconId);
        writer.Write(SystemQuest);
        writer.Write(Unknown5);
    }
}
