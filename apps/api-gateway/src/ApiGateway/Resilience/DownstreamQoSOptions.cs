namespace Bizcord.ApiGateway.Resilience;

/// <summary>Timeout and circuit breaker for calls from the gateway to a downstream service (section "DownstreamQoS").</summary>
public sealed class DownstreamQoSOptions
{
    public const string Section = "DownstreamQoS";

    /// <summary>Longer than a normal answer, shorter than the client's own timeout.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(2);

    public double FailureRatio { get; set; } = 0.5;

    /// <summary>Calls needed within <see cref="SamplingDuration"/> before the breaker may open (Polly minimum: 2).</summary>
    public int MinimumThroughput { get; set; } = 3;

    public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(10);

    public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(5);
}
