using ChatService.Application;
using ChatService.Contracts;
using ChatService.Domain;
using Microsoft.Extensions.Logging.Abstractions;

namespace ChatService.Tests.Application;

public class FileMessageSagaOrchestratorTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    private readonly InMemoryChatRepository _chats = new();
    private readonly InMemoryFileMessageSagaRepository _sagas = new();
    private readonly InMemoryMessageClient _messageClient = new();
    private readonly AdjustableTimeProvider _time = new(Now);
    private readonly FileMessageSagaOrchestrator _orchestrator;

    private readonly Guid _alice = Guid.NewGuid();
    private readonly Guid _bob = Guid.NewGuid();
    private readonly Guid _fileId = Guid.NewGuid();
    private readonly Chat _chat;

    public FileMessageSagaOrchestratorTests()
    {
        _orchestrator = new FileMessageSagaOrchestrator(_chats, _sagas, _messageClient, _time, NullLogger<FileMessageSagaOrchestrator>.Instance);
        _chat = Chat.CreateDirect(_alice, _bob, Now);
        _chats.Chats.Add(_chat.Id, _chat);
    }

    private async Task<FileMessageSaga> StartAsync(string? content = "Here is the video")
    {
        var saga = await _orchestrator.StartAsync(_chat.Id, _alice, _fileId, content);
        _messageClient.Published.Clear();
        return saga;
    }

    private QuotaReserved Reserved(FileMessageSaga saga) => new(saga.SagaId, _fileId, "video.mp4", 15_000_000);

    [Fact]
    public async Task Start_SavesSagaAndSendsReserveQuota()
    {
        var saga = await _orchestrator.StartAsync(_chat.Id, _alice, _fileId, "Here is the video");

        Assert.Equal(FileMessageSagaStatus.AwaitingQuota, _sagas.SavedStatus(saga.SagaId));
        var command = Assert.IsType<ReserveQuota>(Assert.Single(_messageClient.Published));
        Assert.Equal(new ReserveQuota(saga.SagaId, _fileId, _alice), command);
    }

    [Fact]
    public async Task Start_ByNonParticipant_IsNotAllowed_AndSendsNothing()
    {
        await Assert.ThrowsAsync<NotAllowedException>(() => _orchestrator.StartAsync(_chat.Id, Guid.NewGuid(), _fileId, null));

        Assert.Empty(_messageClient.Published);
    }

    [Fact]
    public async Task QuotaReserved_SavesMessageWithAttachment_CompletesSaga_AndPublishesMessageSent()
    {
        var saga = await StartAsync();

        await _orchestrator.HandleAsync(Reserved(saga));

        var message = Assert.Single(_sagas.SavedMessages);
        Assert.Equal(new Attachment(_fileId, "video.mp4", 15_000_000), message.Attachment);
        Assert.Equal(FileMessageSagaStatus.Completed, _sagas.SavedStatus(saga.SagaId));
        Assert.Equal(message.Id, saga.MessageId);

        var sent = Assert.IsType<MessageSent>(Assert.Single(_messageClient.Published));
        Assert.Equal(message.Id, sent.MessageId);
        Assert.Equal("Here is the video", sent.Content);
        Assert.Equal([_bob], sent.RecipientUserIds);
        Assert.Equal(new AttachmentInfo(_fileId, "video.mp4", 15_000_000), sent.Attachment);
    }

    [Fact]
    public async Task QuotaReserved_WithoutText_SendsFileOnlyMessage()
    {
        var saga = await StartAsync(content: "  ");

        await _orchestrator.HandleAsync(Reserved(saga));

        Assert.Null(Assert.Single(_sagas.SavedMessages).Content);
        Assert.Null(Assert.IsType<MessageSent>(Assert.Single(_messageClient.Published)).Content);
    }

    [Fact]
    public async Task QuotaReserved_WhenMessageCannotBeSaved_CompensatesWithReleaseQuota()
    {
        var saga = await StartAsync();
        _chats.Chats.Remove(_chat.Id); // The chat is gone while FileService was working.

        await _orchestrator.HandleAsync(Reserved(saga));

        Assert.Empty(_sagas.SavedMessages);
        Assert.Equal(FileMessageSagaStatus.Compensating, _sagas.SavedStatus(saga.SagaId));
        var command = Assert.IsType<ReleaseQuota>(Assert.Single(_messageClient.Published));
        Assert.Equal(new ReleaseQuota(saga.SagaId, _fileId, _alice), command);
    }

    [Fact]
    public async Task QuotaReserved_WhenTimeoutWonTheRace_SavesNoMessageAndPublishesNothing()
    {
        var saga = await StartAsync();
        _sagas.LoseNextUpdate = true;

        await _orchestrator.HandleAsync(Reserved(saga));

        Assert.Empty(_sagas.SavedMessages);
        Assert.Empty(_messageClient.Published);
    }

    [Fact]
    public async Task QuotaRejected_FailsSaga_WithNothingToCompensate()
    {
        var saga = await StartAsync();

        await _orchestrator.HandleAsync(new QuotaRejected(saga.SagaId, _fileId, "Storage quota exceeded."));

        Assert.Equal(FileMessageSagaStatus.Failed, _sagas.SavedStatus(saga.SagaId));
        Assert.Equal("Storage quota exceeded.", saga.FailureReason);
        Assert.Empty(_messageClient.Published);
        Assert.Empty(_sagas.SavedMessages);
    }

    [Fact]
    public async Task QuotaReleased_WhileCompensating_FailsSaga()
    {
        var saga = await StartAsync();
        _chats.Chats.Remove(_chat.Id);
        await _orchestrator.HandleAsync(Reserved(saga));

        await _orchestrator.HandleAsync(new QuotaReleased(saga.SagaId, _fileId));

        Assert.Equal(FileMessageSagaStatus.Failed, _sagas.SavedStatus(saga.SagaId));
    }

    [Fact]
    public async Task Timeout_WhileAwaitingQuota_CompensatesWithReleaseQuota()
    {
        var saga = await StartAsync();
        _time.Now = Now + Timeout + TimeSpan.FromSeconds(1);

        await _orchestrator.TimeOutStaleAsync(Timeout);

        Assert.Equal(FileMessageSagaStatus.Compensating, _sagas.SavedStatus(saga.SagaId));
        Assert.IsType<ReleaseQuota>(Assert.Single(_messageClient.Published));
    }

    [Fact]
    public async Task Timeout_BeforeItIsDue_DoesNothing()
    {
        var saga = await StartAsync();
        _time.Now = Now + Timeout - TimeSpan.FromSeconds(1);

        await _orchestrator.TimeOutStaleAsync(Timeout);

        Assert.Equal(FileMessageSagaStatus.AwaitingQuota, _sagas.SavedStatus(saga.SagaId));
        Assert.Empty(_messageClient.Published);
    }

    [Fact]
    public async Task LateQuotaReserved_AfterTimeout_ReleasesAgain_AndSavesNoMessage()
    {
        var saga = await StartAsync();
        _time.Now = Now + Timeout + TimeSpan.FromSeconds(1);
        await _orchestrator.TimeOutStaleAsync(Timeout);
        _messageClient.Published.Clear();

        await _orchestrator.HandleAsync(Reserved(saga));

        Assert.Empty(_sagas.SavedMessages);
        Assert.IsType<ReleaseQuota>(Assert.Single(_messageClient.Published));
    }

    [Fact]
    public async Task DuplicateQuotaReserved_ForCompletedSaga_IsIgnored()
    {
        var saga = await StartAsync();
        await _orchestrator.HandleAsync(Reserved(saga));
        _messageClient.Published.Clear();

        await _orchestrator.HandleAsync(Reserved(saga));

        Assert.Single(_sagas.SavedMessages);
        Assert.Empty(_messageClient.Published);
    }

    [Fact]
    public async Task UnansweredCompensation_IsRetried_ThenGivesUpAsCompensationFailed()
    {
        var saga = await StartAsync();

        // Timeout → Compensating (attempt 1), then two retries (attempts 2 and 3), then give up.
        for (var tick = 1; tick <= FileMessageSaga.MaxCompensationAttempts + 1; tick++)
        {
            _time.Now = Now + (Timeout + TimeSpan.FromSeconds(1)) * tick;
            await _orchestrator.TimeOutStaleAsync(Timeout);
        }

        Assert.Equal(FileMessageSagaStatus.CompensationFailed, _sagas.SavedStatus(saga.SagaId));
        Assert.Equal(FileMessageSaga.MaxCompensationAttempts, _messageClient.Published.OfType<ReleaseQuota>().Count());
    }

    [Fact]
    public async Task Get_BySomeoneElseThanTheSender_IsNotFound()
    {
        var saga = await StartAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => _orchestrator.GetAsync(_chat.Id, saga.SagaId, _bob));
    }
}
