using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Wallet.Infrastructure.Persistence;

/// <summary>
/// Usada apenas pelo <c>dotnet ef</c> para gerar migrations (não conecta no banco).
/// </summary>
public sealed class WalletDbContextFactory : IDesignTimeDbContextFactory<WalletDbContext>
{
    public WalletDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<WalletDbContext>()
            .UseNpgsql("Host=localhost;Database=wallet;Username=wallet;Password=wallet")
            .UseSnakeCaseNamingConvention()
            .Options);
}
