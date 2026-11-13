using Npgsql;

using Testcontainers.PostgreSql;

namespace Wallet.Integration.Tests.Infrastructure;

/// <summary>
/// Banco Postgres real para os testes. Por padrão sobe um container descartável (Testcontainers).
/// Sem Docker, defina <c>WALLET_TEST_POSTGRES</c> com a connection string de um Postgres local:
/// um banco temporário é criado e apagado no fim.
/// </summary>
internal abstract class TestDatabase : IAsyncDisposable
{
    public const string ExternalServerVariable = "WALLET_TEST_POSTGRES";

    public abstract string ConnectionString { get; }

    public static TestDatabase Create()
    {
        var external = Environment.GetEnvironmentVariable(ExternalServerVariable);
        return string.IsNullOrWhiteSpace(external)
            ? new ContainerDatabase()
            : new ExternalDatabase(external);
    }

    public abstract Task StartAsync();

    public abstract ValueTask DisposeAsync();

    private sealed class ContainerDatabase : TestDatabase
    {
        private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine").Build();

        public override string ConnectionString => _container.GetConnectionString();

        public override Task StartAsync() => _container.StartAsync();

        public override ValueTask DisposeAsync() => _container.DisposeAsync();
    }

    private sealed class ExternalDatabase(string serverConnectionString) : TestDatabase
    {
        private readonly string _databaseName = $"wallet_test_{Guid.NewGuid():N}";

        public override string ConnectionString =>
            new NpgsqlConnectionStringBuilder(serverConnectionString) { Database = _databaseName }.ConnectionString;

        public override async Task StartAsync() => await ExecuteOnServerAsync($"CREATE DATABASE {_databaseName}");

        public override async ValueTask DisposeAsync()
        {
            NpgsqlConnection.ClearAllPools();
            await ExecuteOnServerAsync($"DROP DATABASE IF EXISTS {_databaseName} WITH (FORCE)");
        }

        private async Task ExecuteOnServerAsync(string sql)
        {
            await using var connection = new NpgsqlConnection(serverConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }
    }
}
