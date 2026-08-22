using IndexDesk.Modules.Analytics.Calculators;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace IndexDesk.Modules.Analytics;

public static class AnalyticsModuleExtensions
{
    public static IServiceCollection AddAnalyticsModule(this IServiceCollection services)
    {
        return services;
    }

    public static IEndpointRouteBuilder MapAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/analytics").WithTags("Analytics");

        group
            .MapPost(
                "/backtest",
                () =>
                    Results.Problem(
                        statusCode: StatusCodes.Status422UnprocessableEntity,
                        title: "Backtest unavailable",
                        detail:
                            "Backtests are unavailable until persisted quote and macroeconomic series are available for every requested asset and benchmark.",
                        extensions: new Dictionary<string, object?>
                        {
                            ["code"] = "Analytics.BacktestUnavailable",
                        }
                    )
            )
            .WithName("RunBacktest")
            .WithSummary(
                "Run a backtest from persisted market data; unavailable until the required series are ingested"
            );

        group
            .MapGet(
                "/real-yield",
                (decimal nominalRate, decimal inflationRate) =>
                {
                    try
                    {
                        var realYield = FinancialCalculators.CalculateRealYield(
                            nominalRate,
                            inflationRate
                        );
                        return Results.Ok(
                            new
                            {
                                nominalRate,
                                inflationRate,
                                realYieldPercent = realYield,
                            }
                        );
                    }
                    catch (ArgumentException ex)
                    {
                        return Results.BadRequest(new { message = ex.Message });
                    }
                }
            )
            .WithName("GetRealYield")
            .WithSummary("Calculate exact real yield using the Fisher Equation");

        return app;
    }
}

public sealed record AllocationItem(string Ticker, decimal WeightPercent);

public sealed record BacktestRequest(
    decimal InitialAmount,
    decimal MonthlyContribution,
    IReadOnlyList<AllocationItem> Allocations,
    string? Benchmark = "CDI",
    bool RebalanceAnnual = true
);

public sealed record EquityPoint(DateOnly Date, decimal Value);

public sealed record BacktestResponse(
    decimal InitialCapital,
    decimal FinalCapital,
    decimal TotalContributions,
    decimal TotalReturnPercent,
    decimal AnnualizedReturnPercent,
    decimal AnnualizedVolatilityPercent,
    decimal SharpeRatio,
    decimal MaxDrawdownPercent,
    IReadOnlyList<EquityPoint> EquityCurve
);
