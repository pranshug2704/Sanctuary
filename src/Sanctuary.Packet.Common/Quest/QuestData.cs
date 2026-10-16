using System.Collections.Generic;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

/// <summary>
/// Quest record sent in QuestAddPacket.
/// </summary>
public sealed class QuestData : ISerializableType
{
    public int QuestId;
    public int NameId;             // string id; shown in the "Quest Started" toast
    public int DescriptionId;      // string id; journal description
    public int HelperTextId;       // string id; quest tracker text
    public bool MembersOnly;
    public ulong TimeStarted;
    public int JobId;              // (drives Job Badge/Icon/Level columns)
    /// <summary>No "Quest Started" toast on add, and no objective popups for this quest.</summary>
    public bool Quiet;
    public float CompletedPercentage;

    public QuestRewardData Reward { get; } = new();

    public List<QuestObjectiveData> Objectives { get; } = [];

    public int IconId;
    public bool SystemQuest;
    public bool Unknown5;

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
        // A map on the wire: key, then the objective (which repeats its id).
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
