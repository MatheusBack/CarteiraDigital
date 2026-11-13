using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

using Npgsql;

using Wallet.Application.Idempotency;
using Wallet.Domain.Common;

namespace Wallet.Api.Http;

/// <summary>Erros que só existem na camada HTTP.</summary>
internal static class ApiErrors
{
    public static readonly Error IdempotencyKeyRequired = new(
        "idempotency-key-required",
        $"Envie o header Idempotency-Key (1 a {IdempotencyKey.MaxLength} caracteres ASCII, ex.: um UUID).");

    public static readonly Error InvalidCursor = new("invalid-cursor", "Cursor de paginação inválido.");

    public static readonly Error InvalidLimit = new("invalid-limit", "O parâmetro limit deve estar entre 1 e 100.");

    public static readonly Error TransferNotFound = new("transfer-not-found", "Transferência não encontrada.");

    public static readonly Error Conflict = new("conflict", "A operação conflita com outra feita ao mesmo tempo. Tente novamente.");
}

/// <summary>
/// Converte <see cref="Error"/> em <c>application/problem+json</c> (RFC 9457). O <c>type</c> aponta para a
/// documentação do erro e o campo <c>code</c> é estável: clientes devem depender dele, não do texto.
/// </summary>
internal static class Problems
{
    private const string DocsUrl = "https://github.com/MatheusBack/CarteiraDigital/blob/main/docs/erros.md#";

    public static IResult For(Error error, IDictionary<string, object?>? extensions = null)
    {
        ArgumentNullException.ThrowIfNull(error);

        var allExtensions = new Dictionary<string, object?>(extensions ?? new Dictionary<string, object?>())
        {
            ["code"] = error.Code,
        };

        return TypedResults.Problem(
            title: error.Message,
            statusCode: StatusFor(error.Code),
            type: TypeFor(error.Code),
            extensions: allExtensions);
    }

    public static string TypeFor(string code) => DocsUrl + code;

    public static int StatusFor(string code) => code switch
    {
        "account-not-found" or "transfer-not-found" => StatusCodes.Status404NotFound,
        "document-already-registered" or "conflict" => StatusCodes.Status409Conflict,
        "invalid-amount"
            or "invalid-document"
            or "invalid-holder-name"
            or "idempotency-key-required"
            or "invalid-cursor"
            or "invalid-limit" => StatusCodes.Status400BadRequest,

        // Regras de negócio: a requisição é válida, mas não pode ser atendida.
        _ => StatusCodes.Status422UnprocessableEntity,
    };
}

/// <summary>
/// Violação de unicidade no banco (ex.: duas contas com o mesmo CPF abertas ao mesmo tempo) vira 409, não 500.
/// </summary>
internal sealed class DatabaseConflictHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } })
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails =
            {
                Status = StatusCodes.Status409Conflict,
                Title = ApiErrors.Conflict.Message,
                Type = Problems.TypeFor(ApiErrors.Conflict.Code),
                Extensions = { ["code"] = ApiErrors.Conflict.Code },
            },
        });
    }
}
