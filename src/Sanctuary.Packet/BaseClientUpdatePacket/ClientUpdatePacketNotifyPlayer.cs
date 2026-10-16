using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Gets the player's attention: flashes the game window/taskbar entry.
/// Client: ClientUpdatePacketNotifyPlayer (38.23), read layout i16 i16 string.
/// </summary>
/// <remarks>
/// The string is a code string name, not display text. The client resolves it
/// through Resources/CodeStringMappings.txt (e.g. "JobLevelUp" -> string id 401560)
/// and passes the localized text to the Lua OnPrintConsole event, which writes to
/// the developer console (Main.wndConsole). That console only opens when
/// Client.EnableDebugConsole is set, which the shipped client never does, so in
/// normal play only the window flash is visible. Use a chat/system message for
/// text the player should read.
/// </remarks>
public class ClientUpdatePacketNotifyPlayer : BaseClientUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 23;

    public string CodeString = null!;

    public ClientUpdatePacketNotifyPlayer() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(CodeString);

        return writer.Buffer;
    }
}
