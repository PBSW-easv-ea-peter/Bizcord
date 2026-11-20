using System.Net.Http.Json;
using System.Text.Json;
using ChatService.Contracts;

namespace ChatService.Tests.Api;

/// <summary>GraphQL through the full HTTP pipeline: interceptor, resolvers and error filter.</summary>
[Trait("Category", "Integration")]
public class GraphQLApiTests(ChatApiFactory factory) : IClassFixture<ChatApiFactory>
{
    private readonly Guid _alice = Guid.NewGuid();
    private readonly Guid _bob = Guid.NewGuid();

    private const string MessagesQuery =
        "query($chatId: UUID!, $limit: Int, $before: UUID) { chat(chatId: $chatId) { messages(limit: $limit, before: $before) { id content deletedAt } } }";

    private HttpClient ClientFor(Guid? userId)
    {
        var client = factory.CreateClient();
        if (userId is not null)
            client.DefaultRequestHeaders.Add("X-User-Id", userId.ToString());
        return client;
    }

    // GraphQL answers 200 for both success and domain errors - the result is in the body.
    private static async Task<JsonElement> PostGraphQL(HttpClient client, string query, object? variables = null)
    {
        var response = await client.PostAsJsonAsync("/graphql", new { query, variables });
        response.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return json.RootElement.Clone();
    }

    // GraphQL can't create chats, so the setup goes through REST.
    private async Task<Guid> CreateGroupAsAlice()
    {
        var response = await ClientFor(_alice).PostAsJsonAsync("/chats/group", new CreateGroupChatRequest("GraphQL"));
        var chat = await response.Content.ReadFromJsonAsync<ChatResponse>();
        return chat!.Id;
    }

    private static async Task<Guid> SendViaGraphQL(HttpClient client, Guid chatId, string content)
    {
        var result = await PostGraphQL(client,
            "mutation($chatId: UUID!, $content: String!) { sendMessage(chatId: $chatId, content: $content) { id } }",
            new { chatId, content });
        return result.GetProperty("data").GetProperty("sendMessage").GetProperty("id").GetGuid();
    }

    private static List<Guid> MessageIds(JsonElement result) =>
        result.GetProperty("data").GetProperty("chat").GetProperty("messages")
            .EnumerateArray().Select(m => m.GetProperty("id").GetGuid()).ToList();

    private static string ErrorCode(JsonElement result) =>
        result.GetProperty("errors")[0].GetProperty("extensions").GetProperty("code").GetString()!;

    [Fact]
    public async Task SendAndRead_ViaGraphQL()
    {
        var chatId = await CreateGroupAsAlice();
        var alice = ClientFor(_alice);

        var sent = await PostGraphQL(alice,
            "mutation($chatId: UUID!, $content: String!) { sendMessage(chatId: $chatId, content: $content) { id } }",
            new { chatId, content = "Hej" });
        var messageId = sent.GetProperty("data").GetProperty("sendMessage").GetProperty("id").GetGuid();

        var read = await PostGraphQL(alice,
            "query($chatId: UUID!) { chat(chatId: $chatId) { messages { id content } } }",
            new { chatId });
        var message = Assert.Single(read.GetProperty("data").GetProperty("chat").GetProperty("messages").EnumerateArray());
        Assert.Equal(messageId, message.GetProperty("id").GetGuid());
        Assert.Equal("Hej", message.GetProperty("content").GetString());

        // Same application service as REST, so the event is published too.
        Assert.Contains(factory.MessageClient.Published, e => e is MessageSent s && s.MessageId == messageId);
    }

    [Fact]
    public async Task Messages_Paginates_WithBefore()
    {
        var chatId = await CreateGroupAsAlice();
        var alice = ClientFor(_alice);
        var sent = new List<Guid>();
        for (var i = 1; i <= 3; i++)
            sent.Add(await SendViaGraphQL(alice, chatId, $"Hej {i}"));

        var page1 = MessageIds(await PostGraphQL(alice, MessagesQuery, new { chatId, limit = 2 }));
        var page2 = MessageIds(await PostGraphQL(alice, MessagesQuery, new { chatId, limit = 2, before = page1[^1] }));

        // Newest first: page 1 is messages 3 and 2, page 2 is message 1.
        Assert.Equal(new[] { sent[2], sent[1] }, page1);
        Assert.Equal(new[] { sent[0] }, page2);
    }

    [Fact]
    public async Task MissingHeader_ReturnsUnauthenticated()
    {
        var chatId = await CreateGroupAsAlice();

        var result = await PostGraphQL(ClientFor(null), MessagesQuery, new { chatId });

        Assert.Equal("UNAUTHENTICATED", ErrorCode(result));
        Assert.Equal(JsonValueKind.Null, result.GetProperty("data").ValueKind);
    }

    [Fact]
    public async Task NonParticipant_ReturnsForbidden()
    {
        var chatId = await CreateGroupAsAlice();

        var result = await PostGraphQL(ClientFor(_bob), MessagesQuery, new { chatId });

        Assert.Equal("FORBIDDEN", ErrorCode(result));
        Assert.Equal(JsonValueKind.Null, result.GetProperty("data").ValueKind);
    }

    [Fact]
    public async Task DeleteMessage_ReturnsId_AndHidesContent()
    {
        var chatId = await CreateGroupAsAlice();
        var alice = ClientFor(_alice);
        var messageId = await SendViaGraphQL(alice, chatId, "Hej");

        var deleted = await PostGraphQL(alice,
            "mutation($chatId: UUID!, $messageId: UUID!) { deleteMessage(chatId: $chatId, messageId: $messageId) }",
            new { chatId, messageId });
        Assert.Equal(messageId, deleted.GetProperty("data").GetProperty("deleteMessage").GetGuid());

        // Soft delete: the message stays in the list, but without content.
        var read = await PostGraphQL(alice, MessagesQuery, new { chatId });
        var message = Assert.Single(read.GetProperty("data").GetProperty("chat").GetProperty("messages").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, message.GetProperty("content").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, message.GetProperty("deletedAt").ValueKind);
    }
}
