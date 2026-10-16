using System.ComponentModel.DataAnnotations;

namespace Sanctuary.Core.Configuration;

public sealed class LoginServerOptions : ServerOptions
{
    [Required]
    public required int LoginGatewayPort { get; set; }

    [Required]
    public required string LoginGatewayChallenge { get; set; }

    /// <summary>
    /// Locks the server and only allows admins to login.
    /// </summary>
    public bool IsLocked { get; set; }

    public int DefaultTitleId { get; set; }

    [Required]
    public required int DefaultProfileId { get; set; }

    /// <summary>
    /// Level (rank 1-20) new characters' jobs start at. Experience from quests and
    /// enemies levels up jobs below 20.
    /// </summary>
    public int StartingJobLevel { get; set; } = 20;

    public int StartingCoins { get; set; }
    public int StartingStationCash { get; set; }

    public bool UnlockAllTitles { get; set; }
    public bool UnlockAllProfiles { get; set; }
}