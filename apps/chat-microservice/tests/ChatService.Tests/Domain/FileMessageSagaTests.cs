using ChatService.Domain;

namespace ChatService.Tests.Domain;

public class FileMessageSagaTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid _alice = Guid.NewGuid();
    private readonly Chat _chat;

    public FileMessageSagaTests() => _chat = Chat.CreateDirect(_alice, Guid.NewGuid(), Now);

    private FileMessageSaga Start() => FileMessageSaga.Start(_chat, _alice, Guid.NewGuid(), null, Now);

    [Fact]
    public void Start_WaitsForQuota()
    {
        var saga = Start();

        Assert.Equal(FileMessageSagaStatus.AwaitingQuota, saga.Status);
        Assert.False(saga.IsFinished);
    }

    [Fact]
    public void Start_ByNonParticipant_IsNotAllowed() =>
        Assert.Throws<NotAllowedException>(() => FileMessageSaga.Start(_chat, Guid.NewGuid(), Guid.NewGuid(), null, Now));

    [Fact]
    public void Start_WithoutFile_IsInvalid() =>
        Assert.Throws<DomainException>(() => FileMessageSaga.Start(_chat, _alice, Guid.Empty, null, Now));

    [Fact]
    public void Complete_SetsMessageId()
    {
        var saga = Start();
        var messageId = Guid.NewGuid();

        saga.Complete(messageId, Now);

        Assert.Equal(FileMessageSagaStatus.Completed, saga.Status);
        Assert.Equal(messageId, saga.MessageId);
        Assert.True(saga.IsFinished);
    }

    [Fact]
    public void Reject_FailsWithReason()
    {
        var saga = Start();

        saga.Reject("Storage quota exceeded.", Now);

        Assert.Equal(FileMessageSagaStatus.Failed, saga.Status);
        Assert.Equal("Storage quota exceeded.", saga.FailureReason);
    }

    [Fact]
    public void Compensation_EndsAsFailed_WhenConfirmed()
    {
        var saga = Start();

        saga.StartCompensation("Timed out.", Now);
        saga.CompleteCompensation(Now);

        Assert.Equal(FileMessageSagaStatus.Failed, saga.Status);
    }

    [Fact]
    public void RetryCompensation_GivesUpAfterMaxAttempts()
    {
        var saga = Start();
        saga.StartCompensation("Timed out.", Now);

        Assert.True(saga.RetryCompensation(Now));
        Assert.True(saga.RetryCompensation(Now));
        Assert.False(saga.RetryCompensation(Now));

        Assert.Equal(FileMessageSagaStatus.CompensationFailed, saga.Status);
        Assert.Equal(FileMessageSaga.MaxCompensationAttempts, saga.CompensationAttempts);
    }

    [Fact]
    public void A_completed_saga_cannot_be_compensated()
    {
        var saga = Start();
        saga.Complete(Guid.NewGuid(), Now);

        Assert.Throws<ConflictException>(() => saga.StartCompensation("Too late.", Now));
    }
}
