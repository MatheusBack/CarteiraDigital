using Microsoft.AspNetCore.Mvc;

using Wallet.Api.Http;
using Wallet.Application;
using Wallet.Application.Abstractions;
using Wallet.Application.Accounts;
using Wallet.Application.Idempotency;

namespace Wallet.Api.Endpoints;

internal sealed record OpenAccountRequest(string? HolderName, string? Document);

/// <summary>Depósito ou saque. <c>amount</c> em reais, com até 2 casas decimais (ex.: 150.75).</summary>
internal sealed record MovementRequest(decimal Amount);

internal static class AccountEndpoints
{
    private const int DefaultStatementLimit = 50;
    private const int MaxStatementLimit = 100;

    public static void MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var accounts = app.MapGroup("/accounts").WithTags("Contas");

        accounts.MapPost("/", OpenAsync)
            .WithSummary("Abrir conta")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        accounts.MapGet("/{id:guid}", GetAsync)
            .WithSummary("Consultar saldo e dados da conta")
            .ProducesProblem(StatusCodes.Status404NotFound);

        accounts.MapGet("/{id:guid}/statement", StatementAsync)
            .WithSummary("Extrato paginado (mais recente primeiro)")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        accounts.MapPost("/{id:guid}/deposits", DepositAsync)
            .WithSummary("Depositar (exige Idempotency-Key)")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        accounts.MapPost("/{id:guid}/withdrawals", WithdrawAsync)
            .WithSummary("Sacar (exige Idempotency-Key)")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private static async Task<IResult> OpenAsync(
        OpenAccountRequest request,
        OpenAccountHandler handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new OpenAccountCommand(request.HolderName, request.Document), ct);
        return result.IsSuccess
            ? TypedResults.Created($"/accounts/{result.Value.Id}", result.Value)
            : Problems.For(result.Error!);
    }

    private static async Task<IResult> GetAsync(Guid id, IWalletQueries queries, CancellationToken ct)
    {
        var account = await queries.GetAccountAsync(id, ct);
        return account is null ? Problems.For(AppErrors.AccountNotFound) : TypedResults.Ok(account);
    }

    private static async Task<IResult> StatementAsync(
        Guid id,
        string? cursor,
        int? limit,
        IWalletQueries queries,
        CancellationToken ct)
    {
        var pageSize = limit ?? DefaultStatementLimit;
        if (pageSize is < 1 or > MaxStatementLimit)
        {
            return Problems.For(ApiErrors.InvalidLimit);
        }

        StatementCursor? parsedCursor = null;
        if (cursor is not null)
        {
            if (!StatementCursor.TryParse(cursor, out var c))
            {
                return Problems.For(ApiErrors.InvalidCursor);
            }

            parsedCursor = c;
        }

        var page = await queries.GetStatementAsync(id, parsedCursor, pageSize, ct);
        return page is null ? Problems.For(AppErrors.AccountNotFound) : TypedResults.Ok(page);
    }

    private static async Task<IResult> DepositAsync(
        Guid id,
        MovementRequest request,
        [FromHeader(Name = Idempotency.KeyHeader)] string? idempotencyKey,
        DepositHandler handler,
        HttpContext http,
        CancellationToken ct)
    {
        if (!IdempotencyKey.IsValid(idempotencyKey))
        {
            return Problems.For(ApiErrors.IdempotencyKeyRequired);
        }

        var key = IdempotencyKey.For("deposit", id, idempotencyKey!, new { amount = Idempotency.Canonical(request.Amount) });
        var outcome = await handler.HandleAsync(new MovementCommand(id, request.Amount), key, ct);
        return Idempotency.Respond(http, outcome, TypedResults.Ok);
    }

    private static async Task<IResult> WithdrawAsync(
        Guid id,
        MovementRequest request,
        [FromHeader(Name = Idempotency.KeyHeader)] string? idempotencyKey,
        WithdrawHandler handler,
        HttpContext http,
        CancellationToken ct)
    {
        if (!IdempotencyKey.IsValid(idempotencyKey))
        {
            return Problems.For(ApiErrors.IdempotencyKeyRequired);
        }

        var key = IdempotencyKey.For("withdrawal", id, idempotencyKey!, new { amount = Idempotency.Canonical(request.Amount) });
        var outcome = await handler.HandleAsync(new MovementCommand(id, request.Amount), key, ct);
        return Idempotency.Respond(http, outcome, TypedResults.Ok);
    }
}
