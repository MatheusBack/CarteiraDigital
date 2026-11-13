using System.Net;

using Wallet.Integration.Tests.Infrastructure;

namespace Wallet.Integration.Tests;

/// <summary>
/// Requisições simultâneas na mesma conta. Sem a trava (SELECT ... FOR UPDATE), duas requisições leriam o
/// mesmo saldo e as duas sacariam: o saldo ficaria errado.
/// </summary>
public sealed class ConcurrencyTests(WalletApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Concurrent_withdrawals_never_spend_more_than_the_balance()
    {
        var account = await OpenAccountAsync(initialDeposit: 100m);

        var responses = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => PostAsync($"/accounts/{account.Id}/withdrawals", new { amount = 10m })));

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(10);
        responses.Count(r => r.StatusCode == HttpStatusCode.UnprocessableEntity).ShouldBe(10);
        (await BalanceAsync(account.Id)).ShouldBe(0m);
    }

    [Fact]
    public async Task Opposite_transfers_at_the_same_time_do_not_deadlock_and_conserve_money()
    {
        var maria = await OpenAccountAsync("Maria", initialDeposit: 1_000m);
        var joao = await OpenAccountAsync("João", initialDeposit: 1_000m);

        var requests = Enumerable.Range(0, 10).SelectMany(_ => new[]
        {
            TransferAsync(maria.Id, joao.Id, 10m),
            TransferAsync(joao.Id, maria.Id, 5m),
        });
        var responses = await Task.WhenAll(requests);

        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.Created);
        (await BalanceAsync(maria.Id)).ShouldBe(950m);
        (await BalanceAsync(joao.Id)).ShouldBe(1_050m);
    }
}
