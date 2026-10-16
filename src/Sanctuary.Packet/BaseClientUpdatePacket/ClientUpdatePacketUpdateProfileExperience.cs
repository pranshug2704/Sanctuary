using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// Updates one job's level and experience bar without the level-up fanfare.
/// Client: ClientUpdatePacketUpdateProfileExperience (38.14), read layout i16 i16 u32 x6.
/// </summary>
/// <remarks>
/// The handler (0x9392c0 case 0xe) finds the profile by id and stores the five values
/// in the same order as ClientPcProfile's Rank, RankPercent, StarsAvailable, StarsEarned
/// and Unknown20, then refreshes BaseClient.Profiles (LevelProgressBar, job browser).
/// A rank change here (other than to 1) refreshes the player's equipment, but plays
/// no fanfare; use ClientUpdatePacketUpdateProfileRank for that.
/// </remarks>
public class ClientUpdatePacketUpdateProfileExperience : BaseClientUpdatePacket, ISerializablePacket
{
    public new const short OpCode = 14;

    public int ProfileId;
    public int Rank;

    /// <summary>Progress through the current rank, 0-100.</summary>
    public int RankPercent;

    public int StarsAvailable;
    public int StarsEarned;
    public int Unknown;

    public ClientUpdatePacketUpdateProfileExperience() : base(OpCode)
    {
    }

    public byte[] Serialize()
    {
        using var writer = new PacketWriter();

        Write(writer);

        writer.Write(ProfileId);
        writer.Write(Rank);
        writer.Write(RankPercent);
        writer.Write(StarsAvailable);
        writer.Write(StarsEarned);
        writer.Write(Unknown);

        return writer.Buffer;
    }
}
