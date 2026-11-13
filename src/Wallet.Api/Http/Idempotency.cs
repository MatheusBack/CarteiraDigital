using Wallet.Application.Idempotency;

namespace Wallet.Api.Http;

internal static class Idempotency
{
    public const string KeyHeader = "Idempotency-Key";

    /// <summary>Presente (valor <c>true</c>) quando a resposta é a repetição de uma requisição anterior.</summary>
    public const string ReplayedHeader = "Idempotency-Replayed";

    /// <summary>
    /// Escreve a resposta de uma operação idempotente. Uma repetição gera exatamente a mesma resposta
    /// (mesmo status e corpo), só com o header <see cref="ReplayedHeader"/> a mais.
    /// </summary>
    public static IResult Respond<T>(HttpContext http, Idempotent<T> outcome, Func<T, IResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(onSuccess);

        if (outcome.Replayed)
        {
            http.Response.Headers[ReplayedHeader] = "true";
        }

        return outcome.Result.IsSuccess
            ? onSuccess(outcome.Result.Value)
            : Problems.For(outcome.Result.Error!);
    }

    /// <summary>
    /// Remove zeros à direita (100.00 → 100), para que <c>100</c> e <c>100.00</c> sejam a mesma
    /// requisição no cálculo do hash.
    /// </summary>
    public static decimal Canonical(decimal value) => value / 1.000000000000000000000000000000000m;
}
