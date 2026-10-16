using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Game;
using Sanctuary.Game.ChatCommands;
using Sanctuary.Game.Helpers;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class QuestReplyPacketHandler
{
    private static ILogger _logger = null!;
    private static IQuestManager _questManager = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(QuestReplyPacketHandler));
        _questManager = serviceProvider.GetRequiredService<IQuestManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!QuestReplyPacket.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}. ( Data: {data} )", nameof(QuestReplyPacket), Convert.ToHexString(data));
            return false;
        }

        _logger.LogInformation("{name}: quest {questId} {reply}.", nameof(QuestReplyPacket), packet.QuestId, packet.Accepted ? "accepted" : "declined");

        if (packet.QuestId != ReverseEngineeringChatCommand.TestQuestId)
        {
            _questManager.OnOfferReply(connection.Player, packet.QuestId, packet.Accepted);
            return true;
        }

        // The `!re quest offer` test quest.

        if (packet.Accepted)
        {
            // The client keeps the quest giver close-up after Accept; release it.
            connection.Player.SendTunneled(new CommandPacketQuestDialogDone());
            connection.Player.SendTunneled(ReverseEngineeringChatCommand.CreateTestQuestAdd());
        }
        else
            ChatHelper.SendSystemMessage(connection.Player, "Test quest declined.");

        return true;
    }
}
