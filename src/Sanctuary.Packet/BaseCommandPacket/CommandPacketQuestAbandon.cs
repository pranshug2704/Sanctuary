using System;

using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// The player abandoning a quest from the journal. Answered with <see cref="QuestAbandonedPacket"/>.
/// </summary>
public class CommandPacketQuestAbandon : BaseCommandPacket, IDeserializable<CommandPacketQuestAbandon>
{
    public new const short OpCode = 23;

    public int QuestId;

    public CommandPacketQuestAbandon() : base(OpCode)
    {
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> data, out CommandPacketQuestAbandon value)
    {
        value = new CommandPacketQuestAbandon();

        var reader = new PacketReader(data);

        if (!value.TryRead(ref reader))
            return false;

        if (!reader.TryRead(out value.QuestId))
            return false;

        return reader.RemainingLength == 0;
    }
}
