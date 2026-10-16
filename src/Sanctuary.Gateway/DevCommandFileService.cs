using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;

namespace Sanctuary.Gateway;

/// <summary>
/// Local test automation: when SANCTUARY_DEV_COMMANDS names a file, chat commands written to it
/// (one per line, e.g. "!re combat hp 300 1000") run as the first logged-in player, as if typed
/// in game. The file is emptied after each read. Off unless the variable is set.
/// </summary>
public sealed class DevCommandFileService : BackgroundService
{
    private readonly ILogger _logger;
    private readonly IZoneManager _zoneManager;
    private readonly IChatCommandManager _chatCommandManager;
    private readonly string? _path = Environment.GetEnvironmentVariable("SANCTUARY_DEV_COMMANDS");

    public DevCommandFileService(ILogger<DevCommandFileService> logger, IZoneManager zoneManager,
        IChatCommandManager chatCommandManager)
    {
        _logger = logger;
        _zoneManager = zoneManager;
        _chatCommandManager = chatCommandManager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_path))
            return;

        _logger.LogWarning("Dev command file enabled: {path}", _path);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                RunPending();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dev command file failed.");
            }

            await Task.Delay(250, stoppingToken);
        }
    }

    private void RunPending()
    {
        if (!File.Exists(_path) || new FileInfo(_path).Length == 0)
            return;

        var player = _zoneManager.Zones.SelectMany(x => x.Players).FirstOrDefault();
        if (player is null)
            return;

        var lines = File.ReadAllLines(_path);
        File.WriteAllText(_path, string.Empty);

        foreach (var line in lines.Select(x => x.Trim()).Where(x => x.Length > 0))
        {
            var handled = _chatCommandManager.TryHandle(player, line);
            _logger.LogInformation("Dev command \"{command}\" as {player}: {result}", line, player.Guid, handled ? "handled" : "unknown command");
        }
    }
}
