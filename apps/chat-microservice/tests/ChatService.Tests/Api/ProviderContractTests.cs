using System.Net;
using System.Net.Http.Json;
using ChatService.Contracts;

namespace ChatService.Tests.Api;

[Trait("Category", "Integration")]
public class ProviderContractTests(ChatApiFactory factory) : IClassFixture<ChatApiFactory>
{
    private readonly Guid _alice = Guid.NewGuid();
    private readonly Guid _bob = Guid.NewGuid();
    private readonly Guid _carol = Guid.NewGuid();

    private HttpClient ClientFor(Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", userId.ToString());
        return client;
    }

    [Fact]
    public async Task Send_PublishedMessageSent_MeetsContract()
    {
        // Arrange
        var alice = ClientFor(_alice);
        var bob = ClientFor(_bob);

        // Create group
        var createResponse = await alice.PostAsJsonAsync("/chats/group", new CreateGroupChatRequest("Team"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(createResponse.Headers.Location);
        var chat = (await createResponse.Content.ReadFromJsonAsync<ChatResponse>())!;
        Assert.Equal("Group", chat.Type);

        // Add Bob
        var addResponse = await alice.PostAsJsonAsync($"/chats/{chat.Id}/participants", new AddParticipantRequest(_bob));
        Assert.Equal(HttpStatusCode.Created, addResponse.StatusCode);

        // Add Carol
        addResponse = await alice.PostAsJsonAsync($"/chats/{chat.Id}/participants", new AddParticipantRequest(_carol));
        Assert.Equal(HttpStatusCode.Created, addResponse.StatusCode);

        var content = "Hej Alice";

        // Act

        // Bob sends message. Serveren afrunder til mikrosekunder, så 'before' får 1 sekunds slæk.
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var sendResponse = await bob.PostAsJsonAsync($"/chats/{chat.Id}/messages", new SendMessageRequest(content));
        var after = DateTimeOffset.UtcNow;
        Assert.Equal(HttpStatusCode.Created, sendResponse.StatusCode);
        var message = (await sendResponse.Content.ReadFromJsonAsync<MessageResponse>())!;

        // Find event - Published deles af alle tests i klassen, så find på id frem for Assert.Single
        var published = factory.MessageClient.Published
            .OfType<MessageSent>()
            .Single(e => e.MessageId == message.Id);

        // Assert - sammenlign med input, hvor vi kender værdien uafhængigt af serveren
        Assert.NotEqual(Guid.Empty, published.MessageId);
        Assert.Equal(chat.Id, published.ChatId);
        Assert.Equal(_bob, published.SenderUserId);
        Assert.Equal(content, published.Content);
        Assert.Equal(new[] { _alice, _carol }.Order(), published.RecipientUserIds.Order());

        // Serverens egne værdier: skal matche svaret OG være fornuftige (ikke default)
        Assert.Equal(message.SentAt, published.SentAt);
        Assert.InRange(published.SentAt, before, after);
    }
}
