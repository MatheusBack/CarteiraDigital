using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

using Npgsql;

using Respawn;
using Respawn.Graph;

using Wallet.Infrastructure;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Wallet.Integration.Tests.Infrastructure;

/// <summary>
/// Sobe a API de verdade em memória, com Postgres real e o antifraude simulado pelo WireMock.
/// É compartilhada por todos os testes (o banco é limpo entre eles com Respawn).
/// </summary>
public sealed class WalletApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly TestDatabase _database = TestDatabase.Create();
    private Respawner? _respawner;

    /// <summary>Serviço antifraude falso. Por padrão aprova tudo.</summary>
    public WireMockServer FraudApi { get; } = WireMockServer.Start();

    public string ConnectionString => _database.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        await _database.StartAsync();
        await Services.MigrateWalletDatabaseAsync();

        await using var connection = await OpenConnectionAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = [new Table("__EFMigrationsHistory")],
        });

        ApproveAllTransfers();
    }

    /// <summary>Apaga os dados e volta o antifraude ao padrão. Chamado antes de cada teste.</summary>
    public async Task ResetAsync()
    {
        await using var connection = await OpenConnectionAsync();
        await _respawner!.ResetAsync(connection);

        FraudApi.Reset();
        ApproveAllTransfers();
    }

    public async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        FraudApi.Stop();
        FraudApi.Dispose();
        await _database.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Postgres", ConnectionString);
        builder.UseSetting("FraudApi:BaseUrl", FraudApi.Url);
        builder.UseSetting("Database:MigrateOnStartup", "false");
    }

    private void ApproveAllTransfers() =>
        FraudApi
            .Given(Request.Create().WithPath("/check").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new { decision = "approved" }));
}

[CollectionDefinition(Name)]
public sealed class ApiTestGroup : ICollectionFixture<WalletApiFactory>
{
    public const string Name = "api";
}
