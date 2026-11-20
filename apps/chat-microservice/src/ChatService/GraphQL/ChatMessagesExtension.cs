using ChatService.Application;
using ChatService.Contracts;
using ChatService.Controllers;
using HotChocolate.Resolvers;

namespace ChatService.GraphQL;

/// <summary>
/// Adds a <c>messages</c> field to the Chat type without changing the
/// <see cref="ChatResponse"/> contract that REST also uses.
/// The resolver only runs when the client selects the field.
/// </summary>
[ExtendObjectType<ChatResponse>]
public sealed class ChatMessagesExtension
{
    public async Task<IReadOnlyList<MessageResponse>> GetMessages(
    [Parent] ChatResponse chat,
    Guid? before,
    IResolverContext context,
    [Service] MessageAppService messages,
    CancellationToken cancellationToken,
    int limit = MessageAppService.DefaultPageSize)
    {
        var page = await messages.GetPageAsync(
            chat.Id, context.RequireUserId(), before, limit, cancellationToken);

        return page.Select(m => m.ToResponse()).ToList();
    }
}
