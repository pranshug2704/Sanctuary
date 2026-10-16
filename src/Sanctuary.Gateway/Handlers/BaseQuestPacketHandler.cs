using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class BaseQuestPacketHandler
{
    private static ILogger _logger = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(BaseQuestPacketHandler));
    }

    public static bool HandlePacket(GatewayConnection connection, PacketReader reader)
    {
        // Quest packets carry a 32-bit sub-opcode.
        if (!reader.TryRead(out uint subOpCode))
        {
            _logger.LogError("Failed to read sub-opcode from packet. ( Data: {data} )", Convert.ToHexString(reader.Span));
            return false;
        }

        var handled = subOpCode switch
        {
            QuestReplyPacket.OpCode => QuestReplyPacketHandler.HandlePacket(connection, reader.Span),
            QuestEndReplyPacket.OpCode => QuestEndReplyPacketHandler.HandlePacket(connection, reader.Span),
            _ => false
        };

        if (!handled)
            _logger.LogWarning("Unhandled quest packet {subOpCode}. ( Data: {data} )", subOpCode, Convert.ToHexString(reader.Span));

        return handled;
    }
}
