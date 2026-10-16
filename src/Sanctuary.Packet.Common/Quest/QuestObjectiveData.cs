using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

/// <summary>
/// One objective inside <see cref="QuestData"/>.
/// </summary>
public sealed class QuestObjectiveData : ISerializableType
{
    /// <summary>
    /// Objective id.
    /// QuestData also writes it as the key of its objective map.
    /// </summary>
    public int ObjectiveId;
    public int NameId;             // string id; journal objective name
    public int DescriptionId;      // string id; journal line under the name
    public bool Unknown4;

    public QuestRewardData Reward { get; } = new();

    /// <summary>
    /// 1 = active: the name is followed by Count/Total; 2 = complete. Set by
    /// QuestObjectiveActivated (1) and QuestObjectiveComplete (2).
    /// </summary>
    public int Status;
    public int Count;
    public int Total;
    public int Unknown8;
    public bool Unknown9;
    public int Unknown10;

    public void Serialize(PacketWriter writer)
    {
        writer.Write(ObjectiveId);
        writer.Write(NameId);
        writer.Write(DescriptionId);
        writer.Write(Unknown4);

        Reward.Serialize(writer);

        writer.Write(Status);
        writer.Write(Count);
        writer.Write(Total);
        writer.Write(Unknown8);
        writer.Write(Unknown9);
        writer.Write(Unknown10);
    }
}
