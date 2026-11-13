using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Wallet.Application.Idempotency;

/// <summary>
/// Chave de idempotência de uma requisição de escrita. A chave vale por conta (<see cref="AccountId"/>);
/// <see cref="RequestHash"/> identifica o conteúdo, para detectar a mesma chave usada com outro corpo.
/// </summary>
public sealed record IdempotencyKey(Guid AccountId, string Key, string RequestHash)
{
    public const int MaxLength = 100;

    /// <summary>A chave precisa ter de 1 a 100 caracteres ASCII visíveis (ex.: um UUID).</summary>
    public static bool IsValid(string? key) =>
        !string.IsNullOrEmpty(key)
        && key.Length <= MaxLength
        && key.All(c => c is >= '!' and <= '~');

    /// <summary>Cria a chave calculando o SHA-256 de operação + conta + corpo da requisição.</summary>
    public static IdempotencyKey For<TRequest>(string operation, Guid accountId, string key, TRequest request)
    {
        var payload = $"{operation}|{accountId}|{JsonSerializer.Serialize(request)}";
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        return new IdempotencyKey(accountId, key, hash);
    }
}
