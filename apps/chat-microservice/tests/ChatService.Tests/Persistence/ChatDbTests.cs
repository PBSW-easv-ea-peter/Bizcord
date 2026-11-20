using ChatService.Infrastructure.Persistence;
using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace ChatService.Tests.Persistence;

[Trait("Category", "Integration")]
public class ChatDbTests
{
    [Fact]
    public async Task PostgresErrors_DoNotOpenTheCircuit()
    {
        // Own instance, so the breaker's state is not shared with other tests.
        var db = new ChatDb(TestDatabase.DataSource, new ChatDbOptions { MinimumThroughput = 2 }, NullLogger<ChatDb>.Instance);

        // An answer from a working database (here division by zero; in practice e.g. a unique violation -> 409).
        for (var i = 0; i < 5; i++)
            await Assert.ThrowsAsync<PostgresException>(() => db.RunAsync(connection => connection.ExecuteAsync("select 1/0"), CancellationToken.None));

        Assert.Equal(1, await db.RunAsync(connection => connection.ExecuteScalarAsync<int>("select 1"), CancellationToken.None));
    }
}
