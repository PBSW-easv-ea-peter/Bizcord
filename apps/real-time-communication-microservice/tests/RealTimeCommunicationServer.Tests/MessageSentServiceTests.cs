using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using RealTimeCommunicationServer.Contracts;
using RealTimeCommunicationServer.Messaging;
using RealTimeCommunicationServer.Realtime;

namespace RealTimeCommunicationServer.Tests;

/// <summary>
/// Service test: hele RTC (rigtig opsætning, rigtig RabbitMQ, rigtig SignalR-klient) - ingen ChatService.
/// Eventet publiceres direkte på brokeren, som ChatService ville gøre det.
/// </summary>
[Trait("Category", "Integration")]
public class MessageSentServiceTests(RtcApiFactory factory) : IClassFixture<RtcApiFactory>
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly Guid _alice = Guid.NewGuid();
    private readonly Guid _bob = Guid.NewGuid();

    // STORE bogstaver: regression - presence kaldte brugeren online, men SignalR matchede ikke strengen,
    // så pushet forsvandt, mens rtc.message-delivered alligevel blev publiceret.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MessageSent_IsPushedToOnlineRecipient_AndMessageDeliveredPublished(bool upperCaseUserId)
    {
        // Arrange: Alice er online, Bob er ikke
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

        // Act: RTC's MessageSent mapper til 'chat.message-sent' - samme exchange som ChatService publicerer på
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
                // Gennem TestServer i hukommelsen - LongPolling, fordi TestServer ikke laver rigtige WebSockets her.
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();

    // Hubben registrerer presence i OnConnectedAsync, som kan køre lige efter klientens StartAsync returnerer.
    private async Task WaitUntilOnline(Guid userId)
    {
        var presence = factory.Services.GetRequiredService<PresenceTracker>();
        using var timeout = new CancellationTokenSource(Timeout);
        while (!presence.IsOnline(userId))
            await Task.Delay(20, timeout.Token);
    }
}
