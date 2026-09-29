using Bizcord.Logging;
using RealTimeCommunicationServer.Contracts;

namespace RealTimeCommunicationServer.Messaging.Handlers;

public class MessagesSeenHandler : IMessageHandler<MessagesSeen>
{
    private readonly ILogger<MessagesSeenHandler> _logger;

    public MessagesSeenHandler(
        ILogger<MessagesSeenHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(
        MessagesSeen message,
        CancellationToken cancellationToken)
    {
        _logger.Information(
            "MessagesSeen received.",
            new { message.ChatId, message.UserId, message.UpToMessageId });

        return Task.CompletedTask;
    }
}
