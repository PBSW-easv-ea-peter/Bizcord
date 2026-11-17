using ChatService.Application;
using ChatService.Contracts;
using ChatService.Controllers;
using HotChocolate.Resolvers;

namespace ChatService.GraphQL;

/// <summary>
/// GraphQL entry point for writes. Same application services as the REST controllers,
/// so validation, authorization and event publishing stay in one place.
/// </summary>

public sealed class ChatMutation
{
    public async Task<MessageResponse> SendMessage(
        Guid chatId,
        string content,
        IResolverContext context,
        [Service] MessageAppService messages,
        CancellationToken cancellationToken)
    {
        var message = await messages.SendAsync(chatId, context.RequireUserId(), content, cancellationToken);
        return message.ToResponse();
    }

    public async Task<MessageResponse> EditMessage(
        Guid chatId,
        Guid messageId,
        string content,
        IResolverContext context,
        [Service] MessageAppService messages,
        CancellationToken cancellationToken
    )
    {
        var message = await messages.EditAsync(chatId, context.RequireUserId(), messageId, content, cancellationToken);
        return message.ToResponse();
    }

    // DeleteAsync returns nothing, but a GraphQL field must return a value.
    // Returning the id lets the client remove the message from its cache.
    public async Task<Guid> DeleteMessage(
        Guid chatId,
        Guid messageId,
        IResolverContext context,
        [Service] MessageAppService messages,
        CancellationToken cancellationToken)
    {
        await messages.DeleteAsync(chatId, context.RequireUserId(), messageId, cancellationToken);
        return messageId;
    }
}