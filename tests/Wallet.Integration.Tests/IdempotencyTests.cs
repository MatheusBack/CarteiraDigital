using System.Net;

using Wallet.Integration.Tests.Infrastructure;

namespace Wallet.Integration.Tests;

/// <summary>
/// RF06: repetir uma requisição de escrita com a mesma Idempotency-Key devolve a mesma resposta
/// sem repetir o efeito. É o que protege o cliente de pagar duas vezes quando a rede falha.
/// </summary>
public sealed class IdempotencyTests(WalletApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Same_transfer_sent_three_times_moves_money_once_and_returns_identical_responses()
    {
        var maria = await OpenAccountAsync("Maria", initialDeposit: 1_000m);
        var joao = await OpenAccountAsync("João");
        const string key = "transfer-maria-joao-001";

        var responses = new List<HttpResponseMessage>();
        for (var i = 0; i < 3; i++)
        {
            responses.Add(await TransferAsync(maria.Id, joao.Id, 250m, key));
        }

        var bodies = await Task.WhenAll(responses.Select(r => r.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)));
        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.Created);
        bodies.Distinct().Count().ShouldBe(1);

        responses[0].Headers.Contains("Idempotency-Replayed").ShouldBeFalse();
        responses[1].Headers.GetValues("Idempotency-Replayed").ShouldBe(["true"]);
        responses[2].Headers.GetValues("Idempotency-Replayed").ShouldBe(["true"]);

        (await BalanceAsync(maria.Id)).ShouldBe(750m);
        (await BalanceAsync(joao.Id)).ShouldBe(250m);
        (await StatementAsync(maria.Id)).Items.Count(i => i.Type == "transfer_out").ShouldBe(1);
    }

    [Fact]
    public async Task Same_key_with_a_different_body_returns_422_and_does_nothing()
    {
        var account = await OpenAccountAsync(initialDeposit: 100m);
        await PostAsync($"/accounts/{account.Id}/withdrawals", new { amount = 10m }, "chave-1");

        var response = await PostAsync($"/accounts/{account.Id}/withdrawals", new { amount = 20m }, "chave-1");

        await ShouldBeProblemAsync(response, HttpStatusCode.UnprocessableEntity, "idempotency-key-reused");
        (await BalanceAsync(account.Id)).ShouldBe(90m);
    }

    [Fact]
    public async Task Same_key_on_another_operation_is_a_different_request()
    {
        var account = await OpenAccountAsync(initialDeposit: 100m);
        await PostAsync($"/accounts/{account.Id}/deposits", new { amount = 10m }, "chave-1");

        var response = await PostAsync($"/accounts/{account.Id}/withdrawals", new { amount = 10m }, "chave-1");

        await ShouldBeProblemAsync(response, HttpStatusCode.UnprocessableEntity, "idempotency-key-reused");
    }

    [Fact]
    public async Task Amount_written_as_100_or_100_00_is_the_same_request()
    {
        var account = await OpenAccountAsync();
        var first = await PostAsync($"/accounts/{account.Id}/deposits", new { amount = 100m }, "chave-1");
        var second = await PostAsync($"/accounts/{account.Id}/deposits", new { amount = 100.00m }, "chave-1");

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.Headers.Contains("Idempotency-Replayed").ShouldBeTrue();
        (await BalanceAsync(account.Id)).ShouldBe(100m);
    }

    [Fact]
    public async Task Business_failures_are_also_replayed_so_the_client_gets_a_consistent_answer()
    {
        var account = await OpenAccountAsync(initialDeposit: 10m);
        var first = await PostAsync($"/accounts/{account.Id}/withdrawals", new { amount = 50m }, "saque-1");
        await ShouldBeProblemAsync(first, HttpStatusCode.UnprocessableEntity, "insufficient-funds");

        // Mesmo depois de ter saldo, a MESMA chave devolve a mesma resposta: para tentar de novo, use outra chave.
        await DepositAsync(account.Id, 100m);
        var retry = await PostAsync($"/accounts/{account.Id}/withdrawals", new { amount = 50m }, "saque-1");

        await ShouldBeProblemAsync(retry, HttpStatusCode.UnprocessableEntity, "insufficient-funds");
        retry.Headers.Contains("Idempotency-Replayed").ShouldBeTrue();
        (await BalanceAsync(account.Id)).ShouldBe(110m);
    }

    [Fact]
    public async Task Keys_belong_to_one_account_so_two_accounts_can_use_the_same_key()
    {
        var maria = await OpenAccountAsync("Maria");
        var joao = await OpenAccountAsync("João");

        await PostAsync($"/accounts/{maria.Id}/deposits", new { amount = 10m }, "deposito-1");
        await PostAsync($"/accounts/{joao.Id}/deposits", new { amount = 10m }, "deposito-1");

        (await BalanceAsync(maria.Id)).ShouldBe(10m);
        (await BalanceAsync(joao.Id)).ShouldBe(10m);
    }

    [Fact]
    public async Task Concurrent_requests_with_the_same_key_have_a_single_effect()
    {
        var account = await OpenAccountAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => PostAsync($"/accounts/{account.Id}/deposits", new { amount = 100m }, "mesma-chave")));

        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.OK);
        responses.Count(r => !r.Headers.Contains("Idempotency-Replayed")).ShouldBe(1);
        (await BalanceAsync(account.Id)).ShouldBe(100m);
    }
}
