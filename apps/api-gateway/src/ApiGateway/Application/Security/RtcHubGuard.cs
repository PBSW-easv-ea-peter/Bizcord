using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.WebUtilities;

namespace Bizcord.ApiGateway.Application.Security;

/// <summary>
/// Protects the RTC SignalR hub before Ocelot. Ocelot's WebSocket proxy skips the route's Bearer check,
/// RouteClaimsRequirement and AddQueriesToRequest, so without this anyone could connect as any user
/// by choosing ?userId= themselves (see docs/documentation/east-west-security.md).
/// </summary>
public class RtcHubGuard(RequestDelegate next)
{
    public static readonly PathString Path = "/rtc/hubs";

    private const string UserIdQueryKey = "userId";
    private const string AccessTokenQueryKey = "access_token";

    public Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments(Path))
            return next(context);

        if (context.User.Identity?.IsAuthenticated != true)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        var sub = context.User.FindFirst("sub")?.Value;
        if (!context.User.HasClaim("role", "member") || sub is null)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        // RTC trusts ?userId=, so it must be the token's sub. The token itself is dropped so it does not end up in RTC's request logs.
        // Keys are matched ignoring case, because RTC's Request.Query lookup does: ?UserId=B would otherwise survive and win.
        var query = new QueryBuilder(QueryHelpers.ParseQuery(context.Request.QueryString.Value)
            .Where(p => !string.Equals(p.Key, UserIdQueryKey, StringComparison.OrdinalIgnoreCase)
                     && !string.Equals(p.Key, AccessTokenQueryKey, StringComparison.OrdinalIgnoreCase)))
        {
            { UserIdQueryKey, sub }
        };
        context.Request.QueryString = query.ToQueryString();

        return next(context);
    }
}
