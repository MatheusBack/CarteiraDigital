using Microsoft.Extensions.DependencyInjection;

using Wallet.Application.Accounts;
using Wallet.Application.Idempotency;
using Wallet.Application.Transfers;
using Wallet.Domain.Accounts;

namespace Wallet.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddWalletApplication(this IServiceCollection services)
    {
        services.AddSingleton(DailyLimitPolicy.Default);
        services.AddScoped<IdempotentExecutor>();
        services.AddScoped<OpenAccountHandler>();
        services.AddScoped<DepositHandler>();
        services.AddScoped<WithdrawHandler>();
        services.AddScoped<BlockAccountHandler>();
        services.AddScoped<TransferHandler>();
        return services;
    }
}
