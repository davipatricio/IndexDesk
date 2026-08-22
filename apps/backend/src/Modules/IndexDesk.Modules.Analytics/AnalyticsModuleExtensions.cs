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
        services.AddScoped<IBacktestService, BacktestService>();
        return services;
    }

    public static IEndpointRouteBuilder MapAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/analytics").WithTags("Analytics");

        group
            .MapPost(
                "/backtest",
                async (BacktestRequest request, IBacktestService service, CancellationToken ct) =>
                {
                    try
                    {
                        var result = await service.RunAsync(request, ct);
                        if (result.IsSuccess)
                            return Results.Ok(result.Response);

                        var failure = result.Failure!;
                        return failure.Code switch
                        {
                            "Analytics.AssetNotFound" => Results.NotFound(
                                new { code = failure.Code, message = failure.Message }
                            ),
                            "Analytics.BacktestInvalid" => Results.BadRequest(
                                new { code = failure.Code, message = failure.Message }
                            ),
                            _ => Results.UnprocessableEntity(
                                new { code = failure.Code, message = failure.Message }
                            ),
                        };
                    }
                    catch (ArgumentException ex)
                    {
                        return Results.UnprocessableEntity(
                            new { code = "Analytics.BacktestInvalid", message = ex.Message }
                        );
                    }
                    catch (InvalidOperationException ex)
                    {
                        return Results.UnprocessableEntity(
                            new { code = "Analytics.BacktestUnavailable", message = ex.Message }
                        );
                    }
                }
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
    string? Rebalance = "annual",
    DateOnly? From = null,
    DateOnly? To = null
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
    IReadOnlyList<EquityPoint> EquityCurve,
    IReadOnlyList<EquityPoint>? BenchmarkCurve = null,
    decimal? BenchmarkFinalCapital = null,
    decimal? BenchmarkTotalReturnPercent = null,
    decimal? BenchmarkAnnualizedReturnPercent = null
);
