using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;

using Wallet.Integration.Tests.Infrastructure;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Wallet.Integration.Tests;

/// <summary>
/// RF05: transferências acima de R$ 1.000 passam pelo antifraude (simulado com WireMock).
/// Recusa → transferência negada. Sem resposta em 2 s → "em análise". Em nenhum dos dois casos o dinheiro se move.
/// </summary>
public sealed class FraudCheckTests(WalletApiFactory factory) : ApiTestBase(factory)
{
    private int FraudCalls => Factory.FraudApi.LogEntries.Count(e => e.RequestMessage?.Path == "/check");

    [Fact]
    public async Task Up_to_1000_reais_the_fraud_service_is_not_called()
    {
        var (maria, joao) = await TwoAccountsAsync();

        var response = await TransferAsync(maria, joao, 1_000m);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        FraudCalls.ShouldBe(0);
    }

    [Fact]
    public async Task Above_1000_reais_an_approved_transfer_completes()
    {
        var (maria, joao) = await TwoAccountsAsync();

        var response = await TransferAsync(maria, joao, 1_000.01m);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        FraudCalls.ShouldBe(1);
        var sent = Factory.FraudApi.LogEntries.Single().RequestMessage!.Body!;
        sent.ShouldContain("\"amountCents\":100001");
        sent.ShouldContain($"\"fromAccountId\":\"{maria}\"");
        (await BalanceAsync(joao)).ShouldBe(1_000.01m);
    }

    [Fact]
    public async Task Denied_transfer_returns_422_is_recorded_and_moves_no_money()
    {
        var (maria, joao) = await TwoAccountsAsync();
        FraudRespondsWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new { decision = "denied" }));

        var response = await TransferAsync(maria, joao, 2_000m);

        var problem = await ShouldBeProblemAsync(response, HttpStatusCode.UnprocessableEntity, "transfer-denied");
        problem.TransferId.ShouldNotBeNull();
        (await TransferStatusAsync(problem.TransferId.Value)).ShouldBe("denied");
        (await BalanceAsync(maria)).ShouldBe(5_000m);
        (await BalanceAsync(joao)).ShouldBe(0m);
    }

    [Fact]
    public async Task Slow_fraud_service_puts_the_transfer_in_review_after_about_2_seconds()
    {
        var (maria, joao) = await TwoAccountsAsync();
        FraudRespondsWith(Response.Create()
            .WithStatusCode(200)
            .WithBodyAsJson(new { decision = "approved" })
            .WithDelay(TimeSpan.FromSeconds(3)));

        var stopwatch = Stopwatch.StartNew();
        var response = await TransferAsync(maria, joao, 2_000m);
        stopwatch.Stop();

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var transfer = (await response.Content.ReadFromJsonAsync<TransferDto>(TestContext.Current.CancellationToken))!;
        transfer.Status.ShouldBe("in_review");
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(2.9));
        (await BalanceAsync(maria)).ShouldBe(5_000m);
        (await BalanceAsync(joao)).ShouldBe(0m);
    }

    [Fact]
    public async Task Transient_errors_are_retried()
    {
        var (maria, joao) = await TwoAccountsAsync();
        Factory.FraudApi.Reset();
        var check = Request.Create().WithPath("/check").UsingPost();
        Factory.FraudApi.Given(check).InScenario("instável").WillSetStateTo("falhou 1x")
            .RespondWith(Response.Create().WithStatusCode(500));
        Factory.FraudApi.Given(check).InScenario("instável").WhenStateIs("falhou 1x").WillSetStateTo("falhou 2x")
            .RespondWith(Response.Create().WithStatusCode(503));
        Factory.FraudApi.Given(check).InScenario("instável").WhenStateIs("falhou 2x")
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new { decision = "approved" }));

        var response = await TransferAsync(maria, joao, 2_000m);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        FraudCalls.ShouldBe(3);
        (await BalanceAsync(joao)).ShouldBe(2_000m);
    }

    [Fact]
    public async Task Fraud_service_always_failing_puts_the_transfer_in_review()
    {
        var (maria, joao) = await TwoAccountsAsync();
        FraudRespondsWith(Response.Create().WithStatusCode(500));

        var response = await TransferAsync(maria, joao, 2_000m);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        FraudCalls.ShouldBe(3);
        (await BalanceAsync(maria)).ShouldBe(5_000m);
    }

    [Fact]
    public async Task Unexpected_answer_from_fraud_service_is_not_treated_as_approval()
    {
        var (maria, joao) = await TwoAccountsAsync();
        FraudRespondsWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new { decision = "talvez" }));

        var response = await TransferAsync(maria, joao, 2_000m);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await BalanceAsync(maria)).ShouldBe(5_000m);
    }

    private async Task<(Guid Maria, Guid Joao)> TwoAccountsAsync()
    {
        var maria = await OpenAccountAsync("Maria", initialDeposit: 5_000m);
        var joao = await OpenAccountAsync("João");
        return (maria.Id, joao.Id);
    }

    private void FraudRespondsWith(IResponseBuilder response)
    {
        Factory.FraudApi.Reset();
        Factory.FraudApi.Given(Request.Create().WithPath("/check").UsingPost()).RespondWith(response);
    }

    private async Task<string> TransferStatusAsync(Guid transferId) =>
        (await Client.GetFromJsonAsync<TransferDto>($"/transfers/{transferId}", TestContext.Current.CancellationToken))!.Status;
}
