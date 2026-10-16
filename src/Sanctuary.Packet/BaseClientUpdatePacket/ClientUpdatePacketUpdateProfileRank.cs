using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Sets one job's level. A higher rank than the client has plays the job level-up
/// notification (Lua NotificationHandler:JobLevelUp, code string "JobLevelUp").
/// Client: ClientUpdatePacketUpdateProfileRank (38.18).
/// </summary>
/// <remarks>
/// The notification only animates for profiles whose BadgeImageSet is one of the
/// job badge sets listed in notification.lua (10, 11, 12, 13, 19, 20, 32, ...).
/// </remarks>
public class ClientUpdatePacketUpdateProfileRank : BaseClientUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 18;

    public int ProfileId;
    public int Rank;

    public ClientUpdatePacketUpdateProfileRank() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(ProfileId);
        writer.Write(Rank);

        return writer.Buffer;
    }
}
