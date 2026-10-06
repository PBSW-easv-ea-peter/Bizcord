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

    private static PostgreSqlContainer Start()
    {
        var container = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("chatDB")
            .WithUsername("admin")
            .WithPassword("admin")
            // Copied to the output by the csproj - the same scripts compose uses.
            .WithResourceMapping(new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "db-init")), "/docker-entrypoint-initdb.d/")
            .Build();

        // Static initialization can't be async. Happens once per test run.
        container.StartAsync().GetAwaiter().GetResult();
        return container;
    }
}
