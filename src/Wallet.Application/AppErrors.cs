using Wallet.Domain.Common;

namespace Wallet.Application;

/// <summary>Erros dos casos de uso (além dos erros de domínio em <see cref="Domain.Errors"/>). Códigos estáveis.</summary>
public static class AppErrors
{
    public static readonly Error AccountNotFound =
        new("account-not-found", "Conta não encontrada.");

    public static readonly Error InvalidDocument =
        new("invalid-document", "CPF inválido.");

    public static readonly Error InvalidHolderName =
        new("invalid-holder-name", "O nome do titular é obrigatório e deve ter no máximo 120 caracteres.");

    public static readonly Error DocumentAlreadyRegistered =
        new("document-already-registered", "Já existe uma conta para este CPF.");

    public static readonly Error IdempotencyKeyReused =
        new("idempotency-key-reused", "Esta Idempotency-Key já foi usada com uma requisição diferente.");

    public static readonly Error TransferDenied =
        new("transfer-denied", "Transferência recusada pela análise antifraude.");
}
