namespace ChatService.Application;

internal static class TimeProviderExtensions
{
    /// <summary>
    /// Nu, afrundet til mikrosekunder - samme præcision som Postgres' timestamptz,
    /// så et objekt i hukommelsen og samme objekt læst fra DB'en har ens tidspunkt.
    /// </summary>
    public static DateTimeOffset GetUtcNowInMicroseconds(this TimeProvider time)
    {
        var now = time.GetUtcNow();
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond));
    }
}
