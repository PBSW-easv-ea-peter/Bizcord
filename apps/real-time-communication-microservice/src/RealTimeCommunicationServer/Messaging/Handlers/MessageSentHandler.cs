using Bizcord.Logging;
using RealTimeCommunicationServer.Contracts;

namespace RealTimeCommunicationServer.Messaging.Handlers;

/// <summary>Skal på sigt pushe beskeden til modtagerne. Logger kun id'er - aldrig Content (GDPR).</summary>
public class MessageSentHandler : IMessageHandler<MessageSent>
{
    private readonly ILogger<MessageSentHandler> _logger;

    public MessageSentHandler(
        ILogger<MessageSentHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(
        MessageSent message,
        CancellationToken cancellationToken)
    {
        _logger.Information(
            "MessageSent received.",
            new { message.MessageId, message.ChatId, RecipientCount = message.RecipientUserIds.Count });

        return Task.CompletedTask;
    }
}
