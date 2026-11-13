using Wallet.Application.Abstractions;
using Wallet.Application.Common;

namespace Wallet.Application.Accounts;

/// <summary>Bloqueia ou desbloqueia uma conta (operação administrativa, naturalmente idempotente).</summary>
public sealed class BlockAccountHandler(IAccountRepository accounts, IUnitOfWork unitOfWork)
{
    public Task<Result<AccountView>> BlockAsync(Guid accountId, CancellationToken cancellationToken) =>
        ChangeAsync(accountId, block: true, cancellationToken);

    public Task<Result<AccountView>> UnblockAsync(Guid accountId, CancellationToken cancellationToken) =>
        ChangeAsync(accountId, block: false, cancellationToken);

    private Task<Result<AccountView>> ChangeAsync(Guid accountId, bool block, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync<Result<AccountView>>(
            async ct =>
            {
                var locked = await accounts.LockAsync([accountId], loadTransfersOutSince: null, ct);
                if (locked.Count == 0)
                {
                    return AppErrors.AccountNotFound;
                }

                var account = locked[0];
                if (block)
                {
                    account.Block();
                }
                else
                {
                    account.Unblock();
                }

                return AccountView.From(account);
            },
            cancellationToken);
}
