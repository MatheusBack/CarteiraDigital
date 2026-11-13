using System.Net;
using System.Net.Http.Json;

using Wallet.Integration.Tests.Infrastructure;

namespace Wallet.Integration.Tests;

public sealed class MovementsApiTests(WalletApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Deposit_increases_the_balance()
    {
        var account = await OpenAccountAsync();

        var response = await PostAsync($"/accounts/{account.Id}/deposits", new { amount = 150.75m });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var movement = (await response.Content.ReadFromJsonAsync<MovementDto>(TestContext.Current.CancellationToken))!;
        movement.Type.ShouldBe("deposit");
        movement.Amount.ShouldBe(150.75m);
        movement.Balance.ShouldBe(150.75m);
        (await BalanceAsync(account.Id)).ShouldBe(150.75m);
    }

    [Fact]
    public async Task Withdraw_decreases_the_balance()
    {
        var account = await OpenAccountAsync(initialDeposit: 100m);

        var response = await PostAsync($"/accounts/{account.Id}/withdrawals", new { amount = 30m });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await BalanceAsync(account.Id)).ShouldBe(70m);
    }

    [Fact]
    public async Task Withdraw_above_the_balance_returns_422_and_keeps_the_balance()
    {
        var account = await OpenAccountAsync(initialDeposit: 100m);

        var response = await PostAsync($"/accounts/{account.Id}/withdrawals", new { amount = 100.01m });

        await ShouldBeProblemAsync(response, HttpStatusCode.UnprocessableEntity, "insufficient-funds");
        (await BalanceAsync(account.Id)).ShouldBe(100m);
    }

    [Fact]
    public async Task Deposit_above_the_per_operation_limit_returns_422()
    {
        var account = await OpenAccountAsync();

        var response = await PostAsync($"/accounts/{account.Id}/deposits", new { amount = 50_000.01m });

        await ShouldBeProblemAsync(response, HttpStatusCode.UnprocessableEntity, "deposit-limit-exceeded");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(0.001)]
    public async Task Invalid_amount_returns_400(decimal amount)
    {
        var account = await OpenAccountAsync();

        var response = await PostAsync($"/accounts/{account.Id}/deposits", new { amount });

        await ShouldBeProblemAsync(response, HttpStatusCode.BadRequest, "invalid-amount");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("chave com espaço")]
    public async Task Write_without_a_valid_idempotency_key_returns_400(string? key)
    {
        var account = await OpenAccountAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/accounts/{account.Id}/deposits")
        {
            Content = JsonContent.Create(new { amount = 10m }),
        };
        if (key is not null)
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        }

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        await ShouldBeProblemAsync(response, HttpStatusCode.BadRequest, "idempotency-key-required");
        (await BalanceAsync(account.Id)).ShouldBe(0m);
    }

    [Fact]
    public async Task Movement_on_unknown_account_returns_404()
    {
        var response = await PostAsync($"/accounts/{Guid.NewGuid()}/deposits", new { amount = 10m });

        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "account-not-found");
    }

    [Fact]
    public async Task Blocked_account_cannot_move_money_until_unblocked()
    {
        var account = await OpenAccountAsync(initialDeposit: 100m);

        var block = await Client.PostAsync($"/admin/accounts/{account.Id}/block", null, TestContext.Current.CancellationToken);
        (await block.Content.ReadFromJsonAsync<AccountDto>(TestContext.Current.CancellationToken))!.Status.ShouldBe("blocked");

        await ShouldBeProblemAsync(
            await PostAsync($"/accounts/{account.Id}/deposits", new { amount = 10m }),
            HttpStatusCode.UnprocessableEntity,
            "account-blocked");
        await ShouldBeProblemAsync(
            await PostAsync($"/accounts/{account.Id}/withdrawals", new { amount = 10m }),
            HttpStatusCode.UnprocessableEntity,
            "account-blocked");

        await Client.PostAsync($"/admin/accounts/{account.Id}/unblock", null, TestContext.Current.CancellationToken);

        (await PostAsync($"/accounts/{account.Id}/withdrawals", new { amount = 10m })).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await BalanceAsync(account.Id)).ShouldBe(90m);
    }

    [Fact]
    public async Task Blocking_unknown_account_returns_404()
    {
        var response = await Client.PostAsync($"/admin/accounts/{Guid.NewGuid()}/block", null, TestContext.Current.CancellationToken);

        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "account-not-found");
    }

    [Fact]
    public async Task Statement_is_paginated_from_newest_to_oldest_without_gaps()
    {
        var account = await OpenAccountAsync();
        for (var i = 1; i <= 5; i++)
        {
            await DepositAsync(account.Id, i);
        }

        var page1 = await StatementAsync(account.Id, "?limit=2");
        var page2 = await StatementAsync(account.Id, $"?limit=2&cursor={page1.NextCursor}");
        var page3 = await StatementAsync(account.Id, $"?limit=2&cursor={page2.NextCursor}");

        page1.Items.Select(i => i.Amount).ShouldBe([5m, 4m]);
        page2.Items.Select(i => i.Amount).ShouldBe([3m, 2m]);
        page3.Items.Select(i => i.Amount).ShouldBe([1m]);
        page3.NextCursor.ShouldBeNull();
        page1.Balance.ShouldBe(15m);
        page1.Items.ShouldAllBe(i => i.Type == "deposit");
    }

    [Theory]
    [InlineData("?cursor=nao-e-um-cursor", "invalid-cursor")]
    [InlineData("?limit=0", "invalid-limit")]
    [InlineData("?limit=101", "invalid-limit")]
    public async Task Invalid_statement_parameters_return_400(string query, string code)
    {
        var account = await OpenAccountAsync();

        var response = await Client.GetAsync($"/accounts/{account.Id}/statement{query}", TestContext.Current.CancellationToken);

        await ShouldBeProblemAsync(response, HttpStatusCode.BadRequest, code);
    }

    [Fact]
    public async Task Statement_of_unknown_account_returns_404()
    {
        var response = await Client.GetAsync($"/accounts/{Guid.NewGuid()}/statement", TestContext.Current.CancellationToken);

        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "account-not-found");
    }
}
