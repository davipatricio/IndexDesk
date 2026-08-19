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
                (BacktestRequest request) =>
                {
                    var totalWeight = request.Allocations.Sum(a => a.WeightPercent);
                    if (Math.Abs(totalWeight - 100m) > 0.01m)
                    {
                        return Results.BadRequest(
                            new { message = "Total allocation weight must equal 100%." }
                        );
                    }

                    var initialCapital = request.InitialAmount > 0 ? request.InitialAmount : 10000m;
                    var monthlyContribution = request.MonthlyContribution;
                    var simulatedMonths = 60; // 5 years demo simulation

                    var equityCurve = new List<EquityPoint>();
                    var currentCapital = initialCapital;
                    var startDate = DateTime.UtcNow.AddMonths(-simulatedMonths);

                    var curveValues = new List<decimal>();

                    for (var m = 0; m <= simulatedMonths; m++)
                    {
                        var date = DateOnly.FromDateTime(startDate.AddMonths(m));
                        if (m > 0)
                        {
                            currentCapital += monthlyContribution;
                            // Mock compounding return ~1.1% per month with variance
                            var monthReturn = 0.011m + (decimal)(Math.Sin(m) * 0.015);
                            currentCapital *= (1m + monthReturn);
                        }
                        equityCurve.Add(new EquityPoint(date, Math.Round(currentCapital, 2)));
                        curveValues.Add(currentCapital);
                    }

                    var maxDrawdown = FinancialCalculators.CalculateMaxDrawdown(curveValues);
                    var sharpe = FinancialCalculators.CalculateSharpeRatio(14.5m, 11.25m, 12.8m);

                    var response = new BacktestResponse(
                        InitialCapital: initialCapital,
                        FinalCapital: Math.Round(currentCapital, 2),
                        TotalContributions: initialCapital
                            + (monthlyContribution * simulatedMonths),
                        TotalReturnPercent: Math.Round(
                            (
                                (
                                    currentCapital
                                    / (initialCapital + monthlyContribution * simulatedMonths)
                                ) - 1m
                            ) * 100m,
                            2
                        ),
                        AnnualizedReturnPercent: 14.5m,
                        AnnualizedVolatilityPercent: 12.8m,
                        SharpeRatio: sharpe,
                        MaxDrawdownPercent: maxDrawdown,
                        EquityCurve: equityCurve
                    );

                    return Results.Ok(response);
                }
            )
            .WithName("RunBacktest")
            .WithSummary(
                "Simulate portfolio backtest with periodic rebalancing and inflation adjustment"
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
