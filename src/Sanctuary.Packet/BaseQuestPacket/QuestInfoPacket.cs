using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

/// <summary>
/// Offers a quest: the client opens the quest start screen (QuestHandler:ShowStartScreen)
/// with Accept/Decline, answered by <see cref="QuestReplyPacket"/> carrying <see cref="QuestId"/>.
/// Client: QuestInfoPacket (49.1).
/// </summary>
public class QuestInfoPacket : BaseQuestPacket, ISerializablePacket
{
    public new const uint OpCode = 1;

    /// <summary>Echoed back in <see cref="QuestReplyPacket"/>.</summary>
    public int QuestId;

    public int IntroTextId;        // string id; start screen NPC dialog
    public int Unknown2;
    public int NameId;             // string id; start screen quest name
    public int DescriptionId;      // string id; start screen description
    public int Unknown5;
    public bool MembersOnly;       // start screen members-only flag

    public QuestRewardData Reward { get; } = new();

    /// <summary>
    /// Quest giver: the camera frames this NPC while the offer is open and its name is used in
    /// the offer's chat line. 0 = no giver.
    /// </summary>
    public ulong QuestGiverGuid;
    public float Unknown8;
    public bool Unknown9;          // no visible effect on the start screen
    /// <summary>Hides Decline.</summary>
    public bool Undeclinable;

    public QuestInfoPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(QuestId);
        writer.Write(IntroTextId);
        writer.Write(Unknown2);
        writer.Write(NameId);
        writer.Write(DescriptionId);
        writer.Write(Unknown5);
        writer.Write(MembersOnly);

        Reward.Serialize(writer);

        writer.Write(QuestGiverGuid);
        writer.Write(Unknown8);
        writer.Write(Unknown9);
        writer.Write(Undeclinable);

        return writer.Buffer;
    }
}
