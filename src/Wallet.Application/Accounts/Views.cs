using Wallet.Domain;
using Wallet.Domain.Accounts;

namespace Wallet.Application.Accounts;

/// <summary>Dados públicos de uma conta. O CPF sai mascarado (LGPD): <c>***.982.247-**</c>.</summary>
public sealed record AccountView(
    Guid Id,
    string HolderName,
    string Document,
    decimal Balance,
    AccountStatus Status,
    DateTimeOffset OpenedAt)
{
    public static AccountView From(Account account)
    {
        ArgumentNullException.ThrowIfNull(account);
        return new AccountView(
            account.Id,
            account.HolderName,
            MaskDocument(account.Document.Digits),
            account.Balance.Reais,
            account.Status,
            account.OpenedAt);
    }

    public static string MaskDocument(string digits)
    {
        ArgumentNullException.ThrowIfNull(digits);
        return $"***.{digits[3..6]}.{digits[6..9]}-**";
    }
}

/// <summary>Resultado de um depósito ou saque.</summary>
public sealed record MovementView(
    Guid AccountId,
    EntryType Type,
    decimal Amount,
    decimal Balance,
    DateTimeOffset OccurredAt);

public sealed record StatementItem(EntryType Type, decimal Amount, DateTimeOffset OccurredAt, Guid? TransferId);

public sealed record StatementPage(
    Guid AccountId,
    decimal Balance,
    IReadOnlyList<StatementItem> Items,
    string? NextCursor);

/// <summary>Converte o valor recebido (em reais) para <see cref="Money"/>, rejeitando valores inválidos.</summary>
public static class MoneyInput
{
    public static bool TryParse(decimal reais, out Money money)
    {
        money = Money.Zero;
        if (reais <= 0 || decimal.Round(reais, 2) != reais)
        {
            return false;
        }

        try
        {
            money = Money.FromReais(reais);
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }
}
