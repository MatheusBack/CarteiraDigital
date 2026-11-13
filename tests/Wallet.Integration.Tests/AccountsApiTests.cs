using System.Net;
using System.Net.Http.Json;

using Wallet.Integration.Tests.Infrastructure;

namespace Wallet.Integration.Tests;

public sealed class AccountsApiTests(WalletApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Opening_an_account_returns_201_with_location_and_masked_cpf()
    {
        var response = await Client.PostAsJsonAsync(
            "/accounts",
            new { holderName = "  Maria da Silva  ", document = "529.982.247-25" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var account = (await response.Content.ReadFromJsonAsync<AccountDto>(TestContext.Current.CancellationToken))!;
        response.Headers.Location!.ToString().ShouldBe($"/accounts/{account.Id}");

        account.HolderName.ShouldBe("Maria da Silva");
        account.Document.ShouldBe("***.982.247-**");
        account.Balance.ShouldBe(0m);
        account.Status.ShouldBe("active");
    }

    [Fact]
    public async Task Opened_account_can_be_read_back()
    {
        var opened = await OpenAccountAsync("João Souza");

        var read = await Client.GetFromJsonAsync<AccountDto>($"/accounts/{opened.Id}", TestContext.Current.CancellationToken);

        read.ShouldBe(opened);
    }

    [Theory]
    [InlineData("123.456.789-00")]
    [InlineData("111.111.111-11")]
    [InlineData("abc")]
    [InlineData(null)]
    public async Task Invalid_cpf_returns_400(string? document)
    {
        var response = await Client.PostAsJsonAsync(
            "/accounts", new { holderName = "Maria", document }, TestContext.Current.CancellationToken);

        await ShouldBeProblemAsync(response, HttpStatusCode.BadRequest, "invalid-document");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Blank_holder_name_returns_400(string? holderName)
    {
        var response = await Client.PostAsJsonAsync(
            "/accounts", new { holderName, document = NewCpf() }, TestContext.Current.CancellationToken);

        await ShouldBeProblemAsync(response, HttpStatusCode.BadRequest, "invalid-holder-name");
    }

    [Fact]
    public async Task Same_cpf_cannot_open_two_accounts()
    {
        var cpf = NewCpf();
        await Client.PostAsJsonAsync("/accounts", new { holderName = "Maria", document = cpf }, TestContext.Current.CancellationToken);

        var second = await Client.PostAsJsonAsync(
            "/accounts", new { holderName = "Outra Maria", document = cpf }, TestContext.Current.CancellationToken);

        await ShouldBeProblemAsync(second, HttpStatusCode.Conflict, "document-already-registered");
    }

    [Fact]
    public async Task Unknown_account_returns_404()
    {
        var response = await Client.GetAsync($"/accounts/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        await ShouldBeProblemAsync(response, HttpStatusCode.NotFound, "account-not-found");
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_checks_are_healthy(string url)
    {
        var response = await Client.GetAsync(url, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
