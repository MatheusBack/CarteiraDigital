using Wallet.Application.Abstractions;
using Wallet.Application.Accounts;
using Wallet.Application.Common;
using Wallet.Application.Idempotency;
using Wallet.Domain;
using Wallet.Domain.Accounts;
using Wallet.Domain.Transfers;

namespace Wallet.Application.Transfers;

public sealed record TransferCommand(Guid FromAccountId, Guid ToAccountId, decimal Amount);

public sealed record TransferView(
    Guid Id,
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount,
    TransferStatus Status,
    DateTimeOffset CreatedAt)
{
    public static TransferView From(Transfer transfer)
    {
        ArgumentNullException.ThrowIfNull(transfer);
        return new TransferView(
            transfer.Id,
            transfer.FromAccountId,
            transfer.ToAccountId,
            transfer.Amount.Reais,
            transfer.Status,
            transfer.CreatedAt);
    }
}

/// <summary>
/// Caso de uso de transferência (RF04, RF05):
/// 1. valida a entrada; 2. consulta o antifraude se o valor passar de R$ 1.000 (antes de travar as contas);
/// 3. trava as duas contas em ordem de Id; 4. aplica as regras do domínio; 5. registra a transferência.
/// </summary>
public sealed class TransferHandler(
    IAccountRepository accounts,
    ITransferRepository transfers,
    IFraudCheck fraudCheck,
    IdempotentExecutor executor,
    TimeProvider clock,
    DailyLimitPolicy dailyLimit)
{
    /// <summary>Transferências acima deste valor passam pelo antifraude.</summary>
    public static readonly Money FraudCheckThreshold = Money.FromReais(1_000m);

    public async Task<Idempotent<TransferView>> HandleAsync(
        TransferCommand command,
        IdempotencyKey key,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!MoneyInput.TryParse(command.Amount, out var amount))
        {
            return new Idempotent<TransferView>(Errors.InvalidAmount, Replayed: false);
        }

        if (command.FromAccountId == command.ToAccountId)
        {
            return new Idempotent<TransferView>(Errors.SameAccount, Replayed: false);
        }

        return await executor.ExecuteAsync(key, ct => TransferAsync(command, amount, ct), cancellationToken);
    }

    private async Task<Result<TransferView>> TransferAsync(TransferCommand command, Money amount, CancellationToken ct)
    {
        if (!await accounts.ExistsAsync(command.FromAccountId, ct)
            || !await accounts.ExistsAsync(command.ToAccountId, ct))
        {
            return AppErrors.AccountNotFound;
        }

        var transferId = Guid.CreateVersion7(clock.GetUtcNow());

        // O antifraude é consultado ANTES de travar as contas: uma chamada HTTP lenta
        // não pode segurar travas no banco.
        if (amount > FraudCheckThreshold)
        {
            var decision = await fraudCheck.CheckAsync(
                new FraudCheckRequest(transferId, command.FromAccountId, command.ToAccountId, amount.Cents), ct);

            if (decision != FraudDecision.Approved)
            {
                var status = decision == FraudDecision.Denied ? TransferStatus.Denied : TransferStatus.InReview;
                return Record(transferId, command, amount, status);
            }
        }

        var now = clock.GetUtcNow();

        // O limite diário só precisa das transferências enviadas "hoje" (no fuso de Brasília);
        // 2 dias de margem cobrem qualquer fuso.
        var locked = await accounts.LockAsync(
            [command.FromAccountId, command.ToAccountId],
            loadTransfersOutSince: now.AddDays(-2),
            ct);

        var from = locked.Single(a => a.Id == command.FromAccountId);
        var to = locked.Single(a => a.Id == command.ToAccountId);

        var result = TransferService.Execute(from, to, amount, transferId, now, dailyLimit);
        return result.IsSuccess
            ? Record(transferId, command, amount, TransferStatus.Completed)
            : result.Error!;
    }

    private TransferView Record(Guid transferId, TransferCommand command, Money amount, TransferStatus status)
    {
        var transfer = Transfer.Record(
            transferId, command.FromAccountId, command.ToAccountId, amount, status, clock.GetUtcNow());
        transfers.Add(transfer);
        return TransferView.From(transfer);
    }
}
