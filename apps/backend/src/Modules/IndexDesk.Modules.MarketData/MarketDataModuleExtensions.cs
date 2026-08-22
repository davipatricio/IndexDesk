using IndexDesk.Modules.MarketData.Clients;
using IndexDesk.Modules.MarketData.Ingestion;
using IndexDesk.Modules.MarketData.Pipeline;
using IndexDesk.Modules.MarketData.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IndexDesk.Modules.MarketData;

public static class MarketDataModuleExtensions
{
    /// <summary>Metrics accepted by GET /api/v1/assets/rankings.</summary>
    private static readonly HashSet<string> RankingMetrics = new(StringComparer.OrdinalIgnoreCase)
    {
        "variacaodia",
        "retorno30d",
        "retorno6m",
        "retorno12m",
        "retornoano",
        "volatilidade",
        "sharpe",
        "drawdown",
        "volume",
    };

    public static IServiceCollection AddMarketDataModule(
        this IServiceCollection services,
        IConfiguration? configuration = null
    )
    {
        // 1. Validator
        services.AddSingleton<MarketDataValidator>();

        // 2. Typed Provider HttpClients
        services.AddHttpClient<BrapiClient>(
            (sp, client) =>
            {
                var baseUrl = configuration?["Providers:Brapi:BaseUrl"] ?? "https://brapi.dev/api/";
                client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/");
                client.Timeout = TimeSpan.FromSeconds(15);
            }
        );

        services.AddHttpClient<YahooFinanceClient>(
            (sp, client) =>
            {
                var baseUrl =
                    configuration?["Providers:Yahoo:BaseUrl"]
                    ?? "https://query1.finance.yahoo.com/";
                client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/");
                client.Timeout = TimeSpan.FromSeconds(15);
            }
        );

        services.AddHttpClient<HgBrasilClient>(
            (sp, client) =>
            {
                var baseUrl =
                    configuration?["Providers:HGBrasil:BaseUrl"] ?? "https://api.hgbrasil.com/";
                client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/");
                client.Timeout = TimeSpan.FromSeconds(15);
            }
        );

        services.AddHttpClient<BcbSeriesClient>(
            (sp, client) =>
            {
                var baseUrl = configuration?["Providers:BCB:BaseUrl"] ?? "https://api.bcb.gov.br/";
                client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/");
                client.Timeout = TimeSpan.FromSeconds(20);
            }
        );

        // 3. Register as IMarketDataClient collection
        services.AddScoped<IMarketDataClient>(sp => sp.GetRequiredService<BrapiClient>());
        services.AddScoped<IMarketDataClient>(sp => sp.GetRequiredService<YahooFinanceClient>());
        services.AddScoped<IMarketDataClient>(sp => sp.GetRequiredService<HgBrasilClient>());

        // 4. Fallback Engine & Ingestion Services
        services.AddScoped<IFallbackMarketDataService, FallbackMarketDataService>();
        services.AddScoped<IAssetBackfillService, AssetBackfillService>();
        services.AddScoped<IAssetSyncService, AssetSyncService>();
        services.AddScoped<IBcbSeriesClient>(sp => sp.GetRequiredService<BcbSeriesClient>());
        services.AddScoped<IMacroEconomicSyncService, MacroEconomicSyncService>();

        // 5. Query / Read APIs
        services.AddScoped<IAssetQueryService, AssetQueryService>();

        // 6. Provider health (sync_job_logs aggregation)
        services.AddScoped<IProviderHealthService, ProviderHealthService>();

        return services;
    }

    public static IEndpointRouteBuilder MapMarketDataEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/assets").WithTags("MarketData");

        // Sync and Backfill Trigger Endpoints
        group
            .MapPost(
                "/sync/daily",
                async (IAssetSyncService syncService, CancellationToken ct) =>
                {
                    var result = await syncService.SyncDailyQuotesAsync(cancellationToken: ct);
                    return result.IsSuccess
                        ? Results.Ok(result.Value)
                        : Results.BadRequest(result.Error);
                }
            )
            .WithName("TriggerDailySync")
            .WithSummary("Trigger incremental daily quote synchronization for pilot assets");

        group
            .MapPost(
                "/sync/backfill",
                async (
                    string? ticker,
                    string? startDate,
                    IAssetBackfillService backfillService,
                    CancellationToken ct
                ) =>
                {
                    if (!string.IsNullOrWhiteSpace(ticker))
                    {
                        var start = DateOnly.TryParse(startDate, out var parsedStart)
                            ? parsedStart
                            : new DateOnly(2021, 1, 1);
                        var end = DateOnly.FromDateTime(DateTime.UtcNow);

                        var singleResult = await backfillService.BackfillAssetAsync(
                            ticker,
                            start,
                            end,
                            ct
                        );
                        if (singleResult.IsSuccess)
                        {
                            return Results.Ok(singleResult.Value);
                        }

                        return singleResult.Error.Code == "Asset.Metadata.NotFound"
                            ? Results.NotFound(singleResult.Error)
                            : Results.BadRequest(singleResult.Error);
                    }

                    var result = await backfillService.BackfillPilotAssetsAsync(
                        cancellationToken: ct
                    );
                    return result.IsSuccess
                        ? Results.Ok(result.Value)
                        : Results.BadRequest(result.Error);
                }
            )
            .WithName("TriggerPilotBackfill")
            .WithSummary(
                "Trigger historical backfill for pilot assets or a specific ticker (e.g. WRLD11)"
            );

        // Macro-economic sync (CDI, Selic, IPCA) used as performance benchmarks
        group
            .MapPost(
                "/sync/macro",
                async (IMacroEconomicSyncService macroSync, CancellationToken ct) =>
                {
                    var result = await macroSync.SyncAllAsync(cancellationToken: ct);
                    return result.IsSuccess
                        ? Results.Ok(result.Value)
                        : Results.BadRequest(result.Error);
                }
            )
            .WithName("TriggerMacroSync")
            .WithSummary("Trigger BCB macro series sync (CDI, Selic, IPCA) for benchmarks");

        group
            .MapGet(
                "/",
                async (
                    string? search,
                    string? assetType,
                    string? currency,
                    string? orderBy,
                    string? orderDirection,
                    int? page,
                    int? pageSize,
                    IAssetQueryService queryService,
                    CancellationToken ct
                ) =>
                {
                    var p = Math.Max(1, page ?? 1);
                    var ps = Math.Clamp(pageSize ?? 20, 1, 100);

                    var result = await queryService.ListAsync(
                        search,
                        assetType,
                        currency,
                        orderBy,
                        orderDirection,
                        p,
                        ps,
                        ct
                    );
                    return Results.Ok(result);
                }
            )
            .WithName("GetAssets")
            .WithSummary(
                "Search, filter, sort and paginate the asset catalog with on-the-fly performance metrics"
            );

        group
            .MapGet(
                "/rankings",
                async (
                    string? assetType,
                    string? metric,
                    string? orderDirection,
                    int? page,
                    int? pageSize,
                    IAssetQueryService queryService,
                    CancellationToken ct
                ) =>
                {
                    var normalizedMetric = (metric ?? "retorno12m").Trim().ToLowerInvariant();
                    if (!RankingMetrics.Contains(normalizedMetric))
                    {
                        return Results.BadRequest(
                            new
                            {
                                code = "MarketData.InvalidRankingMetric",
                                message = $"Métrica '{metric}' inválida. Use uma de: {string.Join(", ", RankingMetrics.OrderBy(m => m))}.",
                            }
                        );
                    }

                    var p = Math.Max(1, page ?? 1);
                    var ps = Math.Clamp(pageSize ?? 20, 1, 100);

                    var result = await queryService.GetRankingsAsync(
                        assetType,
                        normalizedMetric,
                        orderDirection ?? "desc",
                        p,
                        ps,
                        ct
                    );
                    return Results.Ok(result);
                }
            )
            .WithName("GetAssetRankings")
            .WithSummary(
                "Rank active assets by a performance metric (sharpe, returns, volatility, drawdown, volume) with pagination"
            );

        group
            .MapGet(
                "/{ticker}/performance",
                async (
                    string ticker,
                    DateOnly? from,
                    DateOnly? to,
                    string? returnType,
                    bool? includeBenchmarks,
                    IAssetQueryService queryService,
                    CancellationToken ct
                ) =>
                {
                    var end = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
                    var start = from ?? end.AddYears(-1);

                    try
                    {
                        var result = await queryService.GetPerformanceAsync(
                            ticker,
                            start,
                            end,
                            returnType ?? "price",
                            includeBenchmarks ?? true,
                            ct
                        );
                        return result is null
                            ? Results.NotFound(
                                new
                                {
                                    code = "MarketData.AssetNotFound",
                                    message = $"Asset '{ticker}' not found.",
                                }
                            )
                            : Results.Ok(result);
                    }
                    catch (ArgumentException ex)
                    {
                        return Results.BadRequest(new { message = ex.Message });
                    }
                    catch (InvalidOperationException ex)
                    {
                        return Results.NotFound(
                            new { code = "MarketData.PerformanceUnavailable", message = ex.Message }
                        );
                    }
                }
            )
            .WithName("GetAssetPerformance")
            .WithSummary(
                "Return between two dates with price/total return and CDI, IPCA, IBOV, S&P 500 benchmark comparison"
            );

        group
            .MapGet(
                "/market-indicators",
                async (IAssetQueryService queryService, CancellationToken ct) =>
                {
                    var indicators = await queryService.GetMarketIndicatorsAsync(ct);
                    return indicators.Count == 0
                        ? Results.NotFound(
                            new
                            {
                                code = "MarketData.IndicatorsUnavailable",
                                message = "Nenhum indicador macro disponível ainda. Execute a sincronização de séries do BCB.",
                            }
                        )
                        : Results.Ok(indicators);
                }
            )
            .WithName("GetMarketIndicators")
            .WithSummary(
                "Latest CDI, Selic and IPCA values with trailing 12-month accumulation from local BCB series"
            );

        group
            .MapGet(
                "/{ticker}/quotes",
                async (
                    string ticker,
                    DateOnly? from,
                    DateOnly? to,
                    int? days,
                    IAssetQueryService queryService,
                    CancellationToken ct
                ) =>
                {
                    var quotes = await queryService.GetQuotesAsync(ticker, from, to, days, ct);
                    return quotes.Count == 0
                        ? Results.NotFound(
                            new
                            {
                                code = "MarketData.QuotesUnavailable",
                                message = $"No persisted quotes found for '{ticker}'.",
                            }
                        )
                        : Results.Ok(quotes);
                }
            )
            .WithName("GetAssetQuotes")
            .WithSummary(
                "Get historical daily OHLCV series for TradingView Lightweight Charts with optional date range"
            );

        group
            .MapGet(
                "/{ticker}",
                async (string ticker, IAssetQueryService queryService, CancellationToken ct) =>
                {
                    var asset = await queryService.GetDetailAsync(ticker, ct);
                    return asset is null
                        ? Results.NotFound(new { message = $"Asset '{ticker}' not found." })
                        : Results.Ok(asset);
                }
            )
            .WithName("GetAssetByTicker")
            .WithSummary(
                "Get detailed single-asset sheet including market stats and fiscal raio-x"
            );

        // Provider health — last sync, totals, warnings and errors aggregated from sync_job_logs
        var healthGroup = app.MapGroup("/api/v1/providers").WithTags("MarketData");

        healthGroup
            .MapGet(
                "/health",
                async (IProviderHealthService healthService, CancellationToken ct) =>
                {
                    var health = await healthService.GetProvidersHealthAsync(ct);
                    return Results.Ok(health);
                }
            )
            .WithName("GetProvidersHealth")
            .WithSummary(
                "Aggregate health of market-data providers: status, last sync, totals, warnings and errors"
            );

        return app;
    }
}
