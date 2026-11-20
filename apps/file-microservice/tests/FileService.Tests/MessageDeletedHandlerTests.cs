using System.Text.Json;
using FileService.Application;
using FileService.Contracts;
using FileService.Infrastructure.Messaging;
using FileService.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace FileService.Tests;

[Trait("Category", "Integration")]
public class MessageDeletedHandlerTests
{
    private const long Limit = 1000;

    private readonly DapperFileRepository _files = new(TestDatabase.DataSource);
    private readonly QuotaSettings _settings = new() { StandardLimitBytes = Limit, MaxUploadBytes = 10_000 };
    private readonly MessageDeletedHandler _handler;
    private readonly FileAppService _app;
    private readonly QuotaSagaParticipant _participant;
    private readonly Guid _user = Guid.NewGuid();

    public MessageDeletedHandlerTests()
    {
        _handler = new MessageDeletedHandler(_files, NullLogger<MessageDeletedHandler>.Instance);
        _app = new FileAppService(_files, _settings, TimeProvider.System, NullLogger<FileAppService>.Instance);
        _participant = new QuotaSagaParticipant(_files, new InMemoryMessageClient(), _settings, NullLogger<QuotaSagaParticipant>.Instance);
    }

    private async Task<Guid> AttachedFileAsync(int bytes)
    {
        var file = await _app.UploadAsync(_user, "video.mp4", "video/mp4", new byte[bytes]);
        await _participant.HandleAsync(new ReserveQuota(Guid.NewGuid(), file.Id, _user));
        return file.Id;
    }

    [Fact]
    public async Task MessageWithFileDeleted_DeletesFileAndFreesQuota()
    {
        var fileId = await AttachedFileAsync(600);

        await _handler.HandleAsync(new MessageDeleted(Guid.NewGuid(), fileId));

        Assert.Null(await _files.GetAsync(fileId));
        Assert.Equal(0, (await _files.GetQuotaAsync(_user, Limit)).UsedBytes);
    }

    [Fact]
    public async Task SameEventTwice_FreesTheQuotaOnlyOnce()
    {
        var keep = await AttachedFileAsync(300);
        var delete = await AttachedFileAsync(600);

        await _handler.HandleAsync(new MessageDeleted(Guid.NewGuid(), delete));
        await _handler.HandleAsync(new MessageDeleted(Guid.NewGuid(), delete));

        Assert.Equal(300, (await _files.GetQuotaAsync(_user, Limit)).UsedBytes);
        Assert.NotNull(await _files.GetAsync(keep));
    }

    [Fact]
    public async Task TemporaryFile_IsLeftForTheSaga()
    {
        var file = await _app.UploadAsync(_user, "video.mp4", "video/mp4", new byte[100]);

        await _handler.HandleAsync(new MessageDeleted(Guid.NewGuid(), file.Id));

        Assert.NotNull(await _files.GetAsync(file.Id));
    }

    [Fact]
    public void ChatService_wire_format_deserializes_including_messages_without_a_file()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var withFile = JsonSerializer.Deserialize<MessageDeleted>(
            """
            { "messageId": "0192a3b4-0000-7000-8000-000000000001", "chatId": "0192a3b4-0000-7000-8000-000000000002",
              "deletedAt": "2026-11-18T12:00:00+00:00", "recipientUserIds": [],
              "attachmentFileId": "0192a3b4-0000-7000-8000-000000000003" }
            """, options)!;
        var withoutFile = JsonSerializer.Deserialize<MessageDeleted>(
            """{ "messageId": "0192a3b4-0000-7000-8000-000000000001", "chatId": "…", "deletedAt": "…", "recipientUserIds": [] }""", options)!;

        Assert.Equal(Guid.Parse("0192a3b4-0000-7000-8000-000000000003"), withFile.AttachmentFileId);
        Assert.Null(withoutFile.AttachmentFileId);
        Assert.Equal("chat.message-deleted", new MessageNameSerializer().Serialize(typeof(MessageDeleted)));
    }
}
