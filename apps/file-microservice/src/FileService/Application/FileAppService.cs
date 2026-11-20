using Bizcord.Logging;
using FileService.Domain;

namespace FileService.Application;

/// <summary>Upload and download. Uploading only stores the file temporarily - quota is reserved later, by the saga.</summary>
public sealed class FileAppService(
    IFileRepository files,
    QuotaSettings settings,
    TimeProvider time,
    ILogger<FileAppService> logger)
{
    public async Task<StoredFile> UploadAsync(Guid userId, string? fileName, string? contentType, byte[] content, CancellationToken cancellationToken = default)
    {
        if (content.Length == 0)
            throw new FileDomainException("The file is empty.");

        if (content.Length > settings.MaxUploadBytes)
            throw new FileDomainException($"The file is larger than the upload limit of {settings.MaxUploadBytes} bytes.");

        var now = time.GetUtcNow();
        var file = new StoredFile(
            Guid.CreateVersion7(now),
            userId,
            StoredFile.CleanFileName(fileName),
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            content.Length,
            FileStatus.Temporary,
            SagaId: null,
            now);

        await files.AddTemporaryAsync(file, content, cancellationToken);
        logger.Information("File uploaded (temporary).", new { FileId = file.Id, file.SizeBytes });

        return file;
    }

    public async Task<(StoredFile File, byte[] Content)?> DownloadAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        var file = await files.GetAsync(fileId, cancellationToken);
        if (file is null)
            return null;

        var content = await files.GetContentAsync(fileId, cancellationToken);
        return content is null ? null : (file, content);
    }

    public Task<UserQuota> GetQuotaAsync(Guid userId, CancellationToken cancellationToken = default) =>
        files.GetQuotaAsync(userId, settings.StandardLimitBytes, cancellationToken);
}

/// <summary>From the "Quota" section in appsettings.</summary>
public sealed class QuotaSettings
{
    public long StandardLimitBytes { get; init; } = 20 * 1024 * 1024;
    public long MaxUploadBytes { get; init; } = 100 * 1024 * 1024;
}
