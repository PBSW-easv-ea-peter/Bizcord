using Microsoft.Extensions.Logging.Abstractions;
using RealTimeCommunicationServer.Contracts;
using RealTimeCommunicationServer.Messaging.Handlers;
using RealTimeCommunicationServer.Realtime;

namespace RealTimeCommunicationServer.Tests;

/// <summary>Unit: the handler's logic with fakes - no SignalR, no RabbitMQ.</summary>
public class MessageSentHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    private readonly PresenceTracker _presence = new();
    private readonly FakeClientNotifier _notifier = new();
    private readonly FakeMessageClient _messageClient = new();
    private readonly MessageSentHandler _handler;

    private readonly Guid _alice = Guid.NewGuid();
    private readonly Guid _bob = Guid.NewGuid();
    private readonly Guid _carol = Guid.NewGuid();

    public MessageSentHandlerTests() =>
        _handler = new MessageSentHandler(
            _presence, _notifier, _messageClient, new FixedTimeProvider(Now), NullLogger<MessageSentHandler>.Instance);

    private MessageSent MessageFromAliceTo(params Guid[] recipients) =>
        new(Guid.NewGuid(), Guid.NewGuid(), _alice, "Hej", Now.AddSeconds(-1), recipients);

    [Fact]
    public async Task Handle_OnlineRecipients_PushesAndPublishesMessageDelivered()
    {
        _presence.Connected(_bob); // Carol is offline
        var message = MessageFromAliceTo(_bob, _carol);

        await _handler.HandleAsync(message, CancellationToken.None);

        var push = Assert.Single(_notifier.Pushes);
        Assert.Equal([_bob], push.UserIds);
        Assert.Equal(
            new MessageReceived(message.MessageId, message.ChatId, _alice, "Hej", message.SentAt),
            push.Message);

        var delivered = Assert.IsType<MessageDelivered>(Assert.Single(_messageClient.Published));
        Assert.Equal(message.MessageId, delivered.MessageId);
        Assert.Equal(message.ChatId, delivered.ChatId);
        Assert.Equal([_bob], delivered.DeliveredToUserIds);
        Assert.Equal(Now, delivered.DeliveredAt);
    }

    [Fact]
    public async Task Handle_NoOnlineRecipients_PushesAndPublishesNothing()
    {
        await _handler.HandleAsync(MessageFromAliceTo(_bob, _carol), CancellationToken.None);

        Assert.Empty(_notifier.Pushes);
        Assert.Empty(_messageClient.Published);
    }
}
