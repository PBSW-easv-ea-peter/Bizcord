using System.Text;
using System.Text.Json;
using ChatService.Application;
using ChatService.Contracts;
using ChatService.Infrastructure.Messaging;
using EasyNetQ;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ChatService.Tests.Messaging
{
    /// <summary>
    /// Spike: proves that the event contract is independent of C# types and language.
    /// RabbitMQ runs in a Testcontainer (see TestBroker).
    /// </summary>
    [Trait("Category", "Integration")]
    public class EventContractSpikeTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private static readonly MessageSent Event = new(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Hej",
            new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero), [Guid.NewGuid()]);

        private static ServiceProvider CreateServices(EventTypeNames? typeNames = null) =>
            new ServiceCollection()
                .AddLogging()
                .AddChatMessaging(TestBroker.ConnectionString, typeNames)
                .BuildServiceProvider();

        [Fact]
        public async Task ConsumerWithOwnClassInOtherNamespace_ReceivesEvent()
        {
            // "RTC": its own class, a different namespace, only the fields it needs - same logical name.
            await using var consumerServices = CreateServices(new EventTypeNames(new Dictionary<Type, string>
            {
                [typeof(OtherTeam.MessageSentV1)] = EventNames.MessageSent
            }));
            await using var publisherServices = CreateServices();

            var received = new TaskCompletionSource<OtherTeam.MessageSentV1>(TaskCreationOptions.RunContinuationsAsynchronously);
            await consumerServices.GetRequiredService<IBus>().PubSub.SubscribeAsync<OtherTeam.MessageSentV1>(
                $"spike-{Guid.NewGuid()}",
                // The broker is shared with other tests that also publish chat.message-sent - only our own event counts.
                (message, _) =>
                {
                    if (message.MessageId == Event.MessageId)
                        received.TrySetResult(message);
                    return Task.CompletedTask;
                },
                config => config.WithAutoDelete());

            await publisherServices.GetRequiredService<IMessageClient>().PublishAsync(Event);

            var message = await received.Task.WaitAsync(Timeout);
            Assert.Equal(Event.MessageId, message.MessageId);
            Assert.Equal(Event.Content, message.Content);
        }

        [Fact]
        public async Task RawConsumer_SeesLogicalNamesAndCamelCaseJson()
        {
            // No EasyNetQ, no C# contract - like a consumer written in another language.
            var factory = new ConnectionFactory
            {
                HostName = TestBroker.Host, Port = TestBroker.Port, UserName = TestBroker.UserName, Password = TestBroker.Password
            };
            await using var connection = await factory.CreateConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();
            await channel.ExchangeDeclareAsync(EventNames.MessageSent, RabbitMQ.Client.ExchangeType.Topic, durable: true);
            var queue = await channel.QueueDeclareAsync();
            await channel.QueueBindAsync(queue.QueueName, EventNames.MessageSent, "#");

            var received = new TaskCompletionSource<(string Exchange, string? Type, string Body)>(TaskCreationOptions.RunContinuationsAsynchronously);
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += (_, delivery) =>
            {
                // The body buffer is reused after the handler returns - copy it now.
                var body = Encoding.UTF8.GetString(delivery.Body.Span);

                // The broker is shared with other tests - only our own event counts.
                if (body.Contains(Event.MessageId.ToString()))
                    received.TrySetResult((delivery.Exchange, delivery.BasicProperties.Type, body));
                return Task.CompletedTask;
            };
            await channel.BasicConsumeAsync(queue.QueueName, autoAck: true, consumer);

            await using var publisherServices = CreateServices();
            await publisherServices.GetRequiredService<IMessageClient>().PublishAsync(Event);

            var (exchange, type, body) = await received.Task.WaitAsync(Timeout);
            Assert.Equal("chat.message-sent", exchange);
            Assert.Equal("chat.message-sent", type);

            using var json = JsonDocument.Parse(body);
            Assert.Equal(Event.MessageId, json.RootElement.GetProperty("messageId").GetGuid());
            Assert.Equal("Hej", json.RootElement.GetProperty("content").GetString());
            Assert.Equal(Event.SentAt, json.RootElement.GetProperty("sentAt").GetDateTimeOffset());
        }
    }
}

namespace ChatService.Tests.Messaging.OtherTeam
{
    /// <summary>Another team's own representation of the event (tolerant reader).</summary>
    public sealed record MessageSentV1(Guid MessageId, Guid ChatId, string Content);
}
