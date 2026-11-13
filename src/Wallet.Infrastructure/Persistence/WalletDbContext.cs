using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Wallet.Domain;
using Wallet.Domain.Accounts;
using Wallet.Domain.Transfers;

namespace Wallet.Infrastructure.Persistence;

/// <summary>
/// Mapeia o domínio direto para o Postgres, sem classes de persistência intermediárias.
/// Nomes de tabelas e colunas em snake_case (EFCore.NamingConventions).
/// </summary>
public sealed class WalletDbContext(DbContextOptions<WalletDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();

    public DbSet<Transfer> Transfers => Set<Transfer>();

    public DbSet<IdempotencyRecord> IdempotencyKeys => Set<IdempotencyRecord>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        configurationBuilder.Properties<Money>().HaveConversion<MoneyConverter>();
        configurationBuilder.Properties<Cpf>().HaveConversion<CpfConverter>().HaveMaxLength(11);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WalletDbContext).Assembly);
    }

    private sealed class MoneyConverter() : ValueConverter<Money, long>(
        money => money.Cents,
        cents => Money.FromCents(cents));

    private sealed class CpfConverter() : ValueConverter<Cpf, string>(
        cpf => cpf.Digits,
        digits => Cpf.Parse(digits));
}

/// <summary>Resposta gravada para uma Idempotency-Key (tabela <c>idempotency_keys</c>).</summary>
public sealed class IdempotencyRecord
{
    public Guid AccountId { get; init; }

    public required string Key { get; init; }

    public required string RequestHash { get; init; }

    /// <summary>JSON da resposta. Nulo só durante a transação que reservou a chave.</summary>
    public string? Response { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
