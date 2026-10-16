using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Game.ChatCommands;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class QuestEndReplyPacketHandler
{
    private static ILogger _logger = null!;
    private static IQuestManager _questManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(QuestEndReplyPacketHandler));
        _questManager = serviceProvider.GetRequiredService<IQuestManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!QuestEndReplyPacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}. ( Data: {data} )", nameof(QuestEndReplyPacket), Convert.ToHexString(data));
            return false;
        }

        _logger.LogInformation("{name}: quest {questId} turned in.", nameof(QuestEndReplyPacket), packet.QuestId);

        if (packet.QuestId != ReverseEngineeringChatCommand.TestQuestId)
        {
            _questManager.OnTurnInReply(connection.Player, packet.QuestId);
            return true;
        }

        // The `!re quest end` test quest.
        if (packet.QuestId == ReverseEngineeringChatCommand.TestQuestId)
        {
            // The client keeps the quest giver close-up after turn-in; release it.
            connection.Player.SendTunneled(new CommandPacketQuestDialogDone());
            connection.Player.SendTunneled(new QuestCompletePacket { QuestId = packet.QuestId });
        }

        return true;
    }
}
