using System.Diagnostics;
using System.Net;
using ChatService.Tests.Persistence;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;

namespace ChatService.Tests.Api;

/// <summary>
/// Postgres hangs (container paused): calls fail fast with 503, and after repeated failures the circuit opens,
/// so ChatService stops calling the database until BreakDuration has passed.
/// Own container - pausing the shared TestDatabase would break the other tests.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ChatDbOutageTests : IAsyncLifetime
{
    private static readonly TimeSpan BreakDuration = TimeSpan.FromSeconds(2);

    private readonly PostgreSqlContainer _postgres = TestDatabase.CreateContainer();
    private ChatApiFactory _factory = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _factory = new ChatApiFactory(_postgres.GetConnectionString(), new Dictionary<string, string?>
        {
            ["ChatDb:TimeoutSeconds"] = "1",
            ["ChatDb:CommandTimeoutSeconds"] = "1",
            ["ChatDb:MinimumThroughput"] = "2",
            ["ChatDb:BreakDurationSeconds"] = BreakDuration.TotalSeconds.ToString()
        });
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task PostgresHangs_FailsFastWith503_ThenOpensCircuit_ThenRecovers()
    {
        var client = _factory.CreateClient();
        // Without a short DB timeout the first call would hang for 30 s - fail the test instead.
        client.Timeout = TimeSpan.FromSeconds(15);
        var userId = Guid.NewGuid();
        client.DefaultRequestHeaders.Add("X-User-Id", userId.ToString());
        var chatId = Guid.NewGuid();
        var url = $"/chats/{chatId}";

        // The database answers: unknown chat.
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(url)).StatusCode);

        await _postgres.PauseAsync();

        // Circuit closed: the call reaches Postgres and waits for the timeout.
        var (first, firstElapsed) = await TimedGetAsync(client, url);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, first.StatusCode);
        Assert.True(firstElapsed >= TimeSpan.FromSeconds(1), $"Expected the call to wait for the DB timeout, took {firstElapsed}.");

        // Keep calling until the circuit opens - then the answer comes immediately, without calling Postgres.
        HttpResponseMessage? rejected = null;
        for (var attempt = 0; attempt < 5 && rejected is null; attempt++)
        {
            var (response, elapsed) = await TimedGetAsync(client, url);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            if (elapsed < TimeSpan.FromMilliseconds(500))
                rejected = response;
        }

        Assert.NotNull(rejected);
        Assert.NotNull(rejected.Headers.RetryAfter);

        // Timeout = Error (the database is sick), circuit open = Warning (expected while it is) - both with the ids.
        var outageLog = _factory.HandlerLog.Entries.Where(e => chatId.Equals(e.Payload.GetValueOrDefault("ChatId"))).ToList();
        Assert.Contains(outageLog, e => e.Level == LogLevel.Error && e.Message == "ChatDB did not respond.");
        Assert.Contains(outageLog, e => e.Level == LogLevel.Warning && e.Message.Contains("circuit is open"));
        Assert.All(outageLog, e => Assert.Equal(userId, e.Payload["UserId"]));

        // Postgres is back: after BreakDuration the half-open trial call gets through and closes the circuit.
        await _postgres.UnpauseAsync();
        await Task.Delay(BreakDuration + TimeSpan.FromMilliseconds(500));

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(url)).StatusCode);
    }

    private static async Task<(HttpResponseMessage Response, TimeSpan Elapsed)> TimedGetAsync(HttpClient client, string url)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = await client.GetAsync(url);
        return (response, stopwatch.Elapsed);
    }
}
