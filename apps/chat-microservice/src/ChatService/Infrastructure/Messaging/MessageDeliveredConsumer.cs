using ChatService.Application;
using ChatService.Contracts;

namespace ChatService.Infrastructure.Messaging;

/// <summary>
/// Abonnerer på RTC's 'rtc.message-delivered' og sætter delivered_at på receipts.
/// Abonnerer i StartAsync, så hosten først er klar, når køen er bundet.
/// </summary>
public sealed class MessageDeliveredConsumer(IMessageClient messageClient, IServiceScopeFactory scopeFactory) : IHostedService
{
    public const string SubscriptionId = "chat-service";

    public async Task StartAsync(CancellationToken cancellationToken) =>
        await messageClient.SubscribeAsync<MessageDelivered>(
            SubscriptionId,
            async message =>
            {
                // Ét scope pr. besked, ligesom ét pr. HTTP-request (repositories er scoped).
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<MessageAppService>()
                    .MarkDeliveredAsync(message.MessageId, message.DeliveredToUserIds, message.DeliveredAt, cancellationToken);
            });

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
