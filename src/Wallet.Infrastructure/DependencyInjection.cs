using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

using Polly;

using Wallet.Application.Abstractions;
using Wallet.Infrastructure.Fraud;
using Wallet.Infrastructure.Persistence;

namespace Wallet.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddWalletInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<WalletDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("Postgres")
                ?? throw new InvalidOperationException("Configure a connection string 'ConnectionStrings:Postgres'.");

            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
        });

        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ITransferRepository, TransferRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();
        services.AddScoped<IWalletQueries, WalletQueries>();

        services.AddOptions<FraudApiOptions>().BindConfiguration(FraudApiOptions.Section);

        services
            .AddHttpClient<IFraudCheck, FraudCheckHttpClient>((sp, http) =>
            {
                var baseUrl = sp.GetRequiredService<IOptions<FraudApiOptions>>().Value.BaseUrl
                    ?? throw new InvalidOperationException("Configure 'FraudApi:BaseUrl'.");

                // Barra no final para "check" ser resolvido relativo à URL base.
                http.BaseAddress = new Uri(baseUrl.ToString().TrimEnd('/') + "/");
            })
            .AddResilienceHandler("fraud-check", (pipeline, context) =>
            {
                var options = context.ServiceProvider.GetRequiredService<IOptions<FraudApiOptions>>().Value;

                // Ordem: tempo total (fora) → retry → tempo por tentativa (dentro).
                pipeline.AddTimeout(options.TotalTimeout);
                pipeline.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = options.MaxRetries,
                    Delay = TimeSpan.FromMilliseconds(100),
                    BackoffType = DelayBackoffType.Constant,
                    UseJitter = false,
                });
                pipeline.AddTimeout(options.AttemptTimeout);
            });

        return services;
    }

    /// <summary>Aplica as migrations pendentes (usado em desenvolvimento e nos testes).</summary>
    public static async Task MigrateWalletDatabaseAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<WalletDbContext>().Database.MigrateAsync();
    }
}
