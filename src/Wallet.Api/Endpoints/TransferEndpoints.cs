using Microsoft.AspNetCore.Mvc;

using Wallet.Api.Http;
using Wallet.Application;
using Wallet.Application.Abstractions;
using Wallet.Application.Idempotency;
using Wallet.Application.Transfers;
using Wallet.Domain.Transfers;

namespace Wallet.Api.Endpoints;

/// <summary>Transferência. <c>amount</c> em reais, com até 2 casas decimais.</summary>
internal sealed record TransferRequest(Guid FromAccountId, Guid ToAccountId, decimal Amount);

internal static class TransferEndpoints
{
    public static void MapTransferEndpoints(this IEndpointRouteBuilder app)
    {
        var transfers = app.MapGroup("/transfers").WithTags("Transferências");

        transfers.MapPost("/", TransferAsync)
            .WithSummary("Transferir (exige Idempotency-Key)")
            .WithDescription(
                "201: concluída. 202: antifraude não respondeu a tempo, transferência em análise (nada foi debitado). " +
                "422: regra de negócio violada ou recusada pelo antifraude.")
            .Produces<TransferView>(StatusCodes.Status201Created)
            .Produces<TransferView>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        transfers.MapGet("/{id:guid}", GetAsync)
            .WithSummary("Consultar transferência")
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> TransferAsync(
        TransferRequest request,
        [FromHeader(Name = Idempotency.KeyHeader)] string? idempotencyKey,
        TransferHandler handler,
        HttpContext http,
        CancellationToken ct)
    {
        if (!IdempotencyKey.IsValid(idempotencyKey))
        {
            return Problems.For(ApiErrors.IdempotencyKeyRequired);
        }

        var key = IdempotencyKey.For(
            "transfer",
            request.FromAccountId,
            idempotencyKey!,
            new { to = request.ToAccountId, amount = Idempotency.Canonical(request.Amount) });

        var outcome = await handler.HandleAsync(
            new TransferCommand(request.FromAccountId, request.ToAccountId, request.Amount), key, ct);

        return Idempotency.Respond(http, outcome, transfer => transfer.Status switch
        {
            TransferStatus.Completed => TypedResults.Created($"/transfers/{transfer.Id}", transfer),
            TransferStatus.InReview => TypedResults.Accepted($"/transfers/{transfer.Id}", transfer),
            _ => Problems.For(AppErrors.TransferDenied, new Dictionary<string, object?> { ["transferId"] = transfer.Id }),
        });
    }

    private static async Task<IResult> GetAsync(Guid id, IWalletQueries queries, CancellationToken ct)
    {
        var transfer = await queries.GetTransferAsync(id, ct);
        return transfer is null ? Problems.For(ApiErrors.TransferNotFound) : TypedResults.Ok(transfer);
    }
}
