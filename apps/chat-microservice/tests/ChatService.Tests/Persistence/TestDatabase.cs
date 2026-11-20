using ChatService.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ChatService.Tests.Persistence;

/// <summary>
/// One Postgres container per test run (Testcontainers) with the same init scripts as chatDB/compose.yaml.
/// Only requires Docker to be running - the container starts the first time it is used and is cleaned up by Ryuk.
/// </summary>
internal static class TestDatabase
{
    private static readonly PostgreSqlContainer Container = Start();

    public static string ConnectionString { get; } = Container.GetConnectionString();

    public static readonly NpgsqlDataSource DataSource = NpgsqlDataSource.Create(ConnectionString);

    public static readonly ChatDb Db = new(DataSource, new ChatDbOptions(), NullLogger<ChatDb>.Instance);

    /// <summary>A new, unstarted container with the schema - for tests that must not share the database (e.g. pause it).</summary>
    public static PostgreSqlContainer CreateContainer() =>
        new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("chatDB")
            .WithUsername("admin")
            .WithPassword("admin")
            // Copied to the output by the csproj - the same scripts compose uses.
            .WithResourceMapping(new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "db-init")), "/docker-entrypoint-initdb.d/")
            .Build();

    private static PostgreSqlContainer Start()
    {
        var container = CreateContainer();

        // Static initialization can't be async. Happens once per test run.
        container.StartAsync().GetAwaiter().GetResult();
        return container;
    }
}
