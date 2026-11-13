using System.Net;
using System.Net.Http.Json;

using Wallet.Integration.Tests.Infrastructure;

namespace Wallet.Integration.Tests;

public sealed class TransfersApiTests(WalletApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Transfer_moves_money_and_writes_both_ledger_entries()
    {
        var maria = await OpenAccountAsync("Maria", initialDeposit: 1_000m);
        var joao = await OpenAccountAsync("João");

        var response = await TransferAsync(maria.Id, joao.Id, 250m);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var transfer = (await response.Content.ReadFromJsonAsync<TransferDto>(TestContext.Current.CancellationToken))!;
        response.Headers.Location!.ToString().ShouldBe($"/transfers/{transfer.Id}");
        transfer.Status.ShouldBe("completed");
        transfer.Amount.ShouldBe(250m);

        (await BalanceAsync(maria.Id)).ShouldBe(750m);
        (await BalanceAsync(joao.Id)).ShouldBe(250m);

        var outEntry = (await StatementAsync(maria.Id)).Items[0];
        var inEntry = (await StatementAsync(joao.Id)).Items[0];
        outEntry.Type.ShouldBe("transfer_out");
        inEntry.Type.ShouldBe("transfer_in");
        outEntry.TransferId.ShouldBe(transfer.Id);
        inEntry.TransferId.ShouldBe(transfer.Id);
    }

    [Fact]
    public async Task Completed_transfer_can_be_read_back()
    {
        var maria = await OpenAccountAsync("Maria", initialDeposit: 100m);
        var joao = await OpenAccountAsync("João");
        var created = await (await TransferAsync(maria.Id, joao.Id, 10m)).Content
            .ReadFromJsonAsync<TransferDto>(TestContext.Current.CancellationToken);

        var read = await Client.GetFromJsonAsync<TransferDto>($"/transfers/{created!.Id}", TestContext.Current.CancellationToken);

        read.ShouldBe(created);
    }

    [Fact]
    public async Task Insufficient_funds_returns_422_and_changes_nothing()
    {
        var maria = await OpenAccountAsync("Maria", initialDeposit: 100m);
        var joao = await OpenAccountAsync("João");

        var response = await TransferAsync(maria.Id, joao.Id, 100.01m);

        await ShouldBeProblemAsync(response, HttpStatusCode.UnprocessableEntity, "insufficient-funds");
        (await BalanceAsync(maria.Id)).ShouldBe(100m);
        (await BalanceAsync(joao.Id)).ShouldBe(0m);
    }

    [Fact]
    public async Task Blocked_destination_returns_422_and_origin_is_not_debited()
    {
        var maria = await OpenAccountAsync("Maria", initialDeposit: 100m);
        var joao = await OpenAccountAsync("João");
        await Client.PostAsync($"/admin/accounts/{joao.Id}/block", null, TestContext.Current.CancellationToken);

        var response = await TransferAsync(maria.Id, joao.Id, 50m);

        await ShouldBeProblemAsync(response, HttpStatusCode.UnprocessableEntity, "destination-account-blocked");
        (await BalanceAsync(maria.Id)).ShouldBe(100m);
        (await StatementAsync(maria.Id)).Items.ShouldAllBe(i => i.Type == "deposit");
    }

    [Fact]
    public async Task Transfer_to_the_same_account_returns_422()
    {
        var maria = await OpenAccountAsync("Maria", initialDeposit: 100m);

        var response = await TransferAsync(maria.Id, maria.Id, 10m);

        await ShouldBeProblemAsync(response, HttpStatusCode.UnprocessableEntity, "same-account");
    }

    [Fact]
    public async Task Transfer_to_unknown_account_returns_404()
    {
        var maria = await OpenAccountAsync("Maria", initialDeposit: 100m);

        var response = await TransferAsync(maria.Id, Guid.NewGuid(), 10m);

        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "account-not-found");
        (await BalanceAsync(maria.Id)).ShouldBe(100m);
    }

    [Fact]
    public async Task Daily_limit_is_enforced_through_the_api()
    {
        var maria = await OpenAccountAsync("Maria", initialDeposit: 6_000m);
        var joao = await OpenAccountAsync("João");

        (await TransferAsync(maria.Id, joao.Id, 4_000m)).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await TransferAsync(maria.Id, joao.Id, 1_000m)).StatusCode.ShouldBe(HttpStatusCode.Created);
        var overLimit = await TransferAsync(maria.Id, joao.Id, 0.01m);

        await ShouldBeProblemAsync(overLimit, HttpStatusCode.UnprocessableEntity, "daily-limit-exceeded");
        (await BalanceAsync(maria.Id)).ShouldBe(1_000m);
    }

    [Fact]
    public async Task Unknown_transfer_returns_404()
    {
        var response = await Client.GetAsync($"/transfers/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "transfer-not-found");
    }
}
