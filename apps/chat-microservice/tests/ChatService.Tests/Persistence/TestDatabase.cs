using Npgsql;
using Testcontainers.PostgreSql;

namespace ChatService.Tests.Persistence;

/// <summary>
/// Én Postgres-container pr. testkørsel (Testcontainers) med samme init-scripts som chatDB/compose.yaml.
/// Kræver kun at Docker kører - containeren startes første gang, den bruges, og ryddes op af Ryuk.
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
            // Kopieres til output af csproj'en - samme scripts som compose bruger.
            .WithResourceMapping(new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "db-init")), "/docker-entrypoint-initdb.d/")
            .Build();

        // Statisk init kan ikke være async. Sker én gang pr. testkørsel.
        container.StartAsync().GetAwaiter().GetResult();
        return container;
    }
}
