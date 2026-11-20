using ChatService.Application;
using ChatService.Contracts;
using ChatService.Controllers;
using HotChocolate.Resolvers;

namespace ChatService.GraphQL;

/// <summary>
/// GraphQL entry point for reads. 
/// Calls the same application services as the 
/// REST controllers.
/// </summary>
public sealed class ChatQuery
{
    public async Task<ChatResponse> GetChat(
        Guid chatId,
        IResolverContext context,
        [Service] ChatAppService chats,
        CancellationToken cancellationToken)
    {
        var chat = await chats.GetAsync(chatId, context.RequireUserId(), cancellationToken);
        return chat.ToResponse();
    }
}