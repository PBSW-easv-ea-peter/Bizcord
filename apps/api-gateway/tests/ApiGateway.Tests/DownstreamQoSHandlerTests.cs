using System.Diagnostics;
using System.Net;
using Bizcord.ApiGateway.Resilience;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace ApiGateway.Tests;

public class DownstreamQoSHandlerTests
{
    private static readonly Uri ChatService = new("http://chatservice:8081/chats");
    private static readonly Uri Rtc = new("http://realtimecommunicationserver:8082/api/messages");

    private static readonly DownstreamQoSOptions Options = new()
    {
        Timeout = TimeSpan.FromMilliseconds(200),
        FailureRatio = 0.5,
        MinimumThroughput = 3,
        SamplingDuration = TimeSpan.FromSeconds(10),
        BreakDuration = TimeSpan.FromSeconds(5)
    };

    [Fact]
    public async Task Healthy_downstream_passes_through()
    {
        var downstream = new FakeDownstream(_ => HttpStatusCode.OK);
        using var client = Client(downstream);

        var response = await client.SendAsync(Get(ChatService), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, downstream.Calls);
    }

    [Fact]
    public async Task Slow_downstream_gets_504_after_the_timeout()
    {
        var downstream = new FakeDownstream(async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), token);
            return HttpStatusCode.OK;
        });
        using var client = Client(downstream);
        var stopwatch = Stopwatch.StartNew();

        var response = await client.SendAsync(Get(ChatService), CancellationToken.None);

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(3), $"Took {stopwatch.Elapsed}");
    }

    [Fact]
    public async Task Repeated_unavailable_answers_open_the_circuit_and_the_next_call_fails_fast()
    {
        var downstream = new FakeDownstream(_ => HttpStatusCode.ServiceUnavailable);
        using var client = Client(downstream);

        for (var i = 0; i < Options.MinimumThroughput; i++)
            await client.SendAsync(Get(ChatService), CancellationToken.None);
        var response = await client.SendAsync(Get(ChatService), CancellationToken.None);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.NotNull(response.Headers.RetryAfter);
        Assert.Equal(Options.MinimumThroughput, downstream.Calls); // the last call never reached the service
    }

    [Fact]
    public async Task A_500_does_not_open_the_circuit()
    {
        var downstream = new FakeDownstream(_ => HttpStatusCode.InternalServerError);
        using var client = Client(downstream);

        for (var i = 0; i < Options.MinimumThroughput + 2; i++)
            await client.SendAsync(Get(ChatService), CancellationToken.None);

        Assert.Equal(Options.MinimumThroughput + 2, downstream.Calls);
    }

    [Fact]
    public async Task An_open_circuit_to_one_service_does_not_stop_calls_to_another()
    {
        var downstream = new FakeDownstream(request =>
            request.RequestUri!.Authority == ChatService.Authority ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK);
        using var client = Client(downstream);

        for (var i = 0; i < Options.MinimumThroughput + 1; i++)
            await client.SendAsync(Get(ChatService), CancellationToken.None);
        var response = await client.SendAsync(Get(Rtc), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task The_circuit_tries_again_after_the_break()
    {
        var time = new FakeTimeProvider();
        var healthy = false;
        var downstream = new FakeDownstream(_ => healthy ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable);
        using var client = Client(downstream, time);

        for (var i = 0; i < Options.MinimumThroughput; i++)
            await client.SendAsync(Get(ChatService), CancellationToken.None);
        healthy = true;
        time.Advance(Options.BreakDuration + TimeSpan.FromSeconds(1));

        var response = await client.SendAsync(Get(ChatService), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Options.MinimumThroughput + 1, downstream.Calls);
    }

    private static HttpMessageInvoker Client(FakeDownstream downstream, TimeProvider? time = null)
    {
        var qos = new DownstreamQoS(
            Microsoft.Extensions.Options.Options.Create(Options),
            time ?? TimeProvider.System,
            NullLogger<DownstreamQoS>.Instance);
        return new HttpMessageInvoker(new DownstreamQoSHandler(qos) { InnerHandler = downstream });
    }

    private static HttpRequestMessage Get(Uri uri) => new(HttpMethod.Get, uri);

    private sealed class FakeDownstream(Func<HttpRequestMessage, CancellationToken, Task<HttpStatusCode>> answer) : HttpMessageHandler
    {
        private int _calls;

        public FakeDownstream(Func<HttpRequestMessage, HttpStatusCode> answer)
            : this((request, _) => Task.FromResult(answer(request))) { }

        public int Calls => _calls;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            return new HttpResponseMessage(await answer(request, cancellationToken)) { RequestMessage = request };
        }
    }
}
