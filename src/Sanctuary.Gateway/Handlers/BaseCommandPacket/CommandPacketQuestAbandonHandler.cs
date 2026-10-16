using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class CommandPacketQuestAbandonHandler
{
    private static ILogger _logger = null!;
    private static IQuestManager _questManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(CommandPacketQuestAbandonHandler));
        _questManager = serviceProvider.GetRequiredService<IQuestManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!CommandPacketQuestAbandon.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}. ( Data: {data} )", nameof(CommandPacketQuestAbandon), Convert.ToHexString(data));
            return false;
        }

        _questManager.Abandon(connection.Player, packet.QuestId);
        return true;
    }
}
