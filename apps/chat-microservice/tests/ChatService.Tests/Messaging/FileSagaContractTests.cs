using System.Text.Json;
using ChatService.Contracts;
using ChatService.Infrastructure.Messaging;

namespace ChatService.Tests.Messaging;

/// <summary>
/// Both directions of the file-message saga against the wire format in file-microservice/docs/contracts.md.
/// FileService's tests check the same JSON from its side.
/// </summary>
public class FileSagaContractTests
{
    private static readonly JsonSerializerOptions WireFormat = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(typeof(ReserveQuota), "file.reserve-quota")]
    [InlineData(typeof(ReleaseQuota), "file.release-quota")]
    [InlineData(typeof(QuotaReserved), "file.quota-reserved")]
    [InlineData(typeof(QuotaRejected), "file.quota-rejected")]
    [InlineData(typeof(QuotaReleased), "file.quota-released")]
    public void Messages_use_FileService_logical_names(Type type, string name)
    {
        Assert.Equal(name, EventTypeNames.ForChatService.Serialize(type));
        Assert.Equal(type, EventTypeNames.ForChatService.Deserialize(name));
    }

    [Fact]
    public void ReserveQuota_is_sent_in_the_shape_FileService_expects()
    {
        var command = new ReserveQuota(
            Guid.Parse("0192a3b4-0000-7000-8000-000000000001"),
            Guid.Parse("0192a3b4-0000-7000-8000-000000000002"),
            Guid.Parse("0192a3b4-0000-7000-8000-000000000003"));

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(command, WireFormat));

        Assert.Equal("0192a3b4-0000-7000-8000-000000000001", json.RootElement.GetProperty("sagaId").GetString());
        Assert.Equal("0192a3b4-0000-7000-8000-000000000002", json.RootElement.GetProperty("fileId").GetString());
        Assert.Equal("0192a3b4-0000-7000-8000-000000000003", json.RootElement.GetProperty("userId").GetString());
    }

    [Fact]
    public void QuotaReserved_from_FileService_deserializes_with_every_field_ChatService_uses()
    {
        const string fileServiceJson =
            """
            {
              "sagaId": "0192a3b4-0000-7000-8000-000000000001",
              "fileId": "0192a3b4-0000-7000-8000-000000000002",
              "userId": "0192a3b4-0000-7000-8000-000000000003",
              "fileName": "video.mp4",
              "contentType": "video/mp4",
              "sizeBytes": 15000000,
              "someFutureField": "ignored"
            }
            """;

        var reply = JsonSerializer.Deserialize<QuotaReserved>(fileServiceJson, WireFormat)!;

        Assert.Equal(Guid.Parse("0192a3b4-0000-7000-8000-000000000001"), reply.SagaId);
        Assert.Equal(Guid.Parse("0192a3b4-0000-7000-8000-000000000002"), reply.FileId);
        Assert.Equal("video.mp4", reply.FileName);
        Assert.Equal(15_000_000, reply.SizeBytes);
    }

    [Fact]
    public void QuotaRejected_from_FileService_deserializes_with_every_field_ChatService_uses()
    {
        const string fileServiceJson =
            """
            { "sagaId": "0192a3b4-0000-7000-8000-000000000001", "fileId": "0192a3b4-0000-7000-8000-000000000002",
              "userId": "0192a3b4-0000-7000-8000-000000000003", "reason": "Storage quota exceeded." }
            """;

        var reply = JsonSerializer.Deserialize<QuotaRejected>(fileServiceJson, WireFormat)!;

        Assert.Equal(Guid.Parse("0192a3b4-0000-7000-8000-000000000001"), reply.SagaId);
        Assert.Equal("Storage quota exceeded.", reply.Reason);
    }

    [Fact]
    public void QuotaReleased_from_FileService_deserializes_with_every_field_ChatService_uses()
    {
        const string fileServiceJson =
            """
            { "sagaId": "0192a3b4-0000-7000-8000-000000000001", "fileId": "0192a3b4-0000-7000-8000-000000000002",
              "userId": "0192a3b4-0000-7000-8000-000000000003" }
            """;

        var reply = JsonSerializer.Deserialize<QuotaReleased>(fileServiceJson, WireFormat)!;

        Assert.Equal(Guid.Parse("0192a3b4-0000-7000-8000-000000000001"), reply.SagaId);
        Assert.Equal(Guid.Parse("0192a3b4-0000-7000-8000-000000000002"), reply.FileId);
    }
}
