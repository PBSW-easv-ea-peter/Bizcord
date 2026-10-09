using Bizcord.Logging;
using RealTimeCommunicationServer.Contracts;
using RealTimeCommunicationServer.Realtime;

namespace RealTimeCommunicationServer.Messaging.Handlers;

/// <summary>
/// Pushes the message to the recipients who are online and tells the rest of the system who received it
/// (rtc.message-delivered). Logs only ids - never Content (GDPR).
/// </summary>
public class MessageSentHandler : IMessageHandler<MessageSent>
{
    private readonly PresenceTracker _presence;
    private readonly IClientNotifier _notifier;
    private readonly IMessageClient _messageClient;
    private readonly TimeProvider _time;
    private readonly ILogger<MessageSentHandler> _logger;

    public MessageSentHandler(
        PresenceTracker presence,
        IClientNotifier notifier,
        IMessageClient messageClient,
        TimeProvider time,
        ILogger<MessageSentHandler> logger)
    {
        _presence = presence;
        _notifier = notifier;
        _messageClient = messageClient;
        _time = time;
        _logger = logger;
    }

    public async Task HandleAsync(
        MessageSent message,
        CancellationToken cancellationToken)
    {
        var online = _presence.OnlineAmong(message.RecipientUserIds);

        // Nobody to deliver to - offline recipients fetch the message via ChatService's REST API.
        if (online.Count == 0)
        {
            _logger.Information(
                "MessageSent received, no recipients online.",
                new { message.MessageId, message.ChatId, RecipientCount = message.RecipientUserIds.Count });
            return;
        }

        await _notifier.PushMessageAsync(
            online,
            new MessageReceived(message.MessageId, message.ChatId, message.SenderUserId, message.Content, message.SentAt),
            cancellationToken);

        await _messageClient.Publish(
            new MessageDelivered(message.MessageId, message.ChatId, online, _time.GetUtcNow()),
            cancellationToken);

        _logger.Information(
            "MessageSent pushed.",
            new { message.MessageId, message.ChatId, RecipientCount = message.RecipientUserIds.Count, DeliveredCount = online.Count });
    }
}
