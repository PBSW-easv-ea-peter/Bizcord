using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Bizcord.ApiGateway.Resilience;

/// <summary>
/// One resilience pipeline (circuit breaker around a timeout) per downstream service.
/// Singleton - the breakers' state must outlive the handlers, which Ocelot creates per route.
/// Keyed by host:port, so a slow ChatService does not open the circuit for RTC.
/// </summary>
public sealed class DownstreamQoS(IOptions<DownstreamQoSOptions> options, TimeProvider timeProvider, ILogger<DownstreamQoS> logger)
{
    private readonly ConcurrentDictionary<string, ResiliencePipeline<HttpResponseMessage>> _pipelines = new();

    public ResiliencePipeline<HttpResponseMessage> For(Uri downstream) =>
        _pipelines.GetOrAdd(downstream.Authority, Build);

    private ResiliencePipeline<HttpResponseMessage> Build(string downstream)
    {
        var o = options.Value;

        // The timeout is inside the breaker, so a call that times out counts as a failure.
        return new ResiliencePipelineBuilder<HttpResponseMessage> { TimeProvider = timeProvider }
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = o.FailureRatio,
                MinimumThroughput = o.MinimumThroughput,
                SamplingDuration = o.SamplingDuration,
                BreakDuration = o.BreakDuration,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutRejectedException>()
                    .HandleResult(response => IsUnavailable(response.StatusCode)),
                OnOpened = args =>
                {
                    logger.LogWarning("Circuit to {Downstream} opened for {BreakSeconds} s.", downstream, args.BreakDuration.TotalSeconds);
                    return default;
                },
                OnHalfOpened = _ =>
                {
                    logger.LogInformation("Circuit to {Downstream} half-open - trying one call.", downstream);
                    return default;
                },
                OnClosed = _ =>
                {
                    logger.LogInformation("Circuit to {Downstream} closed.", downstream);
                    return default;
                }
            })
            .AddTimeout(o.Timeout)
            .Build();
    }

    /// <summary>
    /// Only "the service is sick" answers open the circuit. A 500 from a bug in one endpoint or a 4xx
    /// is an answer from a working service.
    /// </summary>
    internal static bool IsUnavailable(HttpStatusCode status) =>
        status is HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;
}
