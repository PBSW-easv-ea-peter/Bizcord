using System.Text.Json;
using ChatService.Contracts;

namespace ChatService.Tests.Messaging;

/// <summary>
/// Consumer-kontrakt: det ChatService har brug for fra RTC's 'rtc.message-delivered'.
/// Input er JSON, som det står på RabbitMQ (real-time-communication-microservice/docs/contracts.md),
/// inkl. felter vi ikke bruger (chatId) og et ukendt felt - tolerant reader.
/// </summary>
public class MessageDeliveredConsumerContractTests
{
    // Samme options som AddChatMessaging bruger til EasyNetQ.
    private static readonly JsonSerializerOptions WireFormat = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Rtc_wire_format_deserializes_with_every_field_ChatService_uses()
    {
        const string rtcJson =
            """
            {
              "messageId": "0192a3b4-0000-7000-8000-000000000001",
              "chatId": "0192a3b4-0000-7000-8000-000000000002",
              "deliveredToUserIds": ["0192a3b4-0000-7000-8000-000000000004"],
              "deliveredAt": "2026-10-05T12:00:00+00:00",
              "someFutureField": "ignored"
            }
            """;

        var message = JsonSerializer.Deserialize<MessageDelivered>(rtcJson, WireFormat)!;

        Assert.Equal(Guid.Parse("0192a3b4-0000-7000-8000-000000000001"), message.MessageId);
        Assert.Equal([Guid.Parse("0192a3b4-0000-7000-8000-000000000004")], message.DeliveredToUserIds);
        Assert.Equal(new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero), message.DeliveredAt);
    }
}
