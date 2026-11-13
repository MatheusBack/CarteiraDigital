using System.Net;
using System.Net.Http.Json;

namespace Wallet.Integration.Tests.Infrastructure;

public sealed record AccountDto(Guid Id, string HolderName, string Document, decimal Balance, string Status, DateTimeOffset OpenedAt);

public sealed record MovementDto(Guid AccountId, string Type, decimal Amount, decimal Balance, DateTimeOffset OccurredAt);

public sealed record TransferDto(Guid Id, Guid FromAccountId, Guid ToAccountId, decimal Amount, string Status, DateTimeOffset CreatedAt);

public sealed record StatementItemDto(string Type, decimal Amount, DateTimeOffset OccurredAt, Guid? TransferId);

public sealed record StatementDto(Guid AccountId, decimal Balance, IReadOnlyList<StatementItemDto> Items, string? NextCursor);

public sealed record ProblemDto(string Type, string Title, int Status, string Code, Guid? TransferId);

/// <summary>Base dos testes de API: limpa o banco antes de cada teste e oferece atalhos legíveis.</summary>
[Collection(ApiTestGroup.Name)]
public abstract class ApiTestBase(WalletApiFactory factory) : IAsyncLifetime
{
    private static int _cpfSeed = 100_000_000;

    protected WalletApiFactory Factory { get; } = factory;

    protected HttpClient Client { get; } = factory.CreateClient();

    public async ValueTask InitializeAsync() => await Factory.ResetAsync();

    public ValueTask DisposeAsync()
    {
        Client.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    /// <summary>Gera um CPF válido diferente a cada chamada.</summary>
    protected static string NewCpf()
    {
        var digits = Interlocked.Increment(ref _cpfSeed).ToString("D9", System.Globalization.CultureInfo.InvariantCulture)
            .Select(c => c - '0')
            .ToList();

        for (var length = 9; length <= 10; length++)
        {
            var sum = 0;
            for (var i = 0; i < length; i++)
            {
                sum += digits[i] * (length + 1 - i);
            }

            var remainder = sum % 11;
            digits.Add(remainder < 2 ? 0 : 11 - remainder);
        }

        return string.Concat(digits);
    }

    protected async Task<AccountDto> OpenAccountAsync(string holderName = "Maria da Silva", decimal initialDeposit = 0)
    {
        var response = await Client.PostAsJsonAsync("/accounts", new { holderName, document = NewCpf() }, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var account = (await response.Content.ReadFromJsonAsync<AccountDto>(TestContext.Current.CancellationToken))!;

        if (initialDeposit > 0)
        {
            await DepositAsync(account.Id, initialDeposit);
        }

        return account;
    }

    protected Task<HttpResponseMessage> PostAsync(string url, object body, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", idempotencyKey ?? Guid.NewGuid().ToString());
        return Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>Deposita em partes de até R$ 50.000 (limite por operação).</summary>
    protected async Task DepositAsync(Guid accountId, decimal amount)
    {
        while (amount > 0)
        {
            var part = Math.Min(amount, 50_000m);
            var response = await PostAsync($"/accounts/{accountId}/deposits", new { amount = part });
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            amount -= part;
        }
    }

    protected Task<HttpResponseMessage> TransferAsync(Guid from, Guid to, decimal amount, string? idempotencyKey = null) =>
        PostAsync("/transfers", new { fromAccountId = from, toAccountId = to, amount }, idempotencyKey);

    protected async Task<decimal> BalanceAsync(Guid accountId) =>
        (await Client.GetFromJsonAsync<AccountDto>($"/accounts/{accountId}", TestContext.Current.CancellationToken))!.Balance;

    protected async Task<StatementDto> StatementAsync(Guid accountId, string query = "") =>
        (await Client.GetFromJsonAsync<StatementDto>($"/accounts/{accountId}/statement{query}", TestContext.Current.CancellationToken))!;

    /// <summary>Confere status, content-type <c>application/problem+json</c> e o código estável do erro.</summary>
    protected static async Task<ProblemDto> ShouldBeProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        ArgumentNullException.ThrowIfNull(response);

        response.StatusCode.ShouldBe(status);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var problem = (await response.Content.ReadFromJsonAsync<ProblemDto>(TestContext.Current.CancellationToken))!;
        problem.Code.ShouldBe(code);
        problem.Status.ShouldBe((int)status);
        problem.Type.ShouldEndWith("#" + code);
        return problem;
    }
}
