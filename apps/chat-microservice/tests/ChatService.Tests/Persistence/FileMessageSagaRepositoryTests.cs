using ChatService.Domain;
using ChatService.Infrastructure.Persistence;

namespace ChatService.Tests.Persistence;

[Trait("Category", "Integration")]
public class FileMessageSagaRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

    private readonly DapperChatRepository _chats = new(TestDatabase.Db);
    private readonly DapperMessageRepository _messages = new(TestDatabase.Db);
    private readonly DapperFileMessageSagaRepository _sagas = new(TestDatabase.Db);

    private async Task<(Chat Chat, Guid Sender, FileMessageSaga Saga)> StartSagaAsync(string? content = null)
    {
        var sender = Guid.NewGuid();
        var chat = Chat.CreateDirect(sender, Guid.NewGuid(), Now);
        await _chats.AddAsync(chat);

        var saga = FileMessageSaga.Start(chat, sender, Guid.NewGuid(), content is null ? null : new MessageContent(content), Now);
        await _sagas.AddAsync(saga);
        return (chat, sender, saga);
    }

    [Fact]
    public async Task AddAsync_ThenGetAsync_RoundTripsSaga()
    {
        var (_, _, saga) = await StartSagaAsync("Hej");

        var loaded = await _sagas.GetAsync(saga.SagaId);

        Assert.NotNull(loaded);
        Assert.Equal(saga.FileId, loaded.FileId);
        Assert.Equal(new MessageContent("Hej"), loaded.Content);
        Assert.Equal(FileMessageSagaStatus.AwaitingQuota, loaded.Status);
        Assert.Equal(Now, loaded.StartedAt);
    }

    [Fact]
    public async Task CompleteWithMessageAsync_SavesMessageAndSagaTogether()
    {
        var (chat, sender, saga) = await StartSagaAsync();
        var message = Message.SendWithAttachment(chat, sender, null, new Attachment(saga.FileId, "video.mp4", 42), Now);
        saga.Complete(message.Id, Now);

        Assert.True(await _sagas.CompleteWithMessageAsync(saga, FileMessageSagaStatus.AwaitingQuota, message));

        var loadedMessage = await _messages.GetAsync(message.Id);
        Assert.NotNull(loadedMessage);
        Assert.Null(loadedMessage.Content);
        Assert.Equal(new Attachment(saga.FileId, "video.mp4", 42), loadedMessage.Attachment);
        var loadedSaga = (await _sagas.GetAsync(saga.SagaId))!;
        Assert.Equal(FileMessageSagaStatus.Completed, loadedSaga.Status);
        Assert.Equal(Now, loadedSaga.CompletedAt);
    }

    [Fact]
    public async Task CompleteWithMessageAsync_WhenStatusChangedMeanwhile_SavesNothing()
    {
        var (chat, sender, saga) = await StartSagaAsync();

        // The timeout job gets there first.
        var timedOut = (await _sagas.GetAsync(saga.SagaId))!;
        timedOut.StartCompensation("Timed out.", Now);
        Assert.True(await _sagas.UpdateAsync(timedOut, FileMessageSagaStatus.AwaitingQuota));

        var message = Message.SendWithAttachment(chat, sender, null, new Attachment(saga.FileId, "video.mp4", 42), Now);
        saga.Complete(message.Id, Now);

        Assert.False(await _sagas.CompleteWithMessageAsync(saga, FileMessageSagaStatus.AwaitingQuota, message));
        Assert.Null(await _messages.GetAsync(message.Id));
        Assert.Equal(FileMessageSagaStatus.Compensating, (await _sagas.GetAsync(saga.SagaId))!.Status);
    }

    [Fact]
    public async Task GetStaleAsync_ReturnsOnlyWaitingSagasOlderThanTheCutoff()
    {
        var (_, _, waiting) = await StartSagaAsync();
        var (_, _, finished) = await StartSagaAsync();
        finished.Reject("No.", Now);
        await _sagas.UpdateAsync(finished, FileMessageSagaStatus.AwaitingQuota);

        var stale = await _sagas.GetStaleAsync(Now.AddSeconds(1), limit: 1000);

        Assert.Contains(stale, s => s.SagaId == waiting.SagaId);
        Assert.DoesNotContain(stale, s => s.SagaId == finished.SagaId);
        Assert.DoesNotContain(await _sagas.GetStaleAsync(Now, limit: 1000), s => s.SagaId == waiting.SagaId);
    }
}
