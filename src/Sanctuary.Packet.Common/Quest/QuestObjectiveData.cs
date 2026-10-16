using Sanctuary.Core.IO;

namespace Sanctuary.Packet.Common;

/// <summary>
/// One objective inside <see cref="QuestData"/> (client reader FUN_008fd790, 103 bytes
/// when the reward has no entries; 107 with the map key). Layout measured with the emulator harness
/// (harness/probe_quest_offsets.py); offsets in comments are where the client stores each
/// field in its objective node. The journal's objective datasource (FUN_00cf7fd0) shows
/// Name from +0x04 and Description from +0x08; the Name is suffixed by the status
/// formatter FUN_00a8b9a0 using +0xd0/+0xd4/+0xd8.
/// </summary>
public sealed class QuestObjectiveData : ISerializableType
{
    /// <summary>
    /// Objective id (+0x00, copied to the lookup key +0xf0 used by the Objective* packets).
    /// QuestData also writes it as the key of its objective map.
    /// </summary>
    public int ObjectiveId;        // +0x00
    public int NameId;             // +0x04  string id; journal objective name (confirmed in game)
    public int DescriptionId;      // +0x08  string id; journal line under the name (confirmed in game)
    public bool Unknown4;

    public QuestRewardData Reward { get; } = new();

    /// <summary>
    /// 1 = active: the name is followed by Count/Total; 2 = complete. Set by
    /// QuestObjectiveActivated (1) and QuestObjectiveComplete (2).
    /// </summary>
    public int Status;             // +0xd0
    public int Count;              // +0xd4
    public int Total;              // +0xd8
    public int Unknown8;           // +0xdc
    public bool Unknown9;
    public int Unknown10;          // +0x0c

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
