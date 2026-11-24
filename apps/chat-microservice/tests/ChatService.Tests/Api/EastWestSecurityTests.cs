using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChatService.Contracts;

namespace ChatService.Tests.Api;

/// <summary>
/// Week 45: ChatService validates the token itself, so a caller that bypasses the gateway is still rejected,
/// and the user is taken from the token - not from a header the caller controls.
/// </summary>
[Trait("Category", "Integration")]
public class EastWestSecurityTests(ChatApiFactory factory) : IClassFixture<ChatApiFactory>
{
    private HttpClient ClientWith(string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Health_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Health_WithMemberToken_Returns403()
    {
        var response = await ClientWith(TestTokens.Create(Guid.NewGuid().ToString())).GetAsync("/health");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Health_WithInternalServiceToken_Returns200()
    {
        var response = await ClientWith(TestTokens.Create("chat-health-probe", role: "internal-service")).GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UserEndpoint_WithInternalServiceToken_Returns403()
    {
        var token = TestTokens.Create(Guid.NewGuid().ToString(), role: "internal-service");

        var response = await ClientWith(token).PostAsJsonAsync("/chats/group", new CreateGroupChatRequest("Team"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TokenSignedWithAnotherKey_Returns401()
    {
        var token = TestTokens.Create(Guid.NewGuid().ToString(), signingKey: "another-key-that-the-service-does-not-know-0123456789");

        var response = await ClientWith(token).PostAsJsonAsync("/chats/group", new CreateGroupChatRequest("Team"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ForgedUserHeader_IsIgnored_TheTokenDecidesWhoActs()
    {
        var alice = Guid.NewGuid();
        var mallory = Guid.NewGuid();
        var client = factory.CreateClient().AsMember(alice);
        client.DefaultRequestHeaders.Add("X-User-Id", mallory.ToString());

        var response = await client.PostAsJsonAsync("/chats/group", new CreateGroupChatRequest("Team"));

        response.EnsureSuccessStatusCode();
        var chat = await response.Content.ReadFromJsonAsync<ChatResponse>();
        var participants = chat!.Participants.Select(p => p.UserId).ToList();
        Assert.Contains(alice, participants);
        Assert.DoesNotContain(mallory, participants);
    }
}
