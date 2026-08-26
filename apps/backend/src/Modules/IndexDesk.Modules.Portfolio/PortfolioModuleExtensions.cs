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
        services.AddScoped<IPortfolioFixedIncomeService, PortfolioFixedIncomeService>();
        services.AddScoped<IPortfolioTaxService, PortfolioTaxService>();
        services.AddScoped<IPortfolioSharingService, PortfolioSharingService>();
        services.AddScoped<IPortfolioGoalsService, PortfolioGoalsService>();
        services.AddScoped<IPortfolioExportService, PortfolioExportService>();
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

        // ---------- renda fixa (parâmetros + timeline) ----------

        var fiGroup = group.MapGroup("/{id:guid}/fixed-income");

        fiGroup
            .MapPost(
                "/",
                async (
                    Guid id,
                    AttachFixedIncomeRequest request,
                    IPortfolioFixedIncomeService service,
                    HttpContext http,
                    CancellationToken ct
                ) =>
                {
                    if (!TryGetUserId(http, out var userId))
                        return Results.Unauthorized();
                    var result = await service.AttachAsync(userId, id, request, ct);
                    return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
                }
            )
            .RequireAuthorization("PERMISSION:portfolio:write")
            .WithName("AttachFixedIncomeParams")
            .WithSummary(
                "Anexa/atualiza parâmetros de renda fixa (indexador, taxa, vencimento) a uma posição"
            );

        fiGroup
            .MapGet(
                "/",
                async (
                    Guid id,
                    IPortfolioFixedIncomeService service,
                    HttpContext http,
                    CancellationToken ct
                ) =>
                {
                    if (!TryGetUserId(http, out var userId))
                        return Results.Unauthorized();
                    var result = await service.ListAsync(userId, id, ct);
                    return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
                }
            )
            .RequireAuthorization("PERMISSION:portfolio:read")
            .WithName("ListFixedIncomeParams")
            .WithSummary("Lista os parâmetros de renda fixa da carteira");

        fiGroup
            .MapDelete(
                "/{fixedIncomeId:guid}",
                async (
                    Guid id,
                    Guid fixedIncomeId,
                    IPortfolioFixedIncomeService service,
                    HttpContext http,
                    CancellationToken ct
                ) =>
                {
                    if (!TryGetUserId(http, out var userId))
                        return Results.Unauthorized();
                    var result = await service.DetachAsync(userId, id, fixedIncomeId, ct);
                    return result.IsSuccess ? Results.NoContent() : MapError(result.Error);
                }
            )
            .RequireAuthorization("PERMISSION:portfolio:write")
            .WithName("DetachFixedIncomeParams")
            .WithSummary("Remove parâmetros de renda fixa da posição");

        group
            .MapGet(
                "/{id:guid}/timeline",
                async (
                    Guid id,
                    IPortfolioFixedIncomeService service,
                    HttpContext http,
                    CancellationToken ct
                ) =>
                {
                    if (!TryGetUserId(http, out var userId))
                        return Results.Unauthorized();
                    var result = await service.GetTimelineAsync(userId, id, ct);
                    return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
                }
            )
            .RequireAuthorization("PERMISSION:portfolio:read")
            .WithName("GetPortfolioTimeline")
            .WithSummary("Timeline de vencimentos e carências da carteira");

        // ---------- fiscal (M-P4, educacional) ----------

        var taxGroup = group.MapGroup("/{id:guid}/tax");

        taxGroup
            .MapPost(
                "/redemption-simulation",
                async (
                    Guid id,
                    SimulateRedemptionRequest request,
                    IPortfolioTaxService service,
                    HttpContext http,
                    CancellationToken ct
                ) =>
                {
                    if (!TryGetUserId(http, out var userId))
                        return Results.Unauthorized();
                    var result = await service.SimulateRedemptionAsync(
                        userId,
                        id,
                        request.AssetId,
                        request.Broker,
                        request.Quantity,
                        ct
                    );
                    return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
                }
            )
            .RequireAuthorization("PERMISSION:portfolio:read")
            .WithName("SimulateRedemption")
            .WithSummary(
                "Simula resgate/venda com IR regressivo ou swing 15%, IOF, come-cotas e isenções PF"
            );

        taxGroup
            .MapGet(
                "/darf/{year:int}/{month:int}",
                async (
                    Guid id,
                    int year,
                    int month,
                    IPortfolioTaxService service,
                    HttpContext http,
                    CancellationToken ct
                ) =>
                {
                    if (!TryGetUserId(http, out var userId))
                        return Results.Unauthorized();
                    var result = await service.GetProjectionAsync(userId, id, year, month, ct);
                    return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
                }
            )
            .RequireAuthorization("PERMISSION:portfolio:read")
            .WithName("GetDarfProjection")
            .WithSummary(
                "Projeção mensal de DARF (código 6015 p/ renda variável) por classe com isenções PF"
            );

        // ---------- metas (multi-metas) ----------

        var goalsGroup = group.MapGroup("/{id:guid}/goals");

        goalsGroup
            .MapPost(
                "/",
                async (
                    Guid id,
                    CreateGoalRequest request,
                    IPortfolioGoalsService service,
                    HttpContext http,
                    CancellationToken ct
                ) =>
                {
                    if (!TryGetUserId(http, out var userId))
                        return Results.Unauthorized();
                    var result = await service.CreateAsync(userId, id, request, ct);
                    return result.IsSuccess
                        ? Results.Created(
                            $"/api/v1/portfolios/{id}/goals/{result.Value!.Id}",
                            result.Value
                        )
                        : MapError(result.Error);
                }
            )
            .RequireAuthorization("PERMISSION:portfolio:write")
            .WithName("CreateGoal")
            .WithSummary("Cria uma meta (TARGET_AMOUNT | TARGET_RETURN_PCT | TARGET_DATE)");

        goalsGroup
            .MapGet(
                "/",
                async (
                    Guid id,
                    IPortfolioGoalsService service,
                    HttpContext http,
                    decimal currentValue,
                    decimal? currentReturnPct,
                    CancellationToken ct
                ) =>
                {
                    if (!TryGetUserId(http, out var userId))
                        return Results.Unauthorized();
                    var result = await service.ListAsync(
                        userId,
                        id,
                        currentValue,
                        currentReturnPct,
                        ct
                    );
                    return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
                }
            )
            .RequireAuthorization("PERMISSION:portfolio:read")
            .WithName("ListGoals")
            .WithSummary("Lista as metas com progresso % contra o valor atual informado");

        goalsGroup
            .MapPut(
                "/{goalId:guid}",
                async (
                    Guid id,
                    Guid goalId,
                    UpdateGoalRequest request,
                    IPortfolioGoalsService service,
                    HttpContext http,
                    CancellationToken ct
                ) =>
                {
                    if (!TryGetUserId(http, out var userId))
                        return Results.Unauthorized();
                    var result = await service.UpdateAsync(userId, id, goalId, request, ct);
                    return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
                }
            )
            .RequireAuthorization("PERMISSION:portfolio:write")
            .WithName("UpdateGoal")
            .WithSummary("Atualiza uma meta (campos parciais)");

        goalsGroup
            .MapDelete(
                "/{goalId:guid}",
                async (
                    Guid id,
                    Guid goalId,
                    IPortfolioGoalsService service,
                    HttpContext http,
                    CancellationToken ct
                ) =>
                {
                    if (!TryGetUserId(http, out var userId))
                        return Results.Unauthorized();
                    var result = await service.DeleteAsync(userId, id, goalId, ct);
                    return result.IsSuccess ? Results.NoContent() : MapError(result.Error);
                }
            )
            .RequireAuthorization("PERMISSION:portfolio:write")
            .WithName("DeleteGoal")
            .WithSummary("Remove uma meta");

        // ---------- compartilhamento (dono) ----------

        group
            .MapPatch(
                "/{id:guid}/visibility",
                async (
                    Guid id,
                    UpdateVisibilityRequest request,
                    IPortfolioSharingService service,
                    HttpContext http,
                    CancellationToken ct
                ) =>
                {
                    if (!TryGetUserId(http, out var userId))
                        return Results.Unauthorized();
                    var result = await service.UpdateVisibilityAsync(
                        userId,
                        id,
                        request.Visibility,
                        request.ExpiresInDays,
                        ct
                    );
                    return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
                }
            )
            .RequireAuthorization("PERMISSION:portfolio:write")
            .WithName("UpdateVisibility")
            .WithSummary("Altera visibilidade (private | public | link) e expiração do link");

        group
            .MapPost(
                "/{id:guid}/share-link/regenerate",
                async (
                    Guid id,
                    RegenerateShareLinkRequest request,
                    IPortfolioSharingService service,
                    HttpContext http,
                    CancellationToken ct
                ) =>
                {
                    if (!TryGetUserId(http, out var userId))
                        return Results.Unauthorized();
                    var result = await service.RegenerateShareLinkAsync(
                        userId,
                        id,
                        request.ExpiresInDays,
                        ct
                    );
                    return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
                }
            )
            .RequireAuthorization("PERMISSION:portfolio:write")
            .WithName("RegenerateShareLink")
            .WithSummary("Gera novo link restrito — token claro retornado UMA única vez");

        group
            .MapDelete(
                "/{id:guid}/share-link",
                async (
                    Guid id,
                    IPortfolioSharingService service,
                    HttpContext http,
                    CancellationToken ct
                ) =>
                {
                    if (!TryGetUserId(http, out var userId))
                        return Results.Unauthorized();
                    var result = await service.RevokeShareLinkAsync(userId, id, ct);
                    return result.IsSuccess ? Results.NoContent() : MapError(result.Error);
                }
            )
            .RequireAuthorization("PERMISSION:portfolio:write")
            .WithName("RevokeShareLink")
            .WithSummary("Revoga o link restrito (limpa token/expiração)");

        // ---------- export CSV (XLSX pendente de pacote — documentado no plano M-P5) ----------

        group
            .MapGet(
                "/{id:guid}/export/positions.csv",
                async (
                    Guid id,
                    IPortfolioExportService service,
                    HttpContext http,
                    CancellationToken ct
                ) =>
                {
                    if (!TryGetUserId(http, out var userId))
                        return Results.Unauthorized();
                    var result = await service.PositionsCsvAsync(userId, id, ct);
                    return result.IsSuccess
                        ? Results.File(
                            result.Value.Content,
                            "text/csv; charset=utf-8",
                            result.Value.FileName
                        )
                        : MapError(result.Error);
                }
            )
            .RequireAuthorization("PERMISSION:portfolio:read")
            .WithName("ExportPositionsCsv")
            .WithSummary("Exporta posições em CSV (pt-BR)");

        group
            .MapGet(
                "/{id:guid}/export/transactions.csv",
                async (
                    Guid id,
                    IPortfolioExportService service,
                    HttpContext http,
                    CancellationToken ct
                ) =>
                {
                    if (!TryGetUserId(http, out var userId))
                        return Results.Unauthorized();
                    var result = await service.TransactionsCsvAsync(userId, id, ct);
                    return result.IsSuccess
                        ? Results.File(
                            result.Value.Content,
                            "text/csv; charset=utf-8",
                            result.Value.FileName
                        )
                        : MapError(result.Error);
                }
            )
            .RequireAuthorization("PERMISSION:portfolio:read")
            .WithName("ExportTransactionsCsv")
            .WithSummary("Exporta transações em CSV (pt-BR)");

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

        // ---------- público (página /c/[slug] + clone por visitante logado) ----------

        app.MapGet(
                "/api/v1/portfolios/public/{slug}",
                async (
                    string slug,
                    IPortfolioSharingService service,
                    HttpContext http,
                    string? shareToken,
                    CancellationToken ct
                ) =>
                {
                    // Preferir header: token não vaza em logs de proxy/histórico.
                    // Query mantida como fallback para compatibilidade.
                    var headerToken = http.Request.Headers["X-Share-Token"].FirstOrDefault();
                    var result = await service.GetPublicBySlugAsync(
                        slug,
                        headerToken ?? shareToken,
                        ct
                    );
                    return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error);
                }
            )
            .AllowAnonymous()
            .WithName("GetPublicPortfolio")
            .WithSummary("Carteira pública por slug (link restrito exige shareToken)");

        app.MapPost(
                "/api/v1/portfolios/public/{slug}/clone",
                async (
                    string slug,
                    IPortfolioSharingService service,
                    HttpContext http,
                    CancellationToken ct
                ) =>
                {
                    if (!TryGetUserId(http, out var userId))
                        return Results.Unauthorized();
                    var result = await service.ClonePublicAsync(slug, userId, ct);
                    return result.IsSuccess
                        ? Results.Created(
                            $"/api/v1/portfolios/{result.Value}",
                            new { id = result.Value }
                        )
                        : MapError(result.Error);
                }
            )
            .RequireAuthorization("PERMISSION:portfolio:write")
            .WithName("ClonePublicPortfolio")
            .WithSummary("Importa carteira pública com BUYs sintéticos preservando pesos");

        return app;
    }

    public sealed record UpdateVisibilityRequest(string Visibility, int? ExpiresInDays = null);

    public sealed record RegenerateShareLinkRequest(int ExpiresInDays);

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
            "Portfolio.NotFound"
            or "Transaction.NotFound"
            or "Asset.NotFound"
            or "Position.NotFound"
            or "Goal.NotFound"
            or "FixedIncome.NotFound" => Results.NotFound(
                new { code = error.Code, message = error.Message }
            ),
            "Portfolio.LimitReached" => Results.Conflict(
                new { code = error.Code, message = error.Message }
            ),
            _ => Results.UnprocessableEntity(new { code = error.Code, message = error.Message }),
        };
}
