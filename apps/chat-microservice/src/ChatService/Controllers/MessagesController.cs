using ChatService.Application;
using ChatService.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ChatService.Controllers;

[ApiController]
[Route("chats/{chatId:guid}/messages")]
public sealed class MessagesController(MessageAppService messages) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<MessageResponse>> Send(
        [FromHeader(Name = UserHeader.Name), BindRequired] Guid userId,
        Guid chatId,
        SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        var message = await messages.SendAsync(chatId, userId, request.Content, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, message.ToResponse());
    }

    /// <summary>Newest first. Use the id of the oldest received message as <paramref name="before"/> to page back.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MessageResponse>>> GetPage(
        [FromHeader(Name = UserHeader.Name), BindRequired] Guid userId,
        Guid chatId,
        [FromQuery] Guid? before,
        [FromQuery] int limit = MessageAppService.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var page = await messages.GetPageAsync(chatId, userId, before, limit, cancellationToken);

        return Ok(page.Select(m => m.ToResponse()).ToList());
    }

    /// <summary>Sender only. Replaces the content and sets editedAt.</summary>
    [HttpPut("{messageId:guid}")]
    public async Task<ActionResult<MessageResponse>> Edit(
        [FromHeader(Name = UserHeader.Name), BindRequired] Guid userId,
        Guid chatId,
        Guid messageId,
        EditMessageRequest request,
        CancellationToken cancellationToken)
    {
        var message = await messages.EditAsync(chatId, userId, messageId, request.Content, cancellationToken);

        return Ok(message.ToResponse());
    }

    /// <summary>Sender only. Soft delete - the message stays in the history without content. Idempotent.</summary>
    [HttpDelete("{messageId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(
        [FromHeader(Name = UserHeader.Name), BindRequired] Guid userId,
        Guid chatId,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        await messages.DeleteAsync(chatId, userId, messageId, cancellationToken);

        return NoContent();
    }

    /// <summary>Marks all other users' messages up to and including this one as seen. Idempotent.</summary>
    [HttpPost("{messageId:guid}/seen")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkSeen(
        [FromHeader(Name = UserHeader.Name), BindRequired] Guid userId,
        Guid chatId,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        await messages.MarkSeenUpToAsync(chatId, userId, messageId, cancellationToken);

        return NoContent();
    }

    [HttpGet("{messageId:guid}/receipts")]
    public async Task<ActionResult<IReadOnlyList<ReceiptResponse>>> GetReceipts(
        [FromHeader(Name = UserHeader.Name), BindRequired] Guid userId,
        Guid chatId,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        var receipts = await messages.GetReceiptsAsync(chatId, userId, messageId, cancellationToken);

        return Ok(receipts.Select(r => r.ToResponse()).ToList());
    }
}
