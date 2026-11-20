using Npgsql;
using Testcontainers.PostgreSql;

namespace FileService.Tests;

/// <summary>One Postgres container per test run (Testcontainers) with FileDB's schema. Only requires Docker.</summary>
internal static class TestDatabase
{
    private static readonly PostgreSqlContainer Container = Start();

    public static readonly NpgsqlDataSource DataSource = NpgsqlDataSource.Create(Container.GetConnectionString());

    private static PostgreSqlContainer Start()
    {
        var container = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("fileDB")
            .WithUsername("admin")
            .WithPassword("admin")
            .WithResourceMapping(new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "db-init")), "/docker-entrypoint-initdb.d/")
            .Build();

        container.StartAsync().GetAwaiter().GetResult();
        return container;
    }
}
