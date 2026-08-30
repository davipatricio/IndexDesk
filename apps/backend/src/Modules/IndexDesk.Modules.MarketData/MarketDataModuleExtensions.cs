using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.BuildingBlocks.Persistence;
using IndexDesk.BuildingBlocks.Persistence.Services;
using IndexDesk.Modules.MarketData.Clients;
using IndexDesk.Modules.MarketData.Ingestion;
using IndexDesk.Modules.MarketData.Ingestion.Holdings;
using IndexDesk.Modules.MarketData.Pipeline;
using IndexDesk.Modules.MarketData.Resilience;
using IndexDesk.Modules.MarketData.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace IndexDesk.Modules.MarketData;

public static class MarketDataModuleExtensions
{
    private static readonly IConfiguration EmptyConfiguration = new ConfigurationBuilder().Build();

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
        // 0. Resilience (Fase 4): per-provider retry + circuit breaker and the API-key
        //    pool (rotation, cooldown, token bucket). Both are singletons — the breaker
        //    state and key health must outlive the transient/scoped clients that use them.
        //    Key VALUES come only from configuration (.env); nothing is seeded here.
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ProviderResilience>(sp => new ProviderResilience(
            configuration ?? EmptyConfiguration,
            sp.GetRequiredService<ILoggerFactory>().CreateLogger<ProviderResilience>()
        ));
        services.AddSingleton<IApiKeyPool>(sp => new InMemoryApiKeyPool(
            configuration ?? EmptyConfiguration,
            sp.GetRequiredService<ILoggerFactory>().CreateLogger<InMemoryApiKeyPool>(),
            sp.GetRequiredService<TimeProvider>()
        ));

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

        // FX (AwesomeAPI) — native HttpClient, no sidecar. Optional premium token is
        // appended as a query parameter by the client and never logged.
        services.AddHttpClient<AwesomeApiClient>(
            (sp, client) =>
            {
                var baseUrl =
                    configuration?["Providers:AwesomeApi:BaseUrl"]
                    ?? "https://economia.awesomeapi.com.br/";
                client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/");
                client.Timeout = TimeSpan.FromSeconds(15);
            }
        );

        // Holdings feeds (manager sites) — native HttpClient + module parsers.
        services.AddHttpClient<ISharesHoldingsFeed>(
            (sp, client) =>
            {
                var baseUrl =
                    configuration?["Providers:Ishares:BaseUrl"]
                    ?? "https://www.blackrock.com/br/intermediarios/en/products/";
                client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/");
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36"
                );
            }
        );

        services.AddHttpClient<SpdrHoldingsFeed>(
            (sp, client) =>
            {
                var baseUrl = configuration?["Providers:Spdr:BaseUrl"] ?? "https://www.ssga.com/";
                client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/");
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36"
                );
            }
        );

        services.AddHttpClient<ItNowHoldingsFeed>(
            (sp, client) =>
            {
                // Apex itnow.com.br NXDOMAINs — real host is www.itnow.com.br (recon 0.5).
                var baseUrl =
                    configuration?["Providers:ItNow:BaseUrl"] ?? "https://www.itnow.com.br/";
                client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/");
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36"
                );
            }
        );

        services.AddHttpClient<InvestoHoldingsFeed>(
            (sp, client) =>
            {
                var baseUrl =
                    configuration?["Providers:Investo:BaseUrl"] ?? "https://investoetf.com/";
                client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/");
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36"
                );
            }
        );

        // 3. Register as IMarketDataClient collection — the declarative OHLCV chain
        //    decided in Fase 3 (plans/provider-sync-scrapers.md): Brapi → Yahoo
        //    sidecar → TradingView sidecar. HG Brasil left the chain (paid source;
        //    concrete client kept but inactive). InfoMoney stays OUT on purpose:
        //    explicitly SECONDARY, reachable only via backfill --provider infomoney.
        services.AddScoped<IMarketDataClient>(sp => sp.GetRequiredService<BrapiClient>());
        services.AddScoped<IMarketDataClient>(sp => sp.GetRequiredService<YfinanceSidecarClient>());
        services.AddScoped<IMarketDataClient>(sp =>
            sp.GetRequiredService<TradingViewSidecarClient>()
        );

        // 3b. Sidecar-backed clients (Python sidecar under tools/providers/sidecar,
        // spawned through uv — see plans/provider-sync-scrapers.md Fase 2). Registered
        // as concrete types only on purpose: joining the IMarketDataClient fallback
        // chain above is a declarative-chain decision left for Fase 3 (jobs &
        // scheduling), keeping the current pipeline behavior untouched.
        services.AddSingleton<SidecarProcessRunner>();
        services.AddTransient<YfinanceSidecarClient>();
        services.AddTransient<TradingViewSidecarClient>();
        services.AddTransient<InfoMoneySidecarClient>();

        // 3b'. Generic WAF-safe HTTP over the sidecar `fetch` command (curl_cffi
        // impersonate=chrome). Feeds opt in per source via config — e.g.
        // Providers:Holdings:ItNow:Transport=sidecar|native (default sidecar) —
        // for hosts that Akamai-block non-browser TLS (measured: itnow.com.br).
        services.AddSingleton<ISidecarHttp>(sp => new SidecarHttp(
            sp.GetRequiredService<SidecarProcessRunner>()
        ));

        // 3c. Explicit-provider directory for backfill (--provider yahoo|tv|infomoney)
        services.AddScoped<SidecarProviderDirectory>();

        // 4. Fallback Engine & Ingestion Services
        services.AddScoped<IFallbackMarketDataService, FallbackMarketDataService>();
        services.AddScoped<IAssetBackfillService, AssetBackfillService>();
        services.AddScoped<IAssetSyncService, AssetSyncService>();
        services.AddScoped<IBcbSeriesClient>(sp => sp.GetRequiredService<BcbSeriesClient>());
        services.AddScoped<IMacroEconomicSyncService, MacroEconomicSyncService>();

        // 4b. Daily-close chain, FX and TradingView refresh (Fase 3 jobs' logic lives here)
        services.AddScoped<IDailyCloseSyncService, DailyCloseSyncService>();
        services.AddScoped<IAwesomeApiClient>(sp => sp.GetRequiredService<AwesomeApiClient>());
        services.AddScoped<IFxRateSyncService, FxRateSyncService>();
        services.AddScoped<ITradingViewRefreshSyncService, TradingViewRefreshSyncService>();
        services.AddScoped<IEtfHoldingsSyncService, EtfHoldingsSyncService>();

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
                async (IAssetSyncService syncService, IndexDeskDbContext db, CancellationToken ct) =>
                {
                    // Same advisory lock as the Quartz job / CLI / boot catch-up: an API
                    // trigger never races another writer into double provider hits.
                    var lockHandle = await db.TryAcquireAdvisoryLockAsync(
                        AdvisoryLockExtensions.BackfillSyncLockId,
                        ct
                    );
                    if (lockHandle is null)
                    {
                        return Results.Conflict(
                            Error.Failure(
                                "Sync.LockBusy",
                                "Another sync is already running (daily job, CLI backfill or boot catch-up)."
                            )
                        );
                    }

                    await using (lockHandle)
                    {
                        var result = await syncService.SyncDailyQuotesAsync(
                            cancellationToken: ct
                        );
                        return result.IsSuccess
                            ? Results.Ok(result.Value)
                            : Results.BadRequest(result.Error);
                    }
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
                    string? provider,
                    IAssetBackfillService backfillService,
                    IndexDeskDbContext db,
                    CancellationToken ct
                ) =>
                {
                    var lockHandle = await db.TryAcquireAdvisoryLockAsync(
                        AdvisoryLockExtensions.BackfillSyncLockId,
                        ct
                    );
                    if (lockHandle is null)
                    {
                        return Results.Conflict(
                            Error.Failure(
                                "Sync.LockBusy",
                                "Another sync is already running (daily job, CLI backfill or boot catch-up)."
                            )
                        );
                    }

                    await using (lockHandle)
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
                                ct,
                                preferredProvider: provider
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
                "/quotes/batch",
                async (
                    string? tickers,
                    int? days,
                    IAssetQueryService queryService,
                    CancellationToken ct
                ) =>
                {
                    var requested = (tickers ?? string.Empty).Split(
                        ',',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                    );
                    var items = await queryService.GetQuotesBatchAsync(requested, days, ct);
                    return Results.Ok(items);
                }
            )
            .WithName("GetAssetQuotesBatch")
            .WithSummary(
                "Closing-price windows for several tickers at once, for inline sparklines"
            );

        group
            .MapGet(
                "/macro-series",
                async (
                    string? codes,
                    int? days,
                    IAssetQueryService queryService,
                    CancellationToken ct
                ) =>
                {
                    var requested = (codes ?? string.Empty).Split(
                        ',',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                    );
                    var series = await queryService.GetMacroRateSeriesAsync(requested, days, ct);
                    return series.Count == 0
                        ? Results.NotFound(
                            new
                            {
                                code = "MarketData.MacroSeriesUnavailable",
                                message = "No macro-economic series available for the request.",
                            }
                        )
                        : Results.Ok(series);
                }
            )
            .WithName("GetMacroRateSeries")
            .WithSummary(
                "Raw CDI/Selic/IPCA rate windows used to build accumulated benchmark curves"
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
                "/{ticker}/dividends",
                async (string ticker, IAssetQueryService queryService, CancellationToken ct) =>
                {
                    var dividends = await queryService.GetDividendsAsync(ticker, ct);
                    return dividends is null
                        ? Results.NotFound(
                            new
                            {
                                code = "MarketData.DividendsUnavailable",
                                message = $"No dividend records found for '{ticker}'.",
                            }
                        )
                        : Results.Ok(dividends);
                }
            )
            .WithName("GetAssetDividends")
            .WithSummary(
                "Full local dividend history with trailing-12-months totals and yield per quote"
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
