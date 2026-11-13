namespace Wallet.Domain.Accounts;

/// <summary>Tipo do lançamento no extrato.</summary>
public enum EntryType
{
    Deposit = 1,
    Withdrawal = 2,
    TransferIn = 3,
    TransferOut = 4,
}

/// <summary>
/// Lançamento imutável no livro-razão (ledger) da conta. O saldo sempre pode ser
/// recalculado a partir dos lançamentos: créditos menos débitos.
/// </summary>
public sealed record LedgerEntry(
    Guid AccountId,
    EntryType Type,
    Money Amount,
    DateTimeOffset OccurredAt,
    Guid? TransferId = null)
{
    public bool IsCredit => Type is EntryType.Deposit or EntryType.TransferIn;

    public bool IsDebit => !IsCredit;
}
