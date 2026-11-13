using Wallet.Domain.Common;

namespace Wallet.Domain.Accounts;

public enum AccountStatus
{
    Active = 1,
    Blocked = 2,
}

/// <summary>
/// Conta da carteira. Toda alteração de saldo gera um <see cref="LedgerEntry"/>.
/// Regras de negócio retornam <see cref="Result"/> e, em caso de falha, não alteram nada.
/// </summary>
public sealed class Account
{
    /// <summary>Valor máximo aceito em um único depósito (RF02).</summary>
    public static readonly Money MaxDepositPerOperation = Money.FromReais(50_000m);

    public const int MaxHolderNameLength = 120;

    private readonly List<LedgerEntry> _entries = [];

    private Account(Guid id, string holderName, Cpf document, DateTimeOffset openedAt)
    {
        Id = id;
        HolderName = holderName;
        Document = document;
        OpenedAt = openedAt;
        Status = AccountStatus.Active;
        Balance = Money.Zero;
    }

    public Guid Id { get; }

    public string HolderName { get; }

    public Cpf Document { get; }

    public DateTimeOffset OpenedAt { get; }

    public AccountStatus Status { get; private set; }

    public Money Balance { get; private set; }

    public IReadOnlyList<LedgerEntry> Entries => _entries;

    public bool IsBlocked => Status == AccountStatus.Blocked;

    public static Account Open(string holderName, Cpf document, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(document);

        var name = holderName?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            throw new DomainException("O nome do titular é obrigatório.");
        }

        if (name.Length > MaxHolderNameLength)
        {
            throw new DomainException($"O nome do titular deve ter no máximo {MaxHolderNameLength} caracteres.");
        }

        return new Account(Guid.CreateVersion7(now), name, document, now);
    }

    public Result Deposit(Money amount, DateTimeOffset now)
    {
        if (IsBlocked)
        {
            return Errors.AccountBlocked;
        }

        if (amount.IsZero)
        {
            return Errors.InvalidAmount;
        }

        if (amount > MaxDepositPerOperation)
        {
            return Errors.DepositLimitExceeded;
        }

        AddCredit(EntryType.Deposit, amount, now, transferId: null);
        return Result.Ok();
    }

    public Result Withdraw(Money amount, DateTimeOffset now)
    {
        if (IsBlocked)
        {
            return Errors.AccountBlocked;
        }

        if (amount.IsZero)
        {
            return Errors.InvalidAmount;
        }

        if (amount > Balance)
        {
            return Errors.InsufficientFunds;
        }

        AddDebit(EntryType.Withdrawal, amount, now, transferId: null);
        return Result.Ok();
    }

    public void Block() => Status = AccountStatus.Blocked;

    public void Unblock() => Status = AccountStatus.Active;

    /// <summary>Saldo recalculado a partir do ledger. Deve ser sempre igual a <see cref="Balance"/>.</summary>
    public Money RecomputeBalanceFromLedger()
    {
        long cents = 0;
        foreach (var entry in _entries)
        {
            cents += entry.IsCredit ? entry.Amount.Cents : -entry.Amount.Cents;
        }

        return Money.FromCents(cents);
    }

    /// <summary>Débito de transferência. Usado apenas por <see cref="Transfers.TransferService"/>.</summary>
    internal Result DebitForTransfer(Money amount, Guid transferId, DateTimeOffset now, DailyLimitPolicy limit)
    {
        if (IsBlocked)
        {
            return Errors.AccountBlocked;
        }

        if (amount.IsZero)
        {
            return Errors.InvalidAmount;
        }

        if (amount > Balance)
        {
            return Errors.InsufficientFunds;
        }

        if (!limit.Allows(this, amount, now))
        {
            return Errors.DailyLimitExceeded;
        }

        AddDebit(EntryType.TransferOut, amount, now, transferId);
        return Result.Ok();
    }

    /// <summary>Crédito de transferência. Usado apenas por <see cref="Transfers.TransferService"/>.</summary>
    internal Result CreditFromTransfer(Money amount, Guid transferId, DateTimeOffset now)
    {
        if (IsBlocked)
        {
            return Errors.DestinationAccountBlocked;
        }

        if (amount.IsZero)
        {
            return Errors.InvalidAmount;
        }

        AddCredit(EntryType.TransferIn, amount, now, transferId);
        return Result.Ok();
    }

    private void AddCredit(EntryType type, Money amount, DateTimeOffset now, Guid? transferId)
    {
        Balance += amount;
        _entries.Add(new LedgerEntry(Id, type, amount, now, transferId));
    }

    private void AddDebit(EntryType type, Money amount, DateTimeOffset now, Guid? transferId)
    {
        Balance -= amount;
        _entries.Add(new LedgerEntry(Id, type, amount, now, transferId));
    }
}
