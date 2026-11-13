namespace Wallet.Application.Abstractions;

/// <summary>Resposta já registrada para uma Idempotency-Key.</summary>
public sealed record StoredResponse(string RequestHash, string? ResponseJson);

public interface IIdempotencyStore
{
    /// <summary>
    /// Tenta reservar a chave dentro da transação atual. Retorna <c>null</c> se a chave é nova
    /// (reservada agora) ou a resposta já registrada para ela. Se outra requisição com a mesma chave
    /// estiver em andamento, espera ela terminar.
    /// </summary>
    Task<StoredResponse?> TryClaimAsync(
        Guid accountId,
        string key,
        string requestHash,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>Grava a resposta da chave reservada, na mesma transação da operação.</summary>
    Task CompleteAsync(Guid accountId, string key, string responseJson, CancellationToken cancellationToken);
}
