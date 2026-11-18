using ChatService.Application;
using ChatService.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ChatService.Controllers;

/// <summary>
/// Sending a message with a file is a saga across ChatService and FileService, so it is asynchronous:
/// POST starts it and answers 202 Accepted, GET shows how far it has come.
/// </summary>
[ApiController]
[Route("chats/{chatId:guid}/file-messages")]
public sealed class FileMessagesController(FileMessageSagaOrchestrator orchestrator) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<FileMessageResponse>(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<FileMessageResponse>> Send(
        [FromHeader(Name = UserHeader.Name), BindRequired] Guid userId,
        Guid chatId,
        SendFileMessageRequest request,
        CancellationToken cancellationToken)
    {
        var saga = await orchestrator.StartAsync(chatId, userId, request.FileId!.Value, request.Content, cancellationToken);

        return AcceptedAtAction(nameof(Get), new { chatId, sagaId = saga.SagaId }, saga.ToResponse());
    }

    /// <summary>Sender only. When Status is Completed, MessageId points to the saved message.</summary>
    [HttpGet("{sagaId:guid}")]
    public async Task<ActionResult<FileMessageResponse>> Get(
        [FromHeader(Name = UserHeader.Name), BindRequired] Guid userId,
        Guid chatId,
        Guid sagaId,
        CancellationToken cancellationToken)
    {
        var saga = await orchestrator.GetAsync(chatId, sagaId, userId, cancellationToken);

        return Ok(saga.ToResponse());
    }
}
