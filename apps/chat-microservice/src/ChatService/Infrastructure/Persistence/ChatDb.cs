using Bizcord.Logging;
using ChatService.Application;
using Npgsql;
using Polly;
using Polly.CircuitBreaker;

namespace ChatService.Infrastructure.Persistence;

/// <summary>
/// The repositories' only way into chatDB: opens a connection and runs the work behind a circuit breaker.
/// Singleton - the breaker's state (failures, open/closed) must be shared by all requests.
/// </summary>
public sealed class ChatDb
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ResiliencePipeline _pipeline;

    public ChatDb(NpgsqlDataSource dataSource, ChatDbOptions options, ILogger<ChatDb> logger)
    {
        _dataSource = dataSource;
        _pipeline = new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = options.FailureRatio,
                MinimumThroughput = options.MinimumThroughput,
                SamplingDuration = TimeSpan.FromSeconds(options.SamplingDurationSeconds),
                BreakDuration = TimeSpan.FromSeconds(options.BreakDurationSeconds),
                ShouldHandle = new PredicateBuilder().Handle<NpgsqlException>(IsUnavailable),
                OnOpened = args =>
                {
                    logger.Error("ChatDB circuit opened.", new { BreakDurationSeconds = args.BreakDuration.TotalSeconds }, args.Outcome.Exception);
                    return default;
                },
                OnHalfOpened = _ =>
                {
                    logger.Information("ChatDB circuit half-open - trying one call.");
                    return default;
                },
                OnClosed = _ =>
                {
                    logger.Information("ChatDB circuit closed.");
                    return default;
                }
            })
            .Build();
    }

    /// <exception cref="DatabaseUnavailableException">The database timed out or is unreachable, or the circuit is open.</exception>
    public async Task<T> RunAsync<T>(Func<NpgsqlConnection, Task<T>> work, CancellationToken cancellationToken)
    {
        try
        {
            // Opening the connection is inside the breaker too: a hanging Postgres fails there for new connections.
            return await _pipeline.ExecuteAsync(async token =>
            {
                await using var connection = await _dataSource.OpenConnectionAsync(token);
                return await work(connection);
            }, cancellationToken);
        }
        catch (BrokenCircuitException ex)
        {
            throw new DatabaseUnavailableException("ChatDB is unavailable - the circuit is open.", ex.RetryAfter, ex);
        }
        catch (NpgsqlException ex) when (IsUnavailable(ex))
        {
            throw new DatabaseUnavailableException("ChatDB did not respond.", null, ex);
        }
    }

    public Task RunAsync(Func<NpgsqlConnection, Task> work, CancellationToken cancellationToken) =>
        RunAsync(async connection =>
        {
            await work(connection);
            return true;
        }, cancellationToken);

    /// <summary>
    /// Timeouts and lost connections mean the database is sick. A PostgresException is an answer from a working
    /// database (e.g. a unique violation that becomes 409) and must not open the circuit.
    /// </summary>
    private static bool IsUnavailable(NpgsqlException exception) => exception is not PostgresException;
}
