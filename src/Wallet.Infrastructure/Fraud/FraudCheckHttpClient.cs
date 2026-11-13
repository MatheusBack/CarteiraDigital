using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using Polly.Timeout;

using Wallet.Application.Abstractions;

namespace Wallet.Infrastructure.Fraud;

/// <summary>Configuração do serviço antifraude (seção <c>FraudApi</c>).</summary>
public sealed class FraudApiOptions
{
    public const string Section = "FraudApi";

    public Uri? BaseUrl { get; set; }

    /// <summary>Tempo máximo total, somando tentativas (RF05: 2 segundos).</summary>
    public TimeSpan TotalTimeout { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Tempo máximo de cada tentativa.</summary>
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(1);

    public int MaxRetries { get; set; } = 2;
}

/// <summary>
/// Cliente HTTP do antifraude. Timeout e retry ficam no pipeline de resiliência (Polly);
/// aqui só traduzimos a resposta. Qualquer falha vira <see cref="FraudDecision.Unavailable"/>.
/// </summary>
internal sealed partial class FraudCheckHttpClient(HttpClient http, ILogger<FraudCheckHttpClient> logger) : IFraudCheck
{
    public async Task<FraudDecision> CheckAsync(FraudCheckRequest request, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.PostAsJsonAsync("check", request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                LogUnavailable(logger, request.TransferId, $"HTTP {(int)response.StatusCode}", null);
                return FraudDecision.Unavailable;
            }

            var body = await response.Content.ReadFromJsonAsync<FraudCheckResponse>(cancellationToken);
            return body?.Decision switch
            {
                "approved" => FraudDecision.Approved,
                "denied" => FraudDecision.Denied,
                _ => FraudDecision.Unavailable,
            };
        }
        catch (Exception ex) when (
            ex is HttpRequestException or TimeoutRejectedException or JsonException or NotSupportedException
            || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            LogUnavailable(logger, request.TransferId, ex.GetType().Name, ex);
            return FraudDecision.Unavailable;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Antifraude indisponível para a transferência {TransferId}: {Reason}")]
    private static partial void LogUnavailable(ILogger logger, Guid transferId, string reason, Exception? exception);

    private sealed record FraudCheckResponse(string? Decision);
}
