using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Wallet.Domain.Accounts;
using Wallet.Domain.Transfers;

namespace Wallet.Infrastructure.Persistence;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        // Defesa em profundidade: mesmo que um bug passe pelo domínio, o banco recusa saldo negativo.
        builder.ToTable("accounts", t => t.HasCheckConstraint("ck_accounts_balance_non_negative", "balance_cents >= 0"));

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.HolderName).HasMaxLength(Account.MaxHolderNameLength);
        builder.Property(a => a.Document).IsFixedLength();
        builder.HasIndex(a => a.Document).IsUnique();
        builder.Property(a => a.Balance).HasColumnName("balance_cents");
        builder.Property(a => a.OpenedAt);
        builder.Property(a => a.Status).HasConversion<short>();
        builder.Ignore(a => a.IsBlocked);

        builder.HasMany(a => a.Entries)
            .WithOne()
            .HasForeignKey(e => e.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(a => a.Entries)
            .HasField("_entries")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    /// <summary>Chave técnica (não existe no domínio). Também define a ordem do extrato.</summary>
    public const string SequenceProperty = "Id";

    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.ToTable("ledger_entries", t => t.HasCheckConstraint("ck_ledger_entries_amount_positive", "amount_cents > 0"));

        builder.Property<long>(SequenceProperty).UseIdentityAlwaysColumn();
        builder.HasKey(SequenceProperty);

        builder.Property(e => e.Type).HasConversion<short>();
        builder.Property(e => e.Amount).HasColumnName("amount_cents");
        builder.Ignore(e => e.IsCredit);
        builder.Ignore(e => e.IsDebit);

        // Extrato: lançamentos de uma conta do mais novo para o mais antigo.
        builder.HasIndex(nameof(LedgerEntry.AccountId), SequenceProperty)
            .IsDescending(false, true)
            .HasDatabaseName("ix_ledger_entries_statement");

        // Limite diário: transferências enviadas por conta e data.
        builder.HasIndex(e => new { e.AccountId, e.Type, e.OccurredAt })
            .HasDatabaseName("ix_ledger_entries_daily_limit");
    }
}

internal sealed class TransferConfiguration : IEntityTypeConfiguration<Transfer>
{
    public void Configure(EntityTypeBuilder<Transfer> builder)
    {
        builder.ToTable("transfers", t =>
        {
            t.HasCheckConstraint("ck_transfers_amount_positive", "amount_cents > 0");
            t.HasCheckConstraint("ck_transfers_distinct_accounts", "from_account_id <> to_account_id");
        });

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Amount).HasColumnName("amount_cents");
        builder.Property(t => t.Status).HasConversion<short>();
        builder.Property(t => t.CreatedAt);

        builder.HasOne<Account>().WithMany().HasForeignKey(t => t.FromAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(t => t.ToAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency_keys");
        builder.HasKey(r => new { r.AccountId, r.Key });
        builder.Property(r => r.Key).HasColumnName("idempotency_key").HasMaxLength(100);
        builder.Property(r => r.RequestHash).HasMaxLength(64).IsFixedLength();
        builder.Property(r => r.Response).HasColumnType("jsonb");
    }
}
