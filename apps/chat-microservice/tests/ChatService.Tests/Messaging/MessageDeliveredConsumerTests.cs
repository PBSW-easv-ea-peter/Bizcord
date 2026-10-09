using System.Net;
using System.Net.Http.Json;
using ChatService.Application;
using ChatService.Contracts;
using ChatService.Tests.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ChatService.Tests.Messaging;

/// <summary>ChatService with a real DB and real RabbitMQ (both Testcontainers) - including MessageDeliveredConsumer.</summary>
public sealed class ChatServiceWithBrokerFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:ChatDb", TestDatabase.ConnectionString);
        builder.UseSetting("ConnectionStrings:RabbitMq", TestBroker.ConnectionString);
    }
}

/// <summary>
/// Service test: an 'rtc.message-delivered' on the broker ends up as deliveredAt in the receipts API.
/// The event is published the way RTC would publish it - RTC itself is not involved.
/// </summary>
[Trait("Category", "Integration")]
public class MessageDeliveredConsumerTests(ChatServiceWithBrokerFactory factory) : IClassFixture<ChatServiceWithBrokerFactory>
{
    private readonly Guid _alice = Guid.NewGuid();
    private readonly Guid _bob = Guid.NewGuid();

    private HttpClient ClientFor(Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", userId.ToString());
        return client;
    }

    [Fact]
    public async Task MessageDelivered_SetsDeliveredAtOnReceipt()
    {
        // Arrange: Alice sends to Bob
        var alice = ClientFor(_alice);
        var createResponse = await alice.PostAsJsonAsync("/chats/direct", new CreateDirectChatRequest(_bob));
        var chat = (await createResponse.Content.ReadFromJsonAsync<ChatResponse>())!;
        var sendResponse = await alice.PostAsJsonAsync($"/chats/{chat.Id}/messages", new SendMessageRequest("Hej Bob"));
        Assert.Equal(HttpStatusCode.Created, sendResponse.StatusCode);
        var message = (await sendResponse.Content.ReadFromJsonAsync<MessageResponse>())!;

        var deliveredAt = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

        // Act: ChatService's MessageDelivered maps to 'rtc.message-delivered' - the same exchange RTC publishes to
        await factory.Services.GetRequiredService<IMessageClient>()
            .PublishAsync(new MessageDelivered(message.Id, [_bob], deliveredAt));

        // Assert: the consumer is asynchronous - poll the receipts API
        var receipt = await PollReceiptAsync(alice, chat.Id, message.Id);
        Assert.Equal(_bob, receipt.UserId);
        Assert.Equal(deliveredAt, receipt.DeliveredAt);
        Assert.Null(receipt.SeenAt);
    }

    private static async Task<ReceiptResponse> PollReceiptAsync(HttpClient client, Guid chatId, Guid messageId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (true)
        {
            var receipts = await client.GetFromJsonAsync<List<ReceiptResponse>>($"/chats/{chatId}/messages/{messageId}/receipts");
            if (receipts is [{ DeliveredAt: not null } receipt])
                return receipt;

            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Receipt was not marked as delivered within 5 seconds.");

            await Task.Delay(50);
        }
    }
}
