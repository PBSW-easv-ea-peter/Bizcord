using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Bizcord.ApiGateway.Resilience;

/// <summary>
/// Ocelot delegating handler that runs the downstream call through <see cref="DownstreamQoS"/>.
/// Enabled per route with <c>"DelegatingHandlers": ["DownstreamQoSHandler"]</c>.
/// We use this instead of Ocelot's QoSOptions because Ocelot.Provider.Polly has no stable release for Ocelot 25.
/// </summary>
public sealed class DownstreamQoSHandler(DownstreamQoS qos) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var pipeline = qos.For(request.RequestUri!);
        try
        {
            return await pipeline.ExecuteAsync(
                async token => await base.SendAsync(request, token), cancellationToken);
        }
        catch (TimeoutRejectedException)
        {
            return Problem(request, HttpStatusCode.GatewayTimeout, "The downstream service did not answer in time.");
        }
        catch (BrokenCircuitException ex)
        {
            // Fail fast: the downstream service was not called at all.
            var response = Problem(request, HttpStatusCode.ServiceUnavailable, "The downstream service is unavailable. Try again later.");
            if (ex.RetryAfter is { } retryAfter)
                response.Headers.RetryAfter = new RetryConditionHeaderValue(retryAfter);
            return response;
        }
    }

    private static HttpResponseMessage Problem(HttpRequestMessage request, HttpStatusCode status, string title) =>
        new(status)
        {
            RequestMessage = request,
            Content = JsonContent.Create(
                new { title, status = (int)status },
                mediaType: new MediaTypeHeaderValue("application/problem+json"))
        };
}
