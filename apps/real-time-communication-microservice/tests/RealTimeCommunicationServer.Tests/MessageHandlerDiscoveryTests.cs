using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RealTimeCommunicationServer.Contracts;
using RealTimeCommunicationServer.Messaging;
using RealTimeCommunicationServer.Messaging.Handlers;
using RealTimeCommunicationServer.Models;
using RealTimeCommunicationServer.Realtime;

namespace RealTimeCommunicationServer.Tests;

public class MessageHandlerDiscoveryTests
{
    private static readonly System.Reflection.Assembly ServerAssembly = typeof(PingMessageHandler).Assembly;

    [Fact]
    public void Finds_a_message_type_for_every_handler_in_the_server()
    {
        var registry = BuildProvider(ServerAssembly).GetRequiredService<MessageHandlerRegistry>();

        Assert.Equal(
            new[] { typeof(PingMessage), typeof(MessageSent), typeof(ParticipantAdded), typeof(MessagesSeen) }.OrderBy(t => t.Name),
            registry.MessageTypes.OrderBy(t => t.Name));
    }

    [Fact]
    public void Handlers_are_resolvable_from_a_scope()
    {
        using var scope = BuildProvider(ServerAssembly).CreateScope();

        Assert.IsType<MessageSentHandler>(Assert.Single(scope.ServiceProvider.GetServices<IMessageHandler<MessageSent>>()));
        Assert.IsType<PingMessageHandler>(Assert.Single(scope.ServiceProvider.GetServices<IMessageHandler<PingMessage>>()));
    }

    [Fact]
    public void A_new_handler_class_is_registered_without_other_changes()
    {
        // Scanner test-assembly'en, hvor TestMessageHandler blot er defineret - ingen registrering nogen steder.
        var provider = BuildProvider(typeof(TestMessageHandler).Assembly);

        Assert.Contains(typeof(TestMessage), provider.GetRequiredService<MessageHandlerRegistry>().MessageTypes);
    }

    [Fact]
    public async Task Received_message_is_dispatched_to_all_handlers_for_its_type()
    {
        var client = new FakeMessageClient();
        var services = new ServiceCollection()
            .AddLogging()
            .AddMessageHandlers(typeof(TestMessageHandler).Assembly)
            .AddSingleton<IMessageClient>(client)
            .AddSingleton<Received>();
        using var provider = services.BuildServiceProvider();

        var worker = ActivatorUtilities.CreateInstance<HandleMessages>(provider);
        await worker.StartAsync(CancellationToken.None);

        await client.DeliverAsync(new TestMessage("hello"));

        // To handlers for samme type - begge kaldes, fra én subscription.
        Assert.Equal(["first:hello", "second:hello"], provider.GetRequiredService<Received>().Messages.Order());
        Assert.Single(client.Subscriptions, s => s.Type == typeof(TestMessage));
    }

    private static ServiceProvider BuildProvider(System.Reflection.Assembly assembly) =>
        new ServiceCollection()
            .AddLogging()
            .AddSingleton<Received>()
            // MessageSentHandler's afhængigheder
            .AddSingleton<PresenceTracker>()
            .AddSingleton<IClientNotifier, FakeClientNotifier>()
            .AddSingleton<IMessageClient, FakeMessageClient>()
            .AddSingleton(TimeProvider.System)
            .AddMessageHandlers(assembly)
            .BuildServiceProvider();
}

public record TestMessage(string Text);

public class Received
{
    public List<string> Messages { get; } = [];
}

public class TestMessageHandler : IMessageHandler<TestMessage>
{
    private readonly Received _received;

    public TestMessageHandler(Received received) => _received = received;

    public Task HandleAsync(TestMessage message, CancellationToken cancellationToken)
    {
        _received.Messages.Add($"first:{message.Text}");
        return Task.CompletedTask;
    }
}

public class SecondTestMessageHandler : IMessageHandler<TestMessage>
{
    private readonly Received _received;

    public SecondTestMessageHandler(Received received) => _received = received;

    public Task HandleAsync(TestMessage message, CancellationToken cancellationToken)
    {
        _received.Messages.Add($"second:{message.Text}");
        return Task.CompletedTask;
    }
}

/// <summary>
/// Husker subscriptions og publicerede beskeder i stedet for at tale med RabbitMQ,
/// så en besked kan afleveres direkte, og det publicerede kan verificeres.
/// </summary>
public class FakeMessageClient : IMessageClient
{
    public List<(Type Type, string SubscriptionId, Delegate Handler)> Subscriptions { get; } = [];

    public List<object> Published { get; } = [];

    public Task Publish<T>(T message, CancellationToken cancellationToken = default)
    {
        Published.Add(message!);
        return Task.CompletedTask;
    }

    public Task Subscribe<T>(string subscriptionId, Func<T, Task> handler)
    {
        Subscriptions.Add((typeof(T), subscriptionId, handler));
        return Task.CompletedTask;
    }

    public Task DeliverAsync<T>(T message) =>
        Subscriptions
            .Where(s => s.Type == typeof(T))
            .Select(s => ((Func<T, Task>)s.Handler)(message))
            .Single();
}
