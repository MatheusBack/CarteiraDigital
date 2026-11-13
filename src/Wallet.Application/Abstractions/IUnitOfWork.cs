namespace Wallet.Application.Abstractions;

public interface IUnitOfWork
{
    /// <summary>
    /// Executa <paramref name="work"/> dentro de uma transação. No fim, as alterações são salvas e a
    /// transação é confirmada. Se uma exceção for lançada, tudo é desfeito.
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken);
}
