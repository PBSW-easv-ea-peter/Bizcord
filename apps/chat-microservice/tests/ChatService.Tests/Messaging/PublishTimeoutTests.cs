using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using ChatService.Contracts;
using ChatService.Tests.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.RabbitMq;

namespace ChatService.Tests.Messaging;

/// <summary>
/// Failure mode #2: sending a message must not hang when the broker is shut down.
/// Each test owns its broker - it is paused/stopped, and the shared TestBroker must stay up for the other tests.
/// </summary>

[Trait("Category", "Integration")]
public class PublishTimeoutTests
{
    private const string BrokerUser = "rabbitmq";
    private static readonly TimeSpan PublishTimeout = TimeSpan.FromSeconds(1);

    // Well above PublishTimeout plus host and DB overhead, but well below EasyNetQ's
    // 10 s operation timeout - so the test fails without our own timeout.
    private static readonly TimeSpan MaxResponseTime = TimeSpan.FromSeconds(5);
    
    private static async Task<RabbitMqContainer> StartBrokerAsync()
    {
        var broker = new RabbitMqBuilder("rabbitmq:3-alpine")
            .WithUsername(BrokerUser)
            .WithPassword(BrokerUser)
            .Build();

        await broker.StartAsync();
        return broker;
    }

    private static string ConnectionString(RabbitMqContainer broker) => 
        $"host={broker.Hostname}:{broker.GetMappedPublicPort(RabbitMqBuilder.RabbitMqPort)};username={BrokerUser};password={BrokerUser}";
    
    private sealed class Factory(string rabbitMqConnectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:ChatDb", TestDatabase.ConnectionString);
            builder.UseSetting("ConnectionStrings:RabbitMq", rabbitMqConnectionString);
            builder.UseSetting("RabbitMq:PublishTimeout", PublishTimeout.ToString());
        }
    }
    private static HttpClient ClientFor(Factory factory, Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", userId.ToString());
        return client;
    }

    [Fact]
    public async Task Send_WhenBrokerIsStopped_ReturnsCreatedWithinTimeout()
    {
        // Arrange: the host starts while the broker is up - ChatService cannot start without it (#10)
        await using var broker = await StartBrokerAsync();
        await using var factory = new Factory(ConnectionString(broker));
        var alice = ClientFor(factory, Guid.NewGuid());

        var createResponse = await alice.PostAsJsonAsync("/chats/direct", new CreateDirectChatRequest(Guid.NewGuid()));
        var chat = (await createResponse.Content.ReadFromJsonAsync<ChatResponse>())!;

        await broker.StopAsync();

        // Act
        var stopwatch = Stopwatch.StartNew();
        var sendResponse = await alice.PostAsJsonAsync($"/chats/{chat.Id}/messages", new SendMessageRequest("Hej Bob"));
        stopwatch.Stop();

        // Assert: fast, and the message is still saved - REST is the source of truth
        Assert.Equal(HttpStatusCode.Created, sendResponse.StatusCode);
        Assert.True(stopwatch.Elapsed < MaxResponseTime, $"Send took {stopwatch.Elapsed.TotalSeconds:F1} s");

        var message = (await sendResponse.Content.ReadFromJsonAsync<MessageResponse>())!;
        var page = await alice.GetFromJsonAsync<List<MessageResponse>>($"/chats/{chat.Id}/messages");
        Assert.Contains(page!, m => m.Id == message.Id);
    }
}