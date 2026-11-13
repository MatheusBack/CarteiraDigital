using Npgsql;

using Wallet.Integration.Tests.Infrastructure;

namespace Wallet.Integration.Tests;

/// <summary>
/// Defesa em profundidade: mesmo que um bug passe pelo código, o banco recusa estados inválidos.
/// Estes testes falam SQL direto com o Postgres, sem passar pela API.
/// </summary>
public sealed class DatabaseGuardsTests(WalletApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Database_rejects_a_negative_balance()
    {
        var account = await OpenAccountAsync();

        var error = await Should.ThrowAsync<PostgresException>(() =>
            ExecuteAsync("UPDATE accounts SET balance_cents = -1 WHERE id = @id", account.Id));

        error.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        error.ConstraintName.ShouldBe("ck_accounts_balance_non_negative");
    }

    [Fact]
    public async Task Ledger_entries_cannot_be_updated()
    {
        var account = await OpenAccountAsync(initialDeposit: 100m);

        var error = await Should.ThrowAsync<PostgresException>(() =>
            ExecuteAsync("UPDATE ledger_entries SET amount_cents = 1 WHERE account_id = @id", account.Id));

        error.MessageText.ShouldContain("imutável");
        (await BalanceAsync(account.Id)).ShouldBe(100m);
    }

    [Fact]
    public async Task Every_balance_matches_its_ledger_after_mixed_operations()
    {
        var maria = await OpenAccountAsync("Maria", initialDeposit: 1_000m);
        var joao = await OpenAccountAsync("João", initialDeposit: 50m);
        await TransferAsync(maria.Id, joao.Id, 300m);
        await TransferAsync(joao.Id, maria.Id, 20m);
        await PostAsync($"/accounts/{maria.Id}/withdrawals", new { amount = 99.99m });
        await PostAsync($"/accounts/{joao.Id}/withdrawals", new { amount = 1_000_000m }); // falha: não altera nada

        await using var connection = await Factory.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT count(*) FROM (
                SELECT a.id
                FROM accounts a
                LEFT JOIN ledger_entries e ON e.account_id = a.id
                GROUP BY a.id, a.balance_cents
                HAVING a.balance_cents <> COALESCE(SUM(CASE WHEN e.type IN (1, 3) THEN e.amount_cents ELSE -e.amount_cents END), 0)
            ) AS divergentes
            """,
            connection);

        var divergentAccounts = (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;

        divergentAccounts.ShouldBe(0);
        (await BalanceAsync(maria.Id)).ShouldBe(620.01m);
        (await BalanceAsync(joao.Id)).ShouldBe(330m);
    }

    private async Task ExecuteAsync(string sql, Guid id)
    {
        await using var connection = await Factory.OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
