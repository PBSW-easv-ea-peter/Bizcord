using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using RealTimeCommunicationServer.Contracts;
using RealTimeCommunicationServer.Messaging;
using RealTimeCommunicationServer.Realtime;

namespace RealTimeCommunicationServer.Tests;

/// <summary>
/// Service test: all of RTC (real setup, real RabbitMQ, real SignalR client) - no ChatService.
/// The event is published directly on the broker, the way ChatService would.
/// </summary>
[Trait("Category", "Integration")]
public class MessageSentServiceTests(RtcApiFactory factory) : IClassFixture<RtcApiFactory>
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly Guid _alice = Guid.NewGuid();
    private readonly Guid _bob = Guid.NewGuid();

    // UPPERCASE: regression - presence reported the user online, but SignalR did not match the string,
    // so the push was lost while rtc.message-delivered was still published.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MessageSent_IsPushedToOnlineRecipient_AndMessageDeliveredPublished(bool upperCaseUserId)
    {
        // Arrange: Alice is online, Bob is not
        var pushed = new TaskCompletionSource<MessageReceived>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var alice = ConnectAs(upperCaseUserId ? _alice.ToString().ToUpperInvariant() : _alice.ToString());
        alice.On<MessageReceived>(nameof(IChatClient.MessageReceived), message => pushed.TrySetResult(message));
        await alice.StartAsync();
        await WaitUntilOnline(_alice);

        var messageClient = factory.Services.GetRequiredService<IMessageClient>();
        var delivered = new TaskCompletionSource<MessageDelivered>(TaskCreationOptions.RunContinuationsAsynchronously);
        await messageClient.Subscribe<MessageDelivered>($"test-{Guid.NewGuid()}", message =>
        {
            delivered.TrySetResult(message);
            return Task.CompletedTask;
        });

        var sent = new MessageSent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Hej", DateTimeOffset.UtcNow, [_alice, _bob]);

        // Act: RTC's MessageSent maps to 'chat.message-sent' - the same exchange ChatService publishes to
        await messageClient.Publish(sent);

        // Assert
        var push = await pushed.Task.WaitAsync(Timeout);
        Assert.Equal(sent.MessageId, push.MessageId);
        Assert.Equal("Hej", push.Content);

        var evt = await delivered.Task.WaitAsync(Timeout);
        Assert.Equal(sent.MessageId, evt.MessageId);
        Assert.Equal(sent.ChatId, evt.ChatId);
        Assert.Equal([_alice], evt.DeliveredToUserIds);
    }

    private HubConnection ConnectAs(string userId) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, $"{ChatHub.Path}?{QueryStringUserIdProvider.QueryKey}={userId}"), options =>
            {
                // Through the in-memory TestServer - LongPolling, because TestServer doesn't do real WebSockets here.
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();

    // The hub registers presence in OnConnectedAsync, which may run just after the client's StartAsync returns.
    private async Task WaitUntilOnline(Guid userId)
    {
        var presence = factory.Services.GetRequiredService<PresenceTracker>();
        using var timeout = new CancellationTokenSource(Timeout);
        while (!presence.IsOnline(userId))
            await Task.Delay(20, timeout.Token);
    }
}
