using Wallet.Application.Accounts;
using Wallet.Application.Transfers;

namespace Wallet.Application.Abstractions;

/// <summary>Consultas somente leitura (não travam nada).</summary>
public interface IWalletQueries
{
    Task<AccountView?> GetAccountAsync(Guid id, CancellationToken cancellationToken);

    Task<TransferView?> GetTransferAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Extrato do mais recente para o mais antigo, paginado por cursor (keyset).</summary>
    Task<StatementPage?> GetStatementAsync(
        Guid accountId,
        StatementCursor? cursor,
        int limit,
        CancellationToken cancellationToken);
}
