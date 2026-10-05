using ChatService.Application;
using ChatService.Contracts;
using EasyNetQ;

namespace ChatService.Infrastructure.Messaging;

/// <summary>
/// Abonnerer på RTC's 'rtc.message-delivered' og sætter delivered_at på receipts.
/// Abonnerer i StartAsync, så hosten først er klar, når køen er bundet.
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
                // Ét scope pr. besked, ligesom ét pr. HTTP-request (repositories er scoped).
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
