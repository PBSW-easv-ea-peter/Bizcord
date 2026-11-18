using FileService.Application;
using FileService.Contracts;

namespace FileService.Infrastructure.Messaging;

/// <summary>Subscribes to ChatService's chat.message-deleted (to free the quota of deleted files), one DI scope per message.</summary>
public sealed class ChatEventConsumer(IMessageClient messageClient, IServiceScopeFactory scopeFactory) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) =>
        messageClient.SubscribeAsync<MessageDeleted>(
            QuotaCommandConsumer.SubscriptionId,
            async message =>
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<MessageDeletedHandler>().HandleAsync(message, cancellationToken);
            });

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
