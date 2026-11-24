using System.Security.Claims;
using Bizcord.ApiGateway.Application.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace ApiGateway.Tests;

public class RtcHubGuardTests
{
    private const string UserA = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
    private const string UserB = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";

    [Fact]
    public async Task Hub_without_authenticated_user_returns_401()
    {
        var (context, forwarded) = await Run("/rtc/hubs/chat", $"?userId={UserB}", user: null);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.False(forwarded());
    }

    [Fact]
    public async Task Hub_with_other_role_returns_403()
    {
        var (context, forwarded) = await Run("/rtc/hubs/chat", "", User(UserA, "guest"));

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.False(forwarded());
    }

    [Fact]
    public async Task Hub_member_is_forwarded_as_the_tokens_sub_not_the_clients_userId()
    {
        var (context, forwarded) = await Run("/rtc/hubs/chat", $"?userId={UserB}&userId={UserB}&id=conn1", User(UserA, "member"));

        Assert.True(forwarded());
        var query = QueryHelpers.ParseQuery(context.Request.QueryString.Value);
        Assert.Equal(UserA, Assert.Single(query["userId"]));
        // SignalR's own parameters (here the connection id) must survive the rewrite.
        Assert.Equal("conn1", query["id"].ToString());
    }

    [Fact]
    public async Task Hub_access_token_is_not_forwarded_to_RTC()
    {
        var (context, forwarded) = await Run("/rtc/hubs/chat", "?access_token=secret-token", User(UserA, "member"));

        Assert.True(forwarded());
        Assert.False(context.Request.Query.ContainsKey("access_token"));
    }

    [Fact]
    public async Task Other_paths_are_left_untouched()
    {
        var (context, forwarded) = await Run("/chats", $"?userId={UserB}", user: null);

        Assert.True(forwarded());
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal($"?userId={UserB}", context.Request.QueryString.Value);
    }

    private static ClaimsPrincipal User(string sub, string role) =>
        new(new ClaimsIdentity([new Claim("sub", sub), new Claim("role", role)], "Bearer", "sub", "role"));

    private static async Task<(HttpContext Context, Func<bool> Forwarded)> Run(string path, string query, ClaimsPrincipal? user)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(query);
        if (user is not null)
            context.User = user;

        var called = false;
        var guard = new RtcHubGuard(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });

        await guard.InvokeAsync(context);
        return (context, () => called);
    }
}
