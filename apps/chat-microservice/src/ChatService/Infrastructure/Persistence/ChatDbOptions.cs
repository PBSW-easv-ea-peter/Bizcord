namespace ChatService.Infrastructure.Persistence;

/// <summary>Timeouts and circuit breaker for chatDB (configuration section "ChatDb"). The defaults apply in every environment.</summary>
public sealed class ChatDbOptions
{
    public const string SectionName = "ChatDb";

    /// <summary>Max time to open a connection. Npgsql's default is 15 s.</summary>
    public int TimeoutSeconds { get; set; } = 3;

    /// <summary>Max time per command. Npgsql's default is 30 s, and it may wait up to 2 s more for the cancel request.</summary>
    public int CommandTimeoutSeconds { get; set; } = 3;

    public double FailureRatio { get; set; } = 0.5;

    /// <summary>Calls within the sampling window before the circuit may open. Polly's default of 100 never trips at our traffic.</summary>
    public int MinimumThroughput { get; set; } = 5;

    public int SamplingDurationSeconds { get; set; } = 30;

    public int BreakDurationSeconds { get; set; } = 15;
}
