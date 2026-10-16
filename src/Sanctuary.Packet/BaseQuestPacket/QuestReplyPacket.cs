using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The player's answer to a <see cref="QuestInfoPacket"/> offer.
/// Client: QuestReplyPacket (49.2), built by FUN_00a86400 from QuestScreen.AcceptQuest
/// (FUN_00a97d70, accepted = true) / QuestScreen.DeclineQuest (FUN_00a97e10), written by FUN_00a91630.
/// </summary>
public class QuestReplyPacket : BaseQuestPacket, IDeserializable<QuestReplyPacket>
{
    public new const uint OpCode = 2;

    /// <summary>The offer's <see cref="QuestInfoPacket.QuestId"/>.</summary>
    public int QuestId;

    public bool Accepted;

    public QuestReplyPacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out QuestReplyPacket value)
    {
        value = new QuestReplyPacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.QuestId))
            return false;

        if (!reader.TryRead(out value.Accepted))
            return false;

        return reader.RemainingLength == 0;
    }
}
