using FileService.Application;
using FileService.Contracts;
using FileService.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace FileService.Controllers;

[ApiController]
public sealed class FilesController(FileAppService files) : ControllerBase
{
    /// <summary>Same header as ChatService - trusted in the MVP, replaced by a UserAuth token later.</summary>
    private const string UserHeader = "X-User-Id";

    /// <summary>
    /// Saga step 1: the file is stored temporarily. No quota is used yet - that happens when the file
    /// is sent in a chat (ChatService's POST /chats/{chatId}/file-messages).
    /// </summary>
    [HttpPost("files")]
    [ProducesResponseType<FileResponse>(StatusCodes.Status201Created)]
    [RequestSizeLimit(110 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 110 * 1024 * 1024)]
    public async Task<ActionResult<FileResponse>> Upload(
        [FromHeader(Name = UserHeader), BindRequired] Guid userId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);

        var stored = await files.UploadAsync(userId, file.FileName, file.ContentType, buffer.ToArray(), cancellationToken);

        return CreatedAtAction(nameof(Download), new { fileId = stored.Id }, ToResponse(stored));
    }

    /// <summary>No access check in the MVP - anyone with the (unguessable) id can download. See README.</summary>
    [HttpGet("files/{fileId:guid}")]
    public async Task<IActionResult> Download(Guid fileId, CancellationToken cancellationToken)
    {
        var result = await files.DownloadAsync(fileId, cancellationToken);

        return result is { } found
            ? File(found.Content, found.File.ContentType, found.File.FileName)
            : Problem(statusCode: StatusCodes.Status404NotFound, detail: $"File '{fileId}' was not found.");
    }

    [HttpGet("quota")]
    public async Task<ActionResult<QuotaResponse>> GetQuota(
        [FromHeader(Name = UserHeader), BindRequired] Guid userId,
        CancellationToken cancellationToken)
    {
        var quota = await files.GetQuotaAsync(userId, cancellationToken);

        return Ok(new QuotaResponse(quota.UserId, quota.Tier, quota.LimitBytes, quota.UsedBytes, quota.RemainingBytes));
    }

    private static FileResponse ToResponse(StoredFile file) =>
        new(file.Id, file.FileName, file.ContentType, file.SizeBytes, file.Status.ToString(), file.UploadedAt);
}
