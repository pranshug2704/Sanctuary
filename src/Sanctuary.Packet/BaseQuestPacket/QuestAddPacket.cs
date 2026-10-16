using Sanctuary.Core.IO;
using Sanctuary.Packet.Common;

namespace Sanctuary.Packet;

/// <summary>
/// Adds a quest to the player's quest journal.
/// Client: QuestAddPacket (49.3), reader FUN_00c7cc80 = QuestData + bool (stored at +0x168).
/// </summary>
public class QuestAddPacket : BaseQuestPacket, ISerializablePacket
{
    public new const uint OpCode = 3;

    public QuestData Quest { get; } = new();

    /// <summary>Also selects the quest in the on-screen tracker (FUN_00a8f2b0) when set.</summary>
    public bool Unknown;

    public QuestAddPacket() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        Quest.Serialize(writer);
        writer.Write(Unknown);

        return writer.Buffer;
    }
}
