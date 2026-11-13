using Wallet.Api.Http;
using Wallet.Application.Accounts;

namespace Wallet.Api.Endpoints;

internal static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        // Sem autenticação nesta fase (ver roadmap). Em produção, estas rotas exigiriam papel de administrador.
        var admin = app.MapGroup("/admin/accounts").WithTags("Administração");

        admin.MapPost("/{id:guid}/block", async (Guid id, BlockAccountHandler handler, CancellationToken ct) =>
            {
                var result = await handler.BlockAsync(id, ct);
                return result.IsSuccess ? TypedResults.Ok(result.Value) : Problems.For(result.Error!);
            })
            .WithSummary("Bloquear conta (não envia nem recebe)")
            .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapPost("/{id:guid}/unblock", async (Guid id, BlockAccountHandler handler, CancellationToken ct) =>
            {
                var result = await handler.UnblockAsync(id, ct);
                return result.IsSuccess ? TypedResults.Ok(result.Value) : Problems.For(result.Error!);
            })
            .WithSummary("Desbloquear conta")
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
