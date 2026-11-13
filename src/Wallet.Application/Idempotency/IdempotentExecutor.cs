using System.Text.Json;

using Wallet.Application.Abstractions;
using Wallet.Application.Common;
using Wallet.Domain.Common;

namespace Wallet.Application.Idempotency;

/// <summary>Resultado de uma operação idempotente. <see cref="Replayed"/> indica uma resposta repetida.</summary>
public sealed record Idempotent<T>(Result<T> Result, bool Replayed);

/// <summary>
/// Garante que uma operação de escrita tenha efeito no máximo uma vez por Idempotency-Key.
/// A chave é reservada e a resposta é gravada na MESMA transação da operação: ou as duas
/// coisas acontecem, ou nenhuma.
/// </summary>
public sealed class IdempotentExecutor(IUnitOfWork unitOfWork, IIdempotencyStore store, TimeProvider clock)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<Idempotent<T>> ExecuteAsync<T>(
        IdempotencyKey key,
        Func<CancellationToken, Task<Result<T>>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(operation);

        return unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                var existing = await store.TryClaimAsync(
                    key.AccountId, key.Key, key.RequestHash, clock.GetUtcNow(), ct);

                if (existing is not null)
                {
                    return existing.RequestHash == key.RequestHash
                        ? new Idempotent<T>(Deserialize<T>(existing), Replayed: true)
                        : new Idempotent<T>(AppErrors.IdempotencyKeyReused, Replayed: false);
                }

                var result = await operation(ct);
                await store.CompleteAsync(key.AccountId, key.Key, Serialize(result), ct);
                return new Idempotent<T>(result, Replayed: false);
            },
            cancellationToken);
    }

    private static string Serialize<T>(Result<T> result) =>
        JsonSerializer.Serialize(
            result.IsSuccess
                ? new Envelope<T>(result.Value, null, null)
                : new Envelope<T>(default, result.Error!.Code, result.Error.Message),
            JsonOptions);

    private static Result<T> Deserialize<T>(StoredResponse stored)
    {
        var envelope = JsonSerializer.Deserialize<Envelope<T>>(
            stored.ResponseJson ?? throw new InvalidOperationException("Chave de idempotência sem resposta gravada."),
            JsonOptions)!;

        return envelope.ErrorCode is null
            ? Result<T>.Ok(envelope.Value!)
            : Result<T>.Fail(new Error(envelope.ErrorCode, envelope.ErrorMessage ?? string.Empty));
    }

    private sealed record Envelope<T>(T? Value, string? ErrorCode, string? ErrorMessage);
}
