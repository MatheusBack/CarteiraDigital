using Wallet.Application.Abstractions;
using Wallet.Application.Common;
using Wallet.Domain.Accounts;

namespace Wallet.Application.Accounts;

public sealed record OpenAccountCommand(string? HolderName, string? Document);

public sealed class OpenAccountHandler(IAccountRepository accounts, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task<Result<AccountView>> HandleAsync(OpenAccountCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!Cpf.TryParse(command.Document, out var document))
        {
            return AppErrors.InvalidDocument;
        }

        var name = command.HolderName?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > Account.MaxHolderNameLength)
        {
            return AppErrors.InvalidHolderName;
        }

        return await unitOfWork.ExecuteInTransactionAsync<Result<AccountView>>(
            async ct =>
            {
                if (await accounts.DocumentExistsAsync(document, ct))
                {
                    return AppErrors.DocumentAlreadyRegistered;
                }

                var account = Account.Open(name, document, clock.GetUtcNow());
                accounts.Add(account);
                return AccountView.From(account);
            },
            cancellationToken);
    }
}
