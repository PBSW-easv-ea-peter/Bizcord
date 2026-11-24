using System.Net;
using Bizcord.Services;

namespace Bizcord.Web.Tests;

public class GatewayClientTests
{
    private static readonly Guid Alice = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ChatId = Guid.NewGuid();

    private static object Message(int minute) => new
    {
        id = Guid.NewGuid(),
        chatId = ChatId,
        senderUserId = Alice,
        content = $"Besked {minute}",
        sentAt = new DateTimeOffset(2026, 11, 20, 12, minute, 0, TimeSpan.Zero),
        editedAt = (DateTimeOffset?)null,
        deletedAt = (DateTimeOffset?)null,
        attachment = (object?)null
    };

    [Fact]
    public async Task Messages_are_asked_for_one_extra_and_shown_oldest_first()
    {
        var gateway = new FakeGateway();
        // ChatService answers newest first
        gateway.Respond = _ => FakeGateway.Json(new[] { Message(3), Message(2), Message(1) });

        var result = await gateway.CreateClient().GetMessagesAsync(Alice, ChatId, limit: 2);

        Assert.Equal($"/chats/{ChatId}/messages", gateway.Requests[^1].RequestUri!.AbsolutePath);
        Assert.Equal("?limit=3", gateway.Requests[^1].RequestUri!.Query);
        Assert.True(result.Value!.HasOlder);
        Assert.Equal(["Besked 2", "Besked 3"], result.Value.Messages.Select(m => m.Content));
    }

    [Fact]
    public async Task A_short_page_has_no_older_messages()
    {
        var gateway = new FakeGateway { Respond = _ => FakeGateway.Json(new[] { Message(2), Message(1) }) };

        var result = await gateway.CreateClient().GetMessagesAsync(Alice, ChatId, limit: 2);

        Assert.False(result.Value!.HasOlder);
        Assert.Equal(["Besked 1", "Besked 2"], result.Value.Messages.Select(m => m.Content));
    }

    [Fact]
    public async Task Calls_carry_a_faucet_token_for_the_user_and_reuse_it()
    {
        var gateway = new FakeGateway { Respond = _ => FakeGateway.Json(Array.Empty<object>()) };
        var client = gateway.CreateClient();

        await client.GetMessagesAsync(Alice, ChatId);
        await client.GetMessagesAsync(Alice, ChatId);

        var faucet = Assert.Single(gateway.FaucetBodies);
        Assert.Contains($"\"sub\":\"{Alice}\"", faucet);
        Assert.Contains("\"role\":\"member\"", faucet);
        Assert.All(gateway.Requests.Where(r => r.RequestUri!.AbsolutePath != "/Faucet"),
            r => Assert.Equal("Bearer token-1", r.Headers.Authorization!.ToString()));
    }

    [Fact]
    public async Task A_rejected_token_is_replaced_and_the_call_retried_once()
    {
        var gateway = new FakeGateway();
        var calls = 0;
        gateway.Respond = _ => ++calls == 1
            ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
            : FakeGateway.Json(Array.Empty<object>());

        var result = await gateway.CreateClient().GetMessagesAsync(Alice, ChatId);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, gateway.FaucetBodies.Count);
        Assert.Equal("Bearer token-2", gateway.Requests[^1].Headers.Authorization!.ToString());
    }

    [Fact]
    public async Task An_unreachable_gateway_gives_a_connection_error_without_status()
    {
        var gateway = new FakeGateway { Respond = _ => throw new HttpRequestException("refused") };

        var result = await gateway.CreateClient().GetChatAsync(Alice, ChatId);

        Assert.Equal(GatewayClient.ConnectionError, result.Error);
        Assert.Null(result.StatusCode);
    }

    [Fact]
    public async Task A_problem_details_error_is_shown_with_its_detail()
    {
        var gateway = new FakeGateway
        {
            Respond = _ => FakeGateway.Json(new { title = "Forbidden", detail = "Not a participant." }, HttpStatusCode.Forbidden)
        };

        var result = await gateway.CreateClient().GetChatAsync(Alice, ChatId);

        Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
        Assert.Equal("403 Forbidden: Forbidden – Not a participant.", result.Error);
    }
}
