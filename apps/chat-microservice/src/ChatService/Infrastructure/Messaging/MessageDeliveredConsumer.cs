using ChatService.Application;
using ChatService.Contracts;

namespace ChatService.Infrastructure.Messaging;

/// <summary>
/// Subscribes to RTC's 'rtc.message-delivered' and sets delivered_at on receipts.
/// Subscribes in StartAsync, so the host is not ready until the queue is bound.
/// </summary>
public sealed class MessageDeliveredConsumer(IMessageClient messageClient, IServiceScopeFactory scopeFactory) : IHostedService
{
    public const string SubscriptionId = "chat-service";

    public async Task StartAsync(CancellationToken cancellationToken) =>
        await messageClient.SubscribeAsync<MessageDelivered>(
            SubscriptionId,
            async message =>
            {
                // One scope per message, just like one per HTTP request (repositories are scoped).
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<MessageAppService>()
                    .MarkDeliveredAsync(message.MessageId, message.DeliveredToUserIds, message.DeliveredAt, cancellationToken);
            });

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
