using Wallet.Application.Abstractions;
using Wallet.Application.Common;
using Wallet.Application.Idempotency;
using Wallet.Domain;
using Wallet.Domain.Accounts;
using Wallet.Domain.Common;

namespace Wallet.Application.Accounts;

/// <summary>Depósito ou saque de <see cref="Amount"/> reais.</summary>
public sealed record MovementCommand(Guid AccountId, decimal Amount);

public sealed class DepositHandler(IAccountRepository accounts, IdempotentExecutor executor, TimeProvider clock)
{
    public Task<Idempotent<MovementView>> HandleAsync(
        MovementCommand command,
        IdempotencyKey key,
        CancellationToken cancellationToken) =>
        MovementRunner.RunAsync(
            accounts, executor, clock, command, key, EntryType.Deposit,
            static (account, amount, now) => account.Deposit(amount, now),
            cancellationToken);
}

public sealed class WithdrawHandler(IAccountRepository accounts, IdempotentExecutor executor, TimeProvider clock)
{
    public Task<Idempotent<MovementView>> HandleAsync(
        MovementCommand command,
        IdempotencyKey key,
        CancellationToken cancellationToken) =>
        MovementRunner.RunAsync(
            accounts, executor, clock, command, key, EntryType.Withdrawal,
            static (account, amount, now) => account.Withdraw(amount, now),
            cancellationToken);
}

internal static class MovementRunner
{
    public static async Task<Idempotent<MovementView>> RunAsync(
        IAccountRepository accounts,
        IdempotentExecutor executor,
        TimeProvider clock,
        MovementCommand command,
        IdempotencyKey key,
        EntryType type,
        Func<Account, Money, DateTimeOffset, Result> apply,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!MoneyInput.TryParse(command.Amount, out var amount))
        {
            return new Idempotent<MovementView>(Errors.InvalidAmount, Replayed: false);
        }

        return await executor.ExecuteAsync<MovementView>(
            key,
            async ct =>
            {
                var locked = await accounts.LockAsync([command.AccountId], loadTransfersOutSince: null, ct);
                if (locked.Count == 0)
                {
                    return AppErrors.AccountNotFound;
                }

                var account = locked[0];
                var now = clock.GetUtcNow();
                var result = apply(account, amount, now);

                return result.IsSuccess
                    ? new MovementView(account.Id, type, amount.Reais, account.Balance.Reais, now)
                    : result.Error!;
            },
            cancellationToken);
    }
}
