using Npgsql;

namespace ChatService.Tests.Persistence;

/// <summary>Kræver at chatDB kører: docker compose -f chatDB/compose.yaml up -d</summary>
internal static class TestDatabase
{
    public static readonly NpgsqlDataSource DataSource =
        NpgsqlDataSource.Create("Host=localhost;Port=5432;Database=chatDB;Username=admin;Password=admin");
}
