using IndexDesk.BuildingBlocks.Cache;
using IndexDesk.Modules.MarketData.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace IndexDesk.Modules.MarketData;

public static class MarketDataModuleExtensions
{
    public static IServiceCollection AddMarketDataModule(this IServiceCollection services)
    {
        // Register market data services
        return services;
    }

    public static IEndpointRouteBuilder MapMarketDataEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/assets").WithTags("MarketData");

        group
            .MapGet(
                "/",
                async (string? category, string? search, ICacheService cache) =>
                {
                    var cacheKey = $"marketdata:assets:{category ?? "all"}:{search ?? "all"}";
                    var assets = await cache.GetOrCreateAsync(
                        cacheKey,
                        async ct =>
                        {
                            await Task.Yield();
                            var list = new List<AssetDto>
                            {
                                new(
                                    "IVVB11",
                                    "iShares S&P 500 Fundo de Índice",
                                    "BlackRock",
                                    "ETF",
                                    "Equity",
                                    "S&P 500",
                                    0.23m,
                                    4500000000m,
                                    185000,
                                    342.50m,
                                    0.45m,
                                    18.2m
                                ),
                                new(
                                    "BOVA11",
                                    "iShares Ibovespa Fundo de Índice",
                                    "BlackRock",
                                    "ETF",
                                    "Equity",
                                    "IBOV",
                                    0.10m,
                                    12000000000m,
                                    120000,
                                    125.80m,
                                    -0.15m,
                                    6.4m
                                ),
                                new(
                                    "B5P211",
                                    "It Now IMA-B 5 P2 Fundo de Índice",
                                    "Itaú Asset",
                                    "ETF",
                                    "FixedIncome",
                                    "IMA-B 5 P2",
                                    0.20m,
                                    3200000000m,
                                    65000,
                                    89.20m,
                                    0.05m,
                                    8.9m
                                ),
                                new(
                                    "WRLD11",
                                    "Investo MSCI World Fundo de Índice",
                                    "Investo",
                                    "ETF",
                                    "Equity",
                                    "MSCI World",
                                    0.38m,
                                    1500000000m,
                                    42000,
                                    118.40m,
                                    0.62m,
                                    16.5m
                                ),
                                new(
                                    "SMAL11",
                                    "iShares Small Cap Fundo de Índice",
                                    "BlackRock",
                                    "ETF",
                                    "Equity",
                                    "SMLL",
                                    0.50m,
                                    2100000000m,
                                    58000,
                                    102.10m,
                                    -0.80m,
                                    -2.1m
                                ),
                                new(
                                    "HASH11",
                                    "Hashdex Nasdaq Crypto Index",
                                    "Hashdex",
                                    "ETF",
                                    "Crypto",
                                    "NCI",
                                    1.30m,
                                    2800000000m,
                                    140000,
                                    68.90m,
                                    2.10m,
                                    45.3m
                                ),
                            };

                            if (!string.IsNullOrWhiteSpace(category))
                            {
                                list = list.Where(a =>
                                        a.Category.Equals(
                                            category,
                                            StringComparison.OrdinalIgnoreCase
                                        )
                                    )
                                    .ToList();
                            }

                            if (!string.IsNullOrWhiteSpace(search))
                            {
                                list = list.Where(a =>
                                        a.Ticker.Contains(
                                            search,
                                            StringComparison.OrdinalIgnoreCase
                                        )
                                        || a.Name.Contains(
                                            search,
                                            StringComparison.OrdinalIgnoreCase
                                        )
                                    )
                                    .ToList();
                            }

                            return list;
                        },
                        TimeSpan.FromMinutes(15)
                    );

                    return Results.Ok(assets);
                }
            )
            .WithName("GetAssets")
            .WithSummary("List ETFs, BDRs and indices with metadata, AUM and performance");

        group
            .MapGet(
                "/{ticker}",
                (string ticker) =>
                {
                    ticker = ticker.ToUpperInvariant();
                    var asset = new AssetDto(
                        ticker,
                        $"{ticker} Fundo de Índice B3",
                        "Gestora Referência",
                        "ETF",
                        "Equity",
                        "IBOV",
                        0.20m,
                        3500000000m,
                        85000,
                        142.30m,
                        0.25m,
                        12.4m
                    );

                    return Results.Ok(asset);
                }
            )
            .WithName("GetAssetByTicker")
            .WithSummary("Get detailed ETF/BDR sheet by ticker");

        group
            .MapGet(
                "/{ticker}/quotes",
                (string ticker, int? days) =>
                {
                    var totalDays = days ?? 30;
                    var quotes = new List<QuoteItem>();
                    var basePrice = 100m;
                    var today = DateOnly.FromDateTime(DateTime.UtcNow);

                    for (var i = totalDays; i >= 0; i--)
                    {
                        var date = today.AddDays(-i);
                        var change = (decimal)(Math.Sin(i * 0.3) * 1.5);
                        var close = Math.Round(basePrice + change + (totalDays - i) * 0.2m, 2);
                        quotes.Add(
                            new QuoteItem(
                                date,
                                close - 0.5m,
                                close + 0.8m,
                                close - 0.6m,
                                close,
                                close,
                                1500000
                            )
                        );
                    }

                    return Results.Ok(quotes);
                }
            )
            .WithName("GetAssetQuotes")
            .WithSummary("Get historical daily OHLCV series for TradingView Lightweight Charts");

        return app;
    }
}
