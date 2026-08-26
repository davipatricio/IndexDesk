using System.Security.Claims;
using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.Modules.Portfolio.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IndexDesk.Modules.Portfolio;

public static class PortfolioModuleExtensions
{
    public static IServiceCollection AddPortfolioModule(this IServiceCollection services)
    {
        services.AddScoped<IPortfolioService, PortfolioService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<IPortfolioPerformanceService, PortfolioPerformanceService>();
        return services;
    }

    public static IEndpointRouteBuilder MapPortfolioEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/portfolios").WithTags("Portfolios");

        group
            .MapPost(
                "/",
                async (
                    CreatePortfolioRequest request,
                    IPortfolioService service,
                    HttpContext http,
                    CancellationToken ct
                ) =>
                {
                    if (!TryGetUserId(http, out var userId))
                        return Results.Unauthorized();

                    var result = await service.CreateAsync(userId, request, ct);
                    return result.IsSuccess
                        ? Results.Created($"/api/v1/portfolios/{result.Value!.Id}", result.Value)
                        : MapError(result.Error);
                }
            )
            .RequireAuthorization("PERMISSION:portfolio:write")
            .WithName("CreatePortfolio")
            .WithSummary("Cria uma carteira (limite de 3 por usuário)");

        group
            .MapGet(
                "/",
                async (IPortfolioService service, HttpContext http, CancellationToken ct) =>
                {
                    if (!TryGetUserId(http, out var userId))
                        return Results.Unauthorized();
                    var result = await service.ListAsync(userId, ct);
                    return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
                }
            )
            .RequireAuthorization("PERMISSION:portfolio:read")
            .WithName("ListPortfolios")
            .WithSummary("Lista as carteiras do usuário autenticado");

        group
            .MapGet(
                "/{id:guid}",
                async (
                    Guid id,
                    IPortfolioService service,
                    HttpContext http,
                    CancellationToken ct
                ) =>
                {
                    if (!TryGetUserId(http, out var userId))
                        return Results.Unauthorized();
                    var result = await service.GetSummaryAsync(userId, id, ct);
                    return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
                }
            )
            .RequireAuthorization("PERMISSION:portfolio:read")
            .WithName("GetPortfolioSummary")
            .WithSummary("Resumo da carteira com posições projetadas e valuation local");

        group
            .MapPatch(
                "/{id:guid}",
                async (
                    Guid id,
                    UpdatePortfolioRequest request,
                    IPortfolioService service,
                    HttpContext http,
                    CancellationToken ct
                ) =>
                {
                    if (!TryGetUserId(http, out var userId))
                        return Results.Unauthorized();
                    var result = await service.UpdateAsync(userId, id, request, ct);
                    return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
                }
            )
            .RequireAuthorization("PERMISSION:portfolio:write")
            .WithName("UpdatePortfolio")
            .WithSummary("Atualiza título/descrição/perfil/visibilidade da carteira");

        group
            .MapDelete(
                "/{id:guid}",
                async (
                    Guid id,
                    IPortfolioService service,
                    HttpContext http,
                    CancellationToken ct
                ) =>
                {
                    if (!TryGetUserId(http, out var userId))
                        return Results.Unauthorized();
                    var result = await service.DeleteAsync(userId, id, ct);
                    return result.IsSuccess ? Results.NoContent() : MapError(result.Error);
                }
            )
            .RequireAuthorization("PERMISSION:portfolio:write")
            .WithName("DeletePortfolio")
            .WithSummary("Exclui a carteira e suas transações");

        // ---------- transactions ----------

        var txGroup = group.MapGroup("/{id:guid}/transactions");

        txGroup
            .MapPost(
                "/",
                async (
                    Guid id,
                    CreateTransactionRequest request,
                    ITransactionService service,
                    HttpContext http,
                    CancellationToken ct
                ) =>
                {
                    if (!TryGetUserId(http, out var userId))
                        return Results.Unauthorized();
                    var result = await service.CreateAsync(userId, id, request, ct);
                    return result.IsSuccess
                        ? Results.Created(
                            $"/api/v1/portfolios/{id}/transactions/{result.Value!.Id}",
                            result.Value
                        )
                        : MapError(result.Error);
                }
            )
            .RequireAuthorization("PERMISSION:portfolio:write")
            .WithName("CreateTransaction")
            .WithSummary(
                "Lança uma transação (BUY/SELL/INCOME/CORP_ACTION/TRANSFER), retroativa ilimitada"
            );

        txGroup
            .MapGet(
                "/",
                async (
                    Guid id,
                    ITransactionService service,
                    HttpContext http,
                    int page = 1,
                    int pageSize = 50,
                    CancellationToken ct = default
                ) =>
                {
                    if (!TryGetUserId(http, out var userId))
                        return Results.Unauthorized();
                    var result = await service.ListAsync(userId, id, page, pageSize, ct);
                    return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
                }
            )
            .RequireAuthorization("PERMISSION:portfolio:read")
            .WithName("ListTransactions")
            .WithSummary("Lista transações paginadas (mais recentes primeiro)");

        txGroup
            .MapPut(
                "/{transactionId:guid}",
                async (
                    Guid id,
                    Guid transactionId,
                    CreateTransactionRequest request,
                    ITransactionService service,
                    HttpContext http,
                    CancellationToken ct
                ) =>
                {
                    if (!TryGetUserId(http, out var userId))
                        return Results.Unauthorized();
                    var result = await service.AmendAsync(userId, id, transactionId, request, ct);
                    return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
                }
            )
            .RequireAuthorization("PERMISSION:portfolio:write")
            .WithName("AmendTransaction")
            .WithSummary("Corrige uma transação por edição lógica (histórico preservado)");

        group
            .MapGet(
                "/{id:guid}/performance",
                async (
                    Guid id,
                    IPortfolioPerformanceService service,
                    HttpContext http,
                    DateOnly? from,
                    DateOnly? to,
                    string? benchmarks,
                    CancellationToken ct
                ) =>
                {
                    if (!TryGetUserId(http, out var userId))
                        return Results.Unauthorized();
                    var result = await service.GetAsync(userId, id, from, to, benchmarks, ct);
                    return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
                }
            )
            .RequireAuthorization("PERMISSION:portfolio:read")
            .WithName("GetPortfolioPerformance")
            .WithSummary(
                "Série diária do patrimônio com retorno simples, TWR, MWR e benchmarks (CDI/IPCA/IBOV) — dados locais"
            );

        group
            .MapGet(
                "/lookup/{ticker}",
                async (
                    string ticker,
                    IndexDesk.BuildingBlocks.Persistence.IndexDeskDbContext db,
                    HttpContext http,
                    CancellationToken ct
                ) =>
                {
                    if (!http.User.Identity?.IsAuthenticated ?? true)
                        return Results.Unauthorized();
                    var normalized = ticker.Trim().ToUpperInvariant();
                    var asset = await db.Assets.FirstOrDefaultAsync(
                        a => a.Ticker == normalized && a.IsActive,
                        ct
                    );
                    return asset is null
                        ? Results.NotFound(
                            new
                            {
                                code = "Asset.NotFound",
                                message = $"Ativo '{normalized}' não encontrado.",
                            }
                        )
                        : Results.Ok(
                            new AssetLookupDto(
                                asset.Id,
                                asset.Ticker,
                                asset.Name,
                                asset.AssetType,
                                asset.Currency
                            )
                        );
                }
            )
            .RequireAuthorization("PERMISSION:portfolio:read")
            .WithName("LookupAssetForPortfolio")
            .WithSummary("Busca um ativo do catálogo por ticker para lançar na carteira");

        return app;
    }

    private sealed record AssetLookupDto(
        Guid Id,
        string Ticker,
        string Name,
        string AssetType,
        string Currency
    );

    private static bool TryGetUserId(HttpContext http, out Guid userId)
    {
        var claim =
            http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.User.FindFirstValue("sub");
        userId = Guid.TryParse(claim, out var parsed) ? parsed : Guid.Empty;
        return userId != Guid.Empty;
    }

    private static IResult MapError(Error error) =>
        error.Code switch
        {
            "Portfolio.NotFound" or "Transaction.NotFound" or "Asset.NotFound" => Results.NotFound(
                new { code = error.Code, message = error.Message }
            ),
            "Portfolio.LimitReached" => Results.Conflict(
                new { code = error.Code, message = error.Message }
            ),
            _ => Results.UnprocessableEntity(new { code = error.Code, message = error.Message }),
        };
}
