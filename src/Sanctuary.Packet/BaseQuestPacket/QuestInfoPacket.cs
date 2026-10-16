using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

/// <summary>
/// Offers a quest: the client opens the quest start screen (QuestHandler:ShowStartScreen)
/// with Accept/Decline, answered by <see cref="QuestReplyPacket"/> carrying <see cref="QuestId"/>.
/// Client: QuestInfoPacket (49.1), reader FUN_00c7b7a0, shown by FUN_00a94be0.
/// </summary>
/// <remarks>Offsets are where the client stores each field in the packet object.</remarks>
public class QuestInfoPacket : BaseQuestPacket, ISerializablePacket
{
    public new const uint OpCode = 1;

    /// <summary>Echoed back in <see cref="QuestReplyPacket"/> (+0x0c, kept at offer +0x1c).</summary>
    public int QuestId;            // +0x0c

    public int IntroTextId;        // +0x10  string id; start screen NPC dialog (confirmed in game)
    public int Unknown2;           // +0x14
    public int NameId;             // +0x18  string id; start screen quest name (confirmed in game)
    public int DescriptionId;      // +0x1c  string id; start screen description (confirmed in game)
    public int Unknown5;           // +0x20
    public bool MembersOnly;       // +0x24  start screen members-only flag (confirmed in game)

    public QuestRewardData Reward { get; } = new();

    /// <summary>
    /// Quest giver: the camera frames this NPC while the offer is open and its name is used in
    /// the offer's chat line (looked up by FUN_00984960). Confirmed in game. 0 = no giver.
    /// </summary>
    public ulong QuestGiverGuid;   // +0xe0  (kept with the offer at +0x20, cleared on decline)
    public float Unknown8;         // +0xe8
    public bool Unknown9;          // +0x25  (no visible effect on the start screen)
    /// <summary>Hides Decline (confirmed in game).</summary>
    public bool Undeclinable;      // +0xec

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
