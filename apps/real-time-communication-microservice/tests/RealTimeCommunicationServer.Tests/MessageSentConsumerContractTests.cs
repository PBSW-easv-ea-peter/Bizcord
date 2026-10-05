using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using RealTimeCommunicationServer.Contracts;
using RealTimeCommunicationServer.Messaging.Handlers;
using RealTimeCommunicationServer.Realtime;

namespace RealTimeCommunicationServer.Tests;

/// <summary>
/// Consumer contract: what RTC needs from 'chat.message-sent'. Input is JSON as it appears on
/// RabbitMQ (apps/chat-context/docs/contracts.md) - not a C# object - so the test also catches
/// naming and format errors, not just missing fields. An unknown field is included to prove tolerant reader.
/// </summary>
public class MessageSentConsumerContractTests
{
    // Same options as Program.cs uses for EasyNetQ.
    private static readonly JsonSerializerOptions WireFormat = new(JsonSerializerDefaults.Web);

    private const string ChatServiceJson =
        """
        {
          "messageId": "0192a3b4-0000-7000-8000-000000000001",
          "chatId": "0192a3b4-0000-7000-8000-000000000002",
          "senderUserId": "0192a3b4-0000-7000-8000-000000000003",
          "content": "Hej",
          "sentAt": "2026-09-28T12:00:00+00:00",
          "recipientUserIds": ["0192a3b4-0000-7000-8000-000000000004"],
          "someFutureField": "ignored"
        }
        """;

    [Fact]
    public void ChatService_wire_format_deserializes_with_every_field_RTC_uses()
    {
        var message = JsonSerializer.Deserialize<MessageSent>(ChatServiceJson, WireFormat)!;

        Assert.Equal(Guid.Parse("0192a3b4-0000-7000-8000-000000000001"), message.MessageId);
        Assert.Equal(Guid.Parse("0192a3b4-0000-7000-8000-000000000002"), message.ChatId);
        Assert.Equal(Guid.Parse("0192a3b4-0000-7000-8000-000000000003"), message.SenderUserId);
        Assert.Equal("Hej", message.Content);
        Assert.Equal(new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero), message.SentAt);
        Assert.Equal([Guid.Parse("0192a3b4-0000-7000-8000-000000000004")], message.RecipientUserIds);
    }

    [Fact]
    public async Task Handler_can_consume_the_ChatService_wire_format()
    {
        var message = JsonSerializer.Deserialize<MessageSent>(ChatServiceJson, WireFormat)!;
        var presence = new PresenceTracker();
        presence.Connected(message.RecipientUserIds[0]);
        var client = new FakeMessageClient();
        var handler = new MessageSentHandler(
            presence, new FakeClientNotifier(), client, TimeProvider.System, NullLogger<MessageSentHandler>.Instance);

        await handler.HandleAsync(message, CancellationToken.None);

        Assert.Single(client.Published);
    }
}
