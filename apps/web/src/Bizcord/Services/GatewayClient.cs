using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Bizcord.Services;

/// <summary>
/// Typed HttpClient for the API gateway - the frontend never calls a service directly.
/// Every call is made as a user: a JWT (sub = user id, role = member) from the gateway's dev faucet is sent as Bearer,
/// and the gateway turns the sub claim into ChatService's X-User-Id header.
/// Errors from the services are ProblemDetails and are turned into a readable message.
/// </summary>
public class GatewayClient(HttpClient http, GatewayTokenCache tokens)
{
    internal const string ConnectionError = "Kunne ikke få forbindelse til API-gatewayen. Kører den?";

    /// <summary>ChatService's maximum page size.</summary>
    private const int MaxPageSize = 100;

    // GET chats/{chatId} - the chat with its participants (participants only, otherwise 403).
    public Task<GatewayResult<ChatDto>> GetChatAsync(Guid userId, Guid chatId, CancellationToken ct = default) =>
        SendAsync<ChatDto>(userId, () => new HttpRequestMessage(HttpMethod.Get, $"chats/{chatId}"), ct);

    /// <summary>
    /// GET chats/{chatId}/messages - ChatService returns newest first; the chat shows oldest first.
    /// Asks for one message more than the page to know whether older messages exist.
    /// </summary>
    public async Task<GatewayResult<MessagePage>> GetMessagesAsync(Guid userId, Guid chatId, Guid? before = null, int limit = 30, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, MaxPageSize - 1);
        var url = $"chats/{chatId}/messages?limit={limit + 1}";
        if (before is not null)
        {
            url += $"&before={before}";
        }

        var result = await SendAsync<List<MessageDto>>(userId, () => new HttpRequestMessage(HttpMethod.Get, url), ct);
        if (!result.IsSuccess)
        {
            return GatewayResult<MessagePage>.Fail(result.Error!, result.StatusCode);
        }

        var newestFirst = result.Value!;
        var page = newestFirst.Take(limit).Reverse().ToList();
        return GatewayResult<MessagePage>.Ok(new MessagePage(page, HasOlder: newestFirst.Count > limit));
    }

    // POST chats/direct - idempotent: an existing direct chat between the two is returned (200) instead of a new one.
    public Task<GatewayResult<ChatDto>> CreateDirectChatAsync(Guid userId, Guid otherUserId, CancellationToken ct = default) =>
        SendAsync<ChatDto>(userId, () => Json(HttpMethod.Post, "chats/direct", new { otherUserId }), ct);

    // POST chats/group - the creator becomes Owner; members are added afterwards.
    public Task<GatewayResult<ChatDto>> CreateGroupChatAsync(Guid userId, string title, CancellationToken ct = default) =>
        SendAsync<ChatDto>(userId, () => Json(HttpMethod.Post, "chats/group", new { title }), ct);

    // POST chats/{chatId}/participants
    public Task<GatewayResult> AddParticipantAsync(Guid userId, Guid chatId, Guid newUserId, CancellationToken ct = default) =>
        SendAsync(userId, () => Json(HttpMethod.Post, $"chats/{chatId}/participants", new { userId = newUserId }), ct);

    // POST chats/{chatId}/messages
    public Task<GatewayResult> SendMessageAsync(Guid userId, Guid chatId, string content, CancellationToken ct = default) =>
        SendAsync(userId, () => Json(HttpMethod.Post, $"chats/{chatId}/messages", new { content }), ct);

    // PUT chats/{chatId}/messages/{messageId} - sender only.
    public Task<GatewayResult> EditMessageAsync(Guid userId, Guid chatId, Guid messageId, string content, CancellationToken ct = default) =>
        SendAsync(userId, () => Json(HttpMethod.Put, $"chats/{chatId}/messages/{messageId}", new { content }), ct);

    // DELETE chats/{chatId}/messages/{messageId} - sender only, soft delete.
    public Task<GatewayResult> DeleteMessageAsync(Guid userId, Guid chatId, Guid messageId, CancellationToken ct = default) =>
        SendAsync(userId, () => new HttpRequestMessage(HttpMethod.Delete, $"chats/{chatId}/messages/{messageId}"), ct);

    private async Task<GatewayResult<T>> SendAsync<T>(Guid userId, Func<HttpRequestMessage> createRequest, CancellationToken ct)
    {
        try
        {
            using var response = await SendAuthorizedAsync(userId, createRequest, ct);
            if (!response.IsSuccessStatusCode)
            {
                return GatewayResult<T>.Fail(await ReadErrorAsync(response, ct), response.StatusCode);
            }

            var value = await response.Content.ReadFromJsonAsync<T>(ct);
            return value is null
                ? GatewayResult<T>.Fail("Tomt svar fra gatewayen.", response.StatusCode)
                : GatewayResult<T>.Ok(value);
        }
        catch (HttpRequestException)
        {
            return GatewayResult<T>.Fail(ConnectionError);
        }
    }

    private async Task<GatewayResult> SendAsync(Guid userId, Func<HttpRequestMessage> createRequest, CancellationToken ct)
    {
        try
        {
            using var response = await SendAuthorizedAsync(userId, createRequest, ct);
            return response.IsSuccessStatusCode
                ? GatewayResult.Ok()
                : GatewayResult.Fail(await ReadErrorAsync(response, ct), response.StatusCode);
        }
        catch (HttpRequestException)
        {
            return GatewayResult.Fail(ConnectionError);
        }
    }

    // A request can only be sent once, hence the factory: a 401 retries once with a fresh token,
    // because a cached token dies when the gateway restarts with a new signing key.
    private async Task<HttpResponseMessage> SendAuthorizedAsync(Guid userId, Func<HttpRequestMessage> createRequest, CancellationToken ct)
    {
        var response = await SendWithTokenAsync(userId, createRequest, ct);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        response.Dispose();
        tokens.Remove(userId);
        return await SendWithTokenAsync(userId, createRequest, ct);
    }

    private async Task<HttpResponseMessage> SendWithTokenAsync(Guid userId, Func<HttpRequestMessage> createRequest, CancellationToken ct)
    {
        using var request = createRequest();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenAsync(userId, ct));
        return await http.SendAsync(request, ct);
    }

    // Dev faucet in the gateway (POST /Faucet). Stands in for a real login until there is a UserAuth service.
    private async Task<string> GetTokenAsync(Guid userId, CancellationToken ct)
    {
        if (tokens.TryGet(userId, out var cached))
        {
            return cached;
        }

        var claims = new Dictionary<string, string> { ["sub"] = userId.ToString(), ["role"] = "member" };
        using var response = await http.PostAsJsonAsync("Faucet", new { claims }, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"The gateway's faucet answered {(int)response.StatusCode}.");
        }

        var token = await response.Content.ReadFromJsonAsync<FaucetToken>(ct);
        if (string.IsNullOrEmpty(token?.Value))
        {
            throw new HttpRequestException("The gateway's faucet returned no token.");
        }

        tokens.Set(userId, token.Value);
        return token.Value;
    }

    private static HttpRequestMessage Json(HttpMethod method, string url, object body) =>
        new(method, url) { Content = JsonContent.Create(body) };

    // Status code + title/detail/validation errors from ProblemDetails, if there are any.
    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var status = $"{(int)response.StatusCode} {response.ReasonPhrase}";

        // Ocelot answers e.g. 404 without a body when no route matches, and 401/403 when the token or role is wrong.
        if (response.Content.Headers.ContentLength == 0)
        {
            return response.StatusCode switch
            {
                HttpStatusCode.NotFound => $"{status} (intet svar – mangler der en route i gatewayen?)",
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => $"{status} (gatewayen afviste kaldet)",
                _ => status
            };
        }

        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(ct);
            if (problem is null)
            {
                return status;
            }

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(problem.Title)) parts.Add(problem.Title);
            if (!string.IsNullOrWhiteSpace(problem.Detail)) parts.Add(problem.Detail);
            parts.AddRange(problem.Errors.SelectMany(e => e.Value.Select(msg => $"{e.Key}: {msg}")));

            return parts.Count == 0 ? status : $"{status}: {string.Join(" – ", parts)}";
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            // Not ProblemDetails (e.g. text or HTML) - the status code is all we can show.
            return status;
        }
    }

    private sealed record FaucetToken(string? Value);
}
