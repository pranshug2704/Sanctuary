using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Ends a quest offer / turn-in dialog: resets the camera from the quest giver close-up
/// (QuestInfoPacket / QuestEndPacket QuestGiverGuid) back to the normal mode and calls
/// QuestStartHandler:DismissEndScreen, which restores the HUD. Header only.
/// Client: CommandPacket handler FUN_00aa2560 case 0x1d -> FUN_00a99220 (class name not
/// in the client; named here after its effect). The client resets the camera itself on
/// Decline, but not on Accept or turn-in, so the server sends this after those replies.
/// </summary>
public class CommandPacketQuestDialogDone : BaseCommandPacket, ISerializablePacket
{
    public new const short OpCode = 29;

    public CommandPacketQuestDialogDone() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        base.Write(writer);

        return writer.Buffer;
    }
}
