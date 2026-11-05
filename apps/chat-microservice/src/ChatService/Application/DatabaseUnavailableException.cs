namespace ChatService.Application;

/// <summary>
/// The database did not answer in time, or the circuit breaker is open and the call was never made.
/// RetryAfter is only set when the circuit is open: how long until the database is tried again.
/// </summary>
public sealed class DatabaseUnavailableException(string message, TimeSpan? retryAfter, Exception innerException)
    : Exception(message, innerException)
{
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
