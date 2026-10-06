namespace ChatService.Application;

internal static class TimeProviderExtensions
{
    /// <summary>
    /// Now, truncated to microseconds - same precision as Postgres' timestamptz,
    /// so an object in memory and the same object read from the DB have identical timestamps.
    /// </summary>
    public static DateTimeOffset GetUtcNowInMicroseconds(this TimeProvider time)
    {
        var now = time.GetUtcNow();
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond));
    }
}
