using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The player closing the turn-in screen of a <see cref="QuestEndPacket"/>.
/// Client: QuestEndReplyPacket (49.14).
/// </summary>
public class QuestEndReplyPacket : BaseQuestPacket, IDeserializable<QuestEndReplyPacket>
{
    public new const uint OpCode = 14;

    /// <summary>The turn-in's <see cref="QuestEndPacket.QuestId"/>.</summary>
    public int QuestId;

    public QuestEndReplyPacket() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out QuestEndReplyPacket value)
    {
        value = new QuestEndReplyPacket();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.QuestId))
            return false;

        return reader.RemainingLength == 0;
    }
}
