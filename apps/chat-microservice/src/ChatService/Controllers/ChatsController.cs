using ChatService.Application;
using ChatService.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ChatService.Controllers;

[ApiController]
[Route("chats")]
public sealed class ChatsController(ChatAppService chats) : ControllerBase
{
    /// <summary>Idempotent: 201 for a new chat, 200 if it already exists.</summary>
    [HttpPost("direct")]
    [ProducesResponseType<ChatResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ChatResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ChatResponse>> CreateDirect(
        [FromHeader(Name = UserHeader.Name), BindRequired] Guid userId,
        CreateDirectChatRequest request,
        CancellationToken cancellationToken)
    {
        var (chat, created) = await chats.CreateDirectAsync(userId, request.OtherUserId!.Value, cancellationToken);
        var response = chat.ToResponse();

        return created
            ? CreatedAtAction(nameof(Get), new { chatId = chat.Id }, response)
            : Ok(response);
    }

    [HttpPost("group")]
    [ProducesResponseType<ChatResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ChatResponse>> CreateGroup(
        [FromHeader(Name = UserHeader.Name), BindRequired] Guid userId,
        CreateGroupChatRequest request,
        CancellationToken cancellationToken)
    {
        var chat = await chats.CreateGroupAsync(userId, request.Title, cancellationToken);

        return CreatedAtAction(nameof(Get), new { chatId = chat.Id }, chat.ToResponse());
    }

    [HttpGet("{chatId:guid}")]
    public async Task<ActionResult<ChatResponse>> Get(
        [FromHeader(Name = UserHeader.Name), BindRequired] Guid userId,
        Guid chatId,
        CancellationToken cancellationToken)
    {
        var chat = await chats.GetAsync(chatId, userId, cancellationToken);

        return Ok(chat.ToResponse());
    }

    [HttpPost("{chatId:guid}/participants")]
    [ProducesResponseType<ParticipantResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ParticipantResponse>> AddParticipant(
        [FromHeader(Name = UserHeader.Name), BindRequired] Guid userId,
        Guid chatId,
        AddParticipantRequest request,
        CancellationToken cancellationToken)
    {
        var participant = await chats.AddParticipantAsync(chatId, userId, request.UserId!.Value, cancellationToken);

        // The participant has no resource of its own - Location points to the chat.
        return CreatedAtAction(nameof(Get), new { chatId }, participant.ToResponse());
    }
}
