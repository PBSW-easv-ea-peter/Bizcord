using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using RealTimeCommunicationServer.Contracts;
using RealTimeCommunicationServer.Messaging.Handlers;
using RealTimeCommunicationServer.Realtime;

namespace RealTimeCommunicationServer.Tests;

/// <summary>
/// Consumer-kontrakt: det RTC har brug for fra 'chat.message-sent'. Input er JSON, som det står på
/// RabbitMQ (apps/chat-context/docs/contracts.md) - ikke et C#-objekt - så testen fanger også
/// navne- og formatfejl, ikke kun manglende felter. Et ukendt felt er med for at bevise tolerant reader.
/// </summary>
public class MessageSentConsumerContractTests
{
    // Samme options som Program.cs bruger til EasyNetQ.
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
