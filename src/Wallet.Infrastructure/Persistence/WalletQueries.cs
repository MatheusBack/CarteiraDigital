using Microsoft.EntityFrameworkCore;

using Wallet.Application.Abstractions;
using Wallet.Application.Accounts;
using Wallet.Application.Transfers;

namespace Wallet.Infrastructure.Persistence;

internal sealed class WalletQueries(WalletDbContext db) : IWalletQueries
{
    public async Task<AccountView?> GetAccountAsync(Guid id, CancellationToken cancellationToken)
    {
        var account = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
        return account is null ? null : AccountView.From(account);
    }

    public async Task<TransferView?> GetTransferAsync(Guid id, CancellationToken cancellationToken)
    {
        var transfer = await db.Transfers.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
        return transfer is null ? null : TransferView.From(transfer);
    }

    public async Task<StatementPage?> GetStatementAsync(
        Guid accountId,
        StatementCursor? cursor,
        int limit,
        CancellationToken cancellationToken)
    {
        var account = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == accountId, cancellationToken);
        if (account is null)
        {
            return null;
        }

        const string seq = LedgerEntryConfiguration.SequenceProperty;
        var query = db.LedgerEntries.AsNoTracking().Where(e => e.AccountId == accountId);

        if (cursor is { } c)
        {
            query = query.Where(e => EF.Property<long>(e, seq) < c.Before);
        }

        // Busca 1 item a mais só para saber se existe próxima página.
        var rows = await query
            .OrderByDescending(e => EF.Property<long>(e, seq))
            .Take(limit + 1)
            .Select(e => new { Sequence = EF.Property<long>(e, seq), e.Type, e.Amount, e.OccurredAt, e.TransferId })
            .ToListAsync(cancellationToken);

        var page = rows.Take(limit).ToList();
        var next = rows.Count > limit ? new StatementCursor(page[^1].Sequence).Encode() : null;

        return new StatementPage(
            accountId,
            account.Balance.Reais,
            [.. page.Select(r => new StatementItem(r.Type, r.Amount.Reais, r.OccurredAt, r.TransferId))],
            next);
    }
}
