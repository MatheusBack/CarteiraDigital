using Wallet.Domain.Accounts;
using Wallet.Domain.Common;

namespace Wallet.Domain.Transfers;

/// <summary>
/// Transfere dinheiro entre duas contas. É "tudo ou nada": se qualquer regra falhar,
/// nenhuma das contas é alterada.
/// </summary>
public static class TransferService
{
    public static Result Execute(
        Account from,
        Account to,
        Money amount,
        Guid transferId,
        DateTimeOffset now,
        DailyLimitPolicy limit)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        ArgumentNullException.ThrowIfNull(limit);

        if (from.Id == to.Id)
        {
            return Errors.SameAccount;
        }

        // Verificado ANTES do débito: o crédito não pode falhar depois que a origem foi debitada.
        if (to.IsBlocked)
        {
            return Errors.DestinationAccountBlocked;
        }

        var debit = from.DebitForTransfer(amount, transferId, now, limit);
        if (debit.IsFailure)
        {
            return debit;
        }

        var credit = to.CreditFromTransfer(amount, transferId, now);
        return credit.IsSuccess
            ? credit
            : throw new InvalidOperationException(
                $"Crédito falhou após o débito ({credit.Error!.Code}). Isso indica um bug nas pré-condições.");
    }
}
