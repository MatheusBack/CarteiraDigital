using Wallet.Domain.Accounts;

namespace Wallet.Application.Abstractions;

public interface IAccountRepository
{
    void Add(Account account);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> DocumentExistsAsync(Cpf document, CancellationToken cancellationToken);

    /// <summary>
    /// Carrega e TRAVA as contas (SELECT ... FOR UPDATE) até o fim da transação.
    /// As travas são obtidas sempre na ordem dos Ids, para que A→B e B→A simultâneas não gerem deadlock.
    /// </summary>
    /// <param name="ids">Contas a travar.</param>
    /// <param name="loadTransfersOutSince">
    /// Se informado, carrega as transferências enviadas a partir desta data (necessárias para o limite diário).
    /// </param>
    /// <param name="cancellationToken">Cancelamento.</param>
    Task<IReadOnlyList<Account>> LockAsync(
        IReadOnlyCollection<Guid> ids,
        DateTimeOffset? loadTransfersOutSince,
        CancellationToken cancellationToken);
}
