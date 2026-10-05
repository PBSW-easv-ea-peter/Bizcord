using System.Net;
using System.Net.Http.Json;
using ChatService.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ChatService.Tests.Api;

/// <summary>The full HTTP pipeline against chatDB in a Testcontainer (see TestDatabase).</summary>
[Trait("Category", "Integration")]
public class ChatApiTests(ChatApiFactory factory) : IClassFixture<ChatApiFactory>
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
    public async Task FullFlow_Group_SendSeeAndReadReceipts()
    {
        var alice = ClientFor(_alice);
        var bob = ClientFor(_bob);

        // Create group
        var createResponse = await alice.PostAsJsonAsync("/chats/group", new CreateGroupChatRequest("Team"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(createResponse.Headers.Location);
        var chat = (await createResponse.Content.ReadFromJsonAsync<ChatResponse>())!;
        Assert.Equal("Group", chat.Type);
        Assert.Equal("Owner", Assert.Single(chat.Participants).Role);

        // Add Bob
        var addResponse = await alice.PostAsJsonAsync($"/chats/{chat.Id}/participants", new AddParticipantRequest(_bob));
        Assert.Equal(HttpStatusCode.Created, addResponse.StatusCode);

        // Bob sends
        var sendResponse = await bob.PostAsJsonAsync($"/chats/{chat.Id}/messages", new SendMessageRequest("Hej Alice"));
        Assert.Equal(HttpStatusCode.Created, sendResponse.StatusCode);
        var message = (await sendResponse.Content.ReadFromJsonAsync<MessageResponse>())!;
        Assert.Contains(factory.MessageClient.Published, e => e is MessageSent sent && sent.MessageId == message.Id);

        // Alice fetches messages
        var page = await alice.GetFromJsonAsync<List<MessageResponse>>($"/chats/{chat.Id}/messages");
        Assert.Equal(message.Id, Assert.Single(page!).Id);

        // Alice marks as seen
        var seenResponse = await alice.PostAsync($"/chats/{chat.Id}/messages/{message.Id}/seen", null);
        Assert.Equal(HttpStatusCode.NoContent, seenResponse.StatusCode);

        // Bob sees that Alice has seen it
        var receipts = await bob.GetFromJsonAsync<List<ReceiptResponse>>($"/chats/{chat.Id}/messages/{message.Id}/receipts");
        var receipt = Assert.Single(receipts!);
        Assert.Equal(_alice, receipt.UserId);
        Assert.NotNull(receipt.SeenAt);
    }

    [Fact]
    public async Task CreateDirect_Twice_Returns201ThenExistingWith200()
    {
        var first = await ClientFor(_alice).PostAsJsonAsync("/chats/direct", new CreateDirectChatRequest(_bob));
        var second = await ClientFor(_bob).PostAsJsonAsync("/chats/direct", new CreateDirectChatRequest(_alice));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var firstChat = await first.Content.ReadFromJsonAsync<ChatResponse>();
        var secondChat = await second.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.Equal(firstChat!.Id, secondChat!.Id);
    }

    [Fact]
    public async Task MissingUserHeader_Returns400()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/chats/group", new CreateGroupChatRequest("Team"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingRequiredField_Returns400()
    {
        var response = await ClientFor(_alice).PostAsJsonAsync("/chats/direct", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task InvalidContent_Returns400ProblemDetails()
    {
        var chat = await CreateGroupAsync();

        var response = await ClientFor(_alice).PostAsJsonAsync($"/chats/{chat.Id}/messages", new SendMessageRequest("   "));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UnknownChat_Returns404ProblemDetails()
    {
        var response = await ClientFor(_alice).GetAsync($"/chats/{Guid.NewGuid()}");

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NonParticipant_Returns403ProblemDetails()
    {
        var chat = await CreateGroupAsync();

        var response = await ClientFor(_bob).GetAsync($"/chats/{chat.Id}");

        await AssertProblemAsync(response, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DuplicateParticipant_Returns409ProblemDetails()
    {
        var chat = await CreateGroupAsync();
        var alice = ClientFor(_alice);
        await alice.PostAsJsonAsync($"/chats/{chat.Id}/participants", new AddParticipantRequest(_bob));

        var response = await alice.PostAsJsonAsync($"/chats/{chat.Id}/participants", new AddParticipantRequest(_bob));

        await AssertProblemAsync(response, HttpStatusCode.Conflict);
    }

    private async Task<ChatResponse> CreateGroupAsync()
    {
        var response = await ClientFor(_alice).PostAsJsonAsync("/chats/group", new CreateGroupChatRequest("Team"));
        return (await response.Content.ReadFromJsonAsync<ChatResponse>())!;
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal((int)expected, problem!.Status);
        Assert.False(string.IsNullOrEmpty(problem.Detail));
    }
}
