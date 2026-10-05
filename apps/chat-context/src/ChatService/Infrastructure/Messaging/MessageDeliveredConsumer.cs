using ChatService.Application;
using ChatService.Contracts;
using EasyNetQ;

namespace ChatService.Infrastructure.Messaging;

/// <summary>
/// Subscribes to RTC's 'rtc.message-delivered' and sets delivered_at on receipts.
/// Subscribes in StartAsync, so the host is not ready until the queue is bound.
/// </summary>
public sealed class MessageDeliveredConsumer(IBus bus, IServiceScopeFactory scopeFactory) : IHostedService
{
    public const string SubscriptionId = "chat-service";

    private SubscriptionResult? _subscription;

    public async Task StartAsync(CancellationToken cancellationToken) =>
        _subscription = await bus.PubSub.SubscribeAsync<MessageDelivered>(
            SubscriptionId,
            async (message, messageCancellation) =>
            {
                // One scope per message, just like one per HTTP request (repositories are scoped).
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<MessageAppService>()
                    .MarkDeliveredAsync(message.MessageId, message.DeliveredToUserIds, message.DeliveredAt, messageCancellation);
            },
            _ => { },
            cancellationToken);

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_subscription is { } subscription)
            await subscription.DisposeAsync();
    }
}
