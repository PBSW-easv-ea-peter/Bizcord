using ChatService.Controllers;
using HotChocolate.AspNetCore;
using HotChocolate.Execution;
using HotChocolate.Resolvers;

namespace ChatService.GraphQL;

/// <summary>
/// Reads the caller from the X-User-Id header once per request, like UserHeader does for REST
/// When JWT replaces the header, this is the only GraphQL class that changes.
/// </summary>
public sealed class UserIdInterceptor : DefaultHttpRequestInterceptor
{
    public const string Key = "userId";

    public override ValueTask OnCreateAsync(
        HttpContext context, 
        IRequestExecutor requestExecutor, 
        OperationRequestBuilder requestBuilder, 
        CancellationToken cancellationToken)
    {
        // No header is not an error here: schema introspecton (the Nitro UI) must work without a user
        // Resolvers that need the user fail on their own, when the state is missing.
        if(Guid.TryParse(context.Request.Headers[UserHeader.Name], out var userId))
            requestBuilder.SetGlobalState(Key, userId);

        return base.OnCreateAsync(context, requestExecutor, requestBuilder, cancellationToken);
    }
}

/// <summary>For resolvers that need a caller. Kept next to the interceptor so header/JWT logic stays in one file.</summary>
public static class ResolverContextUserExtensions
{
    // [GlobalState] fails before the resolver runs when the key is missing, so we look it up ourselves.
    public static Guid RequireUserId(this IResolverContext context) =>
        context.GetGlobalStateOrDefault<Guid?>(UserIdInterceptor.Key)
        ?? throw new GraphQLException(ErrorBuilder.New()
            .SetMessage($"The {UserHeader.Name} header is missing or not a valid id.")
            .SetCode("UNAUTHENTICATED")
            .Build());
}