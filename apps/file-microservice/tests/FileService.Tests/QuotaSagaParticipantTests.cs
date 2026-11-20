using FileService.Application;
using FileService.Contracts;
using FileService.Domain;
using FileService.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace FileService.Tests;

/// <summary>Real DB (the quota rule is a conditional SQL update), fake broker.</summary>
[Trait("Category", "Integration")]
public class QuotaSagaParticipantTests
{
    private const long Limit = 1000;

    private readonly DapperFileRepository _files = new(TestDatabase.DataSource);
    private readonly InMemoryMessageClient _messageClient = new();
    private readonly QuotaSettings _settings = new() { StandardLimitBytes = Limit, MaxUploadBytes = 10_000 };
    private readonly QuotaSagaParticipant _participant;
    private readonly FileAppService _app;

    // A fresh user per test - the DB is shared by all tests in the run.
    private readonly Guid _user = Guid.NewGuid();

    public QuotaSagaParticipantTests()
    {
        _participant = new QuotaSagaParticipant(_files, _messageClient, _settings, NullLogger<QuotaSagaParticipant>.Instance);
        _app = new FileAppService(_files, _settings, TimeProvider.System, NullLogger<FileAppService>.Instance);
    }

    private Task<StoredFile> UploadAsync(int bytes, Guid? owner = null) =>
        _app.UploadAsync(owner ?? _user, "video.mp4", "video/mp4", new byte[bytes]);

    private T PublishedFor<T>(Guid sagaId) where T : class =>
        _messageClient.Published.OfType<T>().Single(m => (Guid)typeof(T).GetProperty("SagaId")!.GetValue(m)! == sagaId);

    [Fact]
    public async Task Reserve_WithinQuota_DeductsAndAttaches()
    {
        var file = await UploadAsync(600);
        var sagaId = Guid.NewGuid();

        await _participant.HandleAsync(new ReserveQuota(sagaId, file.Id, _user));

        var reply = PublishedFor<QuotaReserved>(sagaId);
        Assert.Equal(new QuotaReserved(sagaId, file.Id, _user, "video.mp4", "video/mp4", 600), reply);
        Assert.Equal(600, (await _files.GetQuotaAsync(_user, Limit)).UsedBytes);
        Assert.Equal(FileStatus.Attached, (await _files.GetAsync(file.Id))!.Status);
    }

    [Fact]
    public async Task Reserve_OverQuota_RejectsAndDeletesTheTemporaryFile()
    {
        var first = await UploadAsync(600);
        var second = await UploadAsync(600);
        await _participant.HandleAsync(new ReserveQuota(Guid.NewGuid(), first.Id, _user));
        var sagaId = Guid.NewGuid();

        await _participant.HandleAsync(new ReserveQuota(sagaId, second.Id, _user));

        Assert.Contains("quota exceeded", PublishedFor<QuotaRejected>(sagaId).Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Null(await _files.GetAsync(second.Id));
        Assert.Equal(600, (await _files.GetQuotaAsync(_user, Limit)).UsedBytes);
    }

    [Fact]
    public async Task Reserve_Twice_BySameSaga_AnswersReservedAgain_WithoutDeductingTwice()
    {
        var file = await UploadAsync(600);
        var sagaId = Guid.NewGuid();

        await _participant.HandleAsync(new ReserveQuota(sagaId, file.Id, _user));
        await _participant.HandleAsync(new ReserveQuota(sagaId, file.Id, _user));

        Assert.Equal(2, _messageClient.Published.OfType<QuotaReserved>().Count(r => r.SagaId == sagaId));
        Assert.Equal(600, (await _files.GetQuotaAsync(_user, Limit)).UsedBytes);
    }

    [Fact]
    public async Task Reserve_SomeoneElsesFile_IsRejected_AndTheFileIsLeftAlone()
    {
        var file = await UploadAsync(100, owner: Guid.NewGuid());
        var sagaId = Guid.NewGuid();

        await _participant.HandleAsync(new ReserveQuota(sagaId, file.Id, _user));

        PublishedFor<QuotaRejected>(sagaId);
        Assert.NotNull(await _files.GetAsync(file.Id));
    }

    [Fact]
    public async Task Release_AfterReserve_GivesQuotaBackAndDeletesFile()
    {
        var file = await UploadAsync(600);
        var sagaId = Guid.NewGuid();
        await _participant.HandleAsync(new ReserveQuota(sagaId, file.Id, _user));

        await _participant.HandleAsync(new ReleaseQuota(sagaId, file.Id, _user));

        Assert.Equal(new QuotaReleased(sagaId, file.Id, _user), PublishedFor<QuotaReleased>(sagaId));
        Assert.Equal(0, (await _files.GetQuotaAsync(_user, Limit)).UsedBytes);
        Assert.Null(await _files.GetAsync(file.Id));
    }

    [Fact]
    public async Task Release_IsIdempotent_AndAlwaysAnswers()
    {
        var file = await UploadAsync(600);
        var sagaId = Guid.NewGuid();
        await _participant.HandleAsync(new ReserveQuota(sagaId, file.Id, _user));

        await _participant.HandleAsync(new ReleaseQuota(sagaId, file.Id, _user));
        await _participant.HandleAsync(new ReleaseQuota(sagaId, file.Id, _user));

        Assert.Equal(2, _messageClient.Published.OfType<QuotaReleased>().Count(r => r.SagaId == sagaId));
        Assert.Equal(0, (await _files.GetQuotaAsync(_user, Limit)).UsedBytes);
    }

    [Fact]
    public async Task Release_BeforeReserve_DeletesTheTemporaryFile_AndALateReserveIsRejected()
    {
        // ChatService timed out before our ReserveQuota was processed.
        var file = await UploadAsync(600);
        var sagaId = Guid.NewGuid();

        await _participant.HandleAsync(new ReleaseQuota(sagaId, file.Id, _user));
        await _participant.HandleAsync(new ReserveQuota(sagaId, file.Id, _user));

        PublishedFor<QuotaRejected>(sagaId);
        Assert.Equal(0, (await _files.GetQuotaAsync(_user, Limit)).UsedBytes);
    }

    [Fact]
    public async Task Release_BySomeOtherSaga_DoesNotTouchAnAttachedFile()
    {
        var file = await UploadAsync(600);
        await _participant.HandleAsync(new ReserveQuota(Guid.NewGuid(), file.Id, _user));

        await _participant.HandleAsync(new ReleaseQuota(Guid.NewGuid(), file.Id, _user));

        Assert.NotNull(await _files.GetAsync(file.Id));
        Assert.Equal(600, (await _files.GetQuotaAsync(_user, Limit)).UsedBytes);
    }

    [Fact]
    public async Task ConcurrentReservations_NeverExceedTheLimit()
    {
        // Ten files of 300 bytes against a limit of 1000: exactly three fit, whatever the order.
        var files = new List<StoredFile>();
        for (var i = 0; i < 10; i++)
            files.Add(await UploadAsync(300));

        await Task.WhenAll(files.Select(f => _participant.HandleAsync(new ReserveQuota(Guid.NewGuid(), f.Id, _user))));

        Assert.Equal(900, (await _files.GetQuotaAsync(_user, Limit)).UsedBytes);
        Assert.Equal(3, _messageClient.Published.OfType<QuotaReserved>().Count(r => r.UserId == _user));
    }
}

internal sealed class InMemoryMessageClient : IMessageClient
{
    private readonly List<object> _published = [];

    /// <summary>A snapshot - the concurrency test publishes from several threads.</summary>
    public IReadOnlyList<object> Published
    {
        get { lock (_published) return _published.ToList(); }
    }

    public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
    {
        lock (_published) _published.Add(message!);
        return Task.CompletedTask;
    }

    public Task SubscribeAsync<T>(string subscriptionId, Func<T, Task> handler) => Task.CompletedTask;
}
