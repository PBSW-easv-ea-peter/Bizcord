using System.Text.Json;
using FileService.Contracts;
using FileService.Infrastructure.Messaging;

namespace FileService.Tests;

/// <summary>FileService's side of the saga contract (docs/contracts.md). ChatService's FileSagaContractTests check the same JSON.</summary>
public class ContractTests
{
    private static readonly JsonSerializerOptions WireFormat = new(JsonSerializerDefaults.Web);
    private static readonly MessageNameSerializer Names = new();

    [Theory]
    [InlineData(typeof(ReserveQuota), "file.reserve-quota")]
    [InlineData(typeof(ReleaseQuota), "file.release-quota")]
    [InlineData(typeof(QuotaReserved), "file.quota-reserved")]
    [InlineData(typeof(QuotaRejected), "file.quota-rejected")]
    [InlineData(typeof(QuotaReleased), "file.quota-released")]
    public void Messages_use_logical_names(Type type, string name)
    {
        Assert.Equal(name, Names.Serialize(type));
        Assert.Equal(type, Names.Deserialize(name));
    }

    [Fact]
    public void ReserveQuota_from_ChatService_deserializes()
    {
        const string chatServiceJson =
            """
            { "sagaId": "0192a3b4-0000-7000-8000-000000000001", "fileId": "0192a3b4-0000-7000-8000-000000000002",
              "userId": "0192a3b4-0000-7000-8000-000000000003", "someFutureField": "ignored" }
            """;

        var command = JsonSerializer.Deserialize<ReserveQuota>(chatServiceJson, WireFormat)!;

        Assert.Equal(new ReserveQuota(
            Guid.Parse("0192a3b4-0000-7000-8000-000000000001"),
            Guid.Parse("0192a3b4-0000-7000-8000-000000000002"),
            Guid.Parse("0192a3b4-0000-7000-8000-000000000003")), command);
    }

    [Fact]
    public void QuotaReserved_is_published_with_the_documented_fields()
    {
        var reply = new QuotaReserved(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "video.mp4", "video/mp4", 15_000_000);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(reply, WireFormat));

        string[] documented = ["sagaId", "fileId", "userId", "fileName", "contentType", "sizeBytes"];
        Assert.Equal(documented.Order(), json.RootElement.EnumerateObject().Select(p => p.Name).Order());
    }
}
