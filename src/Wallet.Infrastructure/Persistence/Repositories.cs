using Microsoft.EntityFrameworkCore;

using Wallet.Application.Abstractions;
using Wallet.Domain.Accounts;
using Wallet.Domain.Transfers;

namespace Wallet.Infrastructure.Persistence;

internal sealed class AccountRepository(WalletDbContext db) : IAccountRepository
{
    public void Add(Account account) => db.Accounts.Add(account);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        db.Accounts.AnyAsync(a => a.Id == id, cancellationToken);

    public Task<bool> DocumentExistsAsync(Cpf document, CancellationToken cancellationToken) =>
        db.Accounts.AnyAsync(a => a.Document == document, cancellationToken);

    public async Task<IReadOnlyList<Account>> LockAsync(
        IReadOnlyCollection<Guid> ids,
        DateTimeOffset? loadTransfersOutSince,
        CancellationToken cancellationToken)
    {
        var idArray = ids.Distinct().ToArray();

        // Trava as linhas na ordem do Postgres (ORDER BY id). Todas as transações usam a mesma
        // ordem, então duas transferências opostas (A→B e B→A) esperam uma pela outra sem deadlock.
        await db.Database.ExecuteSqlAsync(
            $"SELECT 1 FROM accounts WHERE id = ANY({idArray}) ORDER BY id FOR UPDATE",
            cancellationToken);

        var query = db.Accounts.Where(a => idArray.Contains(a.Id));

        if (loadTransfersOutSince is { } since)
        {
            query = query.Include(a => a.Entries.Where(e => e.Type == EntryType.TransferOut && e.OccurredAt >= since));
        }

        return await query.ToListAsync(cancellationToken);
    }
}

internal sealed class TransferRepository(WalletDbContext db) : ITransferRepository
{
    public void Add(Transfer transfer) => db.Transfers.Add(transfer);
}

internal sealed class UnitOfWork(WalletDbContext db) : IUnitOfWork
{
    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> work,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var result = await work(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}

internal sealed class IdempotencyStore(WalletDbContext db) : IIdempotencyStore
{
    public async Task<StoredResponse?> TryClaimAsync(
        Guid accountId,
        string key,
        string requestHash,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Se outra transação acabou de inserir a mesma chave, o Postgres espera ela terminar:
        // se ela confirmar, nada é inserido aqui e devolvemos a resposta dela.
        var inserted = await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO idempotency_keys (account_id, idempotency_key, request_hash, created_at)
            VALUES ({accountId}, {key}, {requestHash}, {now})
            ON CONFLICT (account_id, idempotency_key) DO NOTHING
            """,
            cancellationToken);

        if (inserted == 1)
        {
            return null;
        }

        return await db.IdempotencyKeys
            .AsNoTracking()
            .Where(r => r.AccountId == accountId && r.Key == key)
            .Select(r => new StoredResponse(r.RequestHash, r.Response))
            .SingleAsync(cancellationToken);
    }

    public Task CompleteAsync(Guid accountId, string key, string responseJson, CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlAsync(
            $"""
            UPDATE idempotency_keys SET response = CAST({responseJson} AS jsonb)
            WHERE account_id = {accountId} AND idempotency_key = {key}
            """,
            cancellationToken);
}
