using Bizcord.Logging;
using ChatService.Application;

namespace ChatService.Infrastructure.Messaging;

/// <summary>
/// Gives the file-message saga an owner for "nobody answered". Every <c>FileSaga:CheckInterval</c> it compensates
/// sagas that have waited longer than <c>FileSaga:Timeout</c> - without it, a lost message would leave a saga hanging forever.
/// </summary>
public sealed class FileSagaTimeoutService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<FileSagaTimeoutService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var timeout = configuration.GetValue("FileSaga:Timeout", TimeSpan.FromMinutes(2));
        var interval = configuration.GetValue("FileSaga:CheckInterval", TimeSpan.FromSeconds(30));

        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<FileMessageSagaOrchestrator>()
                    .TimeOutStaleAsync(timeout, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // E.g. ChatDB is down - try again on the next tick instead of stopping the job.
                logger.Error("File saga timeout check failed.", exception: ex);
            }
        }
    }
}
