using IndexDesk.BuildingBlocks.Persistence;
using IndexDesk.BuildingBlocks.Persistence.Entities;
using IndexDesk.Modules.Analytics.Calculators;
using Microsoft.EntityFrameworkCore;

namespace IndexDesk.Modules.Analytics;

public interface IBacktestService
{
    Task<BacktestResult> RunAsync(BacktestRequest request, CancellationToken cancellationToken);
}

public sealed record BacktestResult(BacktestResponse? Response, BacktestFailure? Failure)
{
    public bool IsSuccess => Response is not null;

    public static BacktestResult Success(BacktestResponse response) => new(response, null);

    public static BacktestResult Unavailable(string code, string message) =>
        new(null, new BacktestFailure(code, message));
}

public sealed record BacktestFailure(string Code, string Message);

internal sealed class BacktestService(IndexDeskDbContext dbContext) : IBacktestService
{
    private const decimal WeightTolerance = 0.01m;

    public async Task<BacktestResult> RunAsync(
        BacktestRequest request,
        CancellationToken cancellationToken
    )
    {
        var validation = Validate(request);
        if (validation is not null)
            return BacktestResult.Unavailable("Analytics.BacktestInvalid", validation);

        var allocations = request
            .Allocations.GroupBy(item => item.Ticker.Trim().ToUpperInvariant())
            .Select(group => new AllocationItem(group.Key, group.Sum(item => item.WeightPercent)))
            .ToArray();

        var assets = await dbContext
            .Assets.AsNoTracking()
            .Where(asset => allocations.Select(item => item.Ticker).Contains(asset.Ticker))
            .ToDictionaryAsync(
                asset => asset.Ticker,
                StringComparer.OrdinalIgnoreCase,
                cancellationToken
            );

        var missingTicker = allocations.FirstOrDefault(item => !assets.ContainsKey(item.Ticker));
        if (missingTicker is not null)
        {
            return BacktestResult.Unavailable(
                "Analytics.AssetNotFound",
                $"Asset '{missingTicker.Ticker}' was not found in the local catalog."
            );
        }

        var requestedFrom = request.From;
        var requestedTo = request.To;
        var quoteSets = new Dictionary<string, List<AssetQuoteEntity>>(
            StringComparer.OrdinalIgnoreCase
        );

        foreach (var allocation in allocations)
        {
            var asset = assets[allocation.Ticker];
            var query = dbContext
                .AssetQuotes.AsNoTracking()
                .Where(quote => quote.AssetId == asset.Id);

            if (requestedFrom.HasValue)
                query = query.Where(quote => quote.Date >= requestedFrom.Value);
            if (requestedTo.HasValue)
                query = query.Where(quote => quote.Date <= requestedTo.Value);

            var quotes = await query.OrderBy(quote => quote.Date).ToListAsync(cancellationToken);
            if (quotes.Count < 2)
            {
                return BacktestResult.Unavailable(
                    "Analytics.BacktestUnavailable",
                    $"Persisted quotes for '{allocation.Ticker}' are insufficient for this period."
                );
            }

            quoteSets[allocation.Ticker] = quotes;
        }

        var dates = quoteSets
            .Values.Select(quotes => quotes.Select(quote => quote.Date).ToHashSet())
            .Aggregate(
                (left, right) =>
                {
                    left.IntersectWith(right);
                    return left;
                }
            )
            .OrderBy(date => date)
            .ToArray();

        if (dates.Length < 2)
        {
            return BacktestResult.Unavailable(
                "Analytics.BacktestUnavailable",
                "The selected assets do not have at least two common persisted trading dates."
            );
        }

        if (requestedFrom.HasValue && dates[0] < requestedFrom.Value)
            dates = dates.Where(date => date >= requestedFrom.Value).ToArray();
        if (requestedTo.HasValue && dates[^1] > requestedTo.Value)
            dates = dates.Where(date => date <= requestedTo.Value).ToArray();
        if (dates.Length < 2)
        {
            return BacktestResult.Unavailable(
                "Analytics.BacktestUnavailable",
                "The selected period does not contain two common persisted trading dates."
            );
        }

        var isBenchmarkRequested = !string.Equals(
            request.Benchmark,
            "NONE",
            StringComparison.OrdinalIgnoreCase
        );
        int? benchmarkSeriesCode = request.Benchmark?.Trim().ToUpperInvariant() switch
        {
            "SELIC" => 11,
            "CDI" => 12,
            _ => isBenchmarkRequested ? 12 : null,
        };

        Dictionary<DateOnly, decimal>? macroRates = null;
        decimal fallbackDailyMacroRate = 0m;

        if (benchmarkSeriesCode.HasValue)
        {
            var fromDate = dates[0];
            var toDate = dates[^1];
            var seriesData = await dbContext
                .MacroEconomicSeries.AsNoTracking()
                .Where(series =>
                    series.SeriesCode == benchmarkSeriesCode.Value
                    && series.Date >= fromDate
                    && series.Date <= toDate
                )
                .Select(series => new { series.Date, series.Value })
                .ToListAsync(cancellationToken);

            if (seriesData.Count == 0)
            {
                return BacktestResult.Unavailable(
                    "Analytics.BenchmarkUnavailable",
                    $"No persisted {request.Benchmark ?? "CDI"} series was found for the selected period."
                );
            }

            macroRates = seriesData.ToDictionary(s => s.Date, s => s.Value);
            fallbackDailyMacroRate = seriesData.Average(s => s.Value);
        }

        var quoteByTicker = quoteSets.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToDictionary(quote => quote.Date),
            StringComparer.OrdinalIgnoreCase
        );
        var targetWeights = allocations.ToDictionary(
            item => item.Ticker,
            item => item.WeightPercent / 100m,
            StringComparer.OrdinalIgnoreCase
        );
        var holdings = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var equityCurve = new List<EquityPoint>(dates.Length);
        var benchmarkCurve = benchmarkSeriesCode.HasValue
            ? new List<EquityPoint>(dates.Length)
            : null;
        var dailyReturns = new List<decimal>(dates.Length - 1);
        var totalContributions = request.InitialAmount;
        var benchmarkCapital = request.InitialAmount;
        var previousDate = dates[0];
        var initialQuotes = quoteByTicker.ToDictionary(
            pair => pair.Key,
            pair => GetAdjustedClose(pair.Value[dates[0]]),
            StringComparer.OrdinalIgnoreCase
        );

        foreach (var allocation in allocations)
            holdings[allocation.Ticker] =
                request.InitialAmount
                * targetWeights[allocation.Ticker]
                / initialQuotes[allocation.Ticker];

        equityCurve.Add(new EquityPoint(dates[0], request.InitialAmount));
        if (benchmarkCurve is not null)
            benchmarkCurve.Add(new EquityPoint(dates[0], decimal.Round(benchmarkCapital, 2)));

        for (var index = 1; index < dates.Length; index++)
        {
            var date = dates[index];
            foreach (var allocation in allocations)
            {
                var previousPrice = GetAdjustedClose(
                    quoteByTicker[allocation.Ticker][previousDate]
                );
                var currentPrice = GetAdjustedClose(quoteByTicker[allocation.Ticker][date]);
                holdings[allocation.Ticker] *= currentPrice / previousPrice;
            }

            var isNewMonth = date.Month != previousDate.Month || date.Year != previousDate.Year;
            if (isNewMonth && request.MonthlyContribution > 0m)
            {
                totalContributions += request.MonthlyContribution;
                foreach (var allocation in allocations)
                {
                    var price = GetAdjustedClose(quoteByTicker[allocation.Ticker][date]);
                    holdings[allocation.Ticker] +=
                        request.MonthlyContribution * targetWeights[allocation.Ticker] / price;
                }
            }

            var currentValue = holdings.Sum(pair =>
                pair.Value * GetAdjustedClose(quoteByTicker[pair.Key][date])
            );

            if (ShouldRebalance(request.Rebalance, date, previousDate))
            {
                foreach (var allocation in allocations)
                {
                    var price = GetAdjustedClose(quoteByTicker[allocation.Ticker][date]);
                    holdings[allocation.Ticker] =
                        currentValue * targetWeights[allocation.Ticker] / price;
                }
            }

            var contributionForReturn = isNewMonth ? request.MonthlyContribution : 0m;
            var previousValue = equityCurve[^1].Value + contributionForReturn;
            if (previousValue > 0m)
                dailyReturns.Add((currentValue / previousValue - 1m) * 100m);
            equityCurve.Add(new EquityPoint(date, decimal.Round(currentValue, 2)));

            if (benchmarkCurve is not null && macroRates is not null)
            {
                var dailyRate = macroRates.TryGetValue(date, out var rate)
                    ? rate
                    : fallbackDailyMacroRate;
                benchmarkCapital *= 1m + (dailyRate / 100m);
                if (isNewMonth && request.MonthlyContribution > 0m)
                {
                    benchmarkCapital += request.MonthlyContribution;
                }
                benchmarkCurve.Add(new EquityPoint(date, decimal.Round(benchmarkCapital, 2)));
            }

            previousDate = date;
        }

        var finalCapital = equityCurve[^1].Value;
        var totalReturnPercent =
            totalContributions > 0m ? (finalCapital / totalContributions - 1m) * 100m : 0m;
        var calendarDays = dates[^1].DayNumber - dates[0].DayNumber;
        var annualizedReturn = AnnualizedReturn(totalReturnPercent, calendarDays);
        var volatility = AnnualizedVolatility(dailyReturns);

        decimal? benchmarkFinalCapital = benchmarkCurve is not null
            ? benchmarkCurve[^1].Value
            : null;
        decimal? benchmarkTotalReturnPercent =
            benchmarkFinalCapital.HasValue && totalContributions > 0m
                ? (benchmarkFinalCapital.Value / totalContributions - 1m) * 100m
                : null;
        decimal? benchmarkAnnualizedReturnPercent = benchmarkTotalReturnPercent.HasValue
            ? AnnualizedReturn(benchmarkTotalReturnPercent.Value, calendarDays)
            : null;

        var riskFreeRate = benchmarkAnnualizedReturnPercent ?? 0m;
        var sharpe = FinancialCalculators.CalculateSharpeRatio(
            annualizedReturn,
            riskFreeRate,
            volatility
        );

        return BacktestResult.Success(
            new BacktestResponse(
                InitialCapital: decimal.Round(request.InitialAmount, 2),
                FinalCapital: decimal.Round(finalCapital, 2),
                TotalContributions: decimal.Round(totalContributions, 2),
                TotalReturnPercent: decimal.Round(totalReturnPercent, 2),
                AnnualizedReturnPercent: decimal.Round(annualizedReturn, 2),
                AnnualizedVolatilityPercent: decimal.Round(volatility, 2),
                SharpeRatio: sharpe,
                MaxDrawdownPercent: FinancialCalculators.CalculateMaxDrawdown(
                    equityCurve.Select(point => point.Value).ToArray()
                ),
                EquityCurve: equityCurve,
                BenchmarkCurve: benchmarkCurve,
                BenchmarkFinalCapital: benchmarkFinalCapital.HasValue
                    ? decimal.Round(benchmarkFinalCapital.Value, 2)
                    : null,
                BenchmarkTotalReturnPercent: benchmarkTotalReturnPercent.HasValue
                    ? decimal.Round(benchmarkTotalReturnPercent.Value, 2)
                    : null,
                BenchmarkAnnualizedReturnPercent: benchmarkAnnualizedReturnPercent.HasValue
                    ? decimal.Round(benchmarkAnnualizedReturnPercent.Value, 2)
                    : null
            )
        );
    }

    private static string? Validate(BacktestRequest request)
    {
        if (request.InitialAmount <= 0m)
            return "Initial amount must be greater than zero.";
        if (request.MonthlyContribution < 0m)
            return "Monthly contribution cannot be negative.";
        if (request.Allocations is null || request.Allocations.Count == 0)
            return "At least one allocation is required.";
        if (request.From.HasValue && request.To.HasValue && request.From.Value >= request.To.Value)
            return "The start date must be earlier than the end date.";
        if (request.Allocations.Any(item => item.WeightPercent <= 0m))
            return "Allocation weights must be greater than zero.";
        if (request.Allocations.Any(item => string.IsNullOrWhiteSpace(item.Ticker)))
            return "Allocation tickers cannot be empty.";

        var total = request.Allocations.Sum(item => item.WeightPercent);
        return Math.Abs(total - 100m) > WeightTolerance
            ? "Allocation weights must sum to 100%."
            : null;
    }

    private static decimal GetAdjustedClose(AssetQuoteEntity quote)
    {
        var price = quote.AdjClose > 0m ? quote.AdjClose : quote.Close;
        if (price <= 0m)
            throw new InvalidOperationException(
                "Persisted quote contains a non-positive adjusted close."
            );
        return price;
    }

    private static bool ShouldRebalance(string? rebalance, DateOnly date, DateOnly previousDate)
    {
        var normalized = (rebalance ?? "annual").Trim().ToLowerInvariant();
        if (normalized is "none" or "buy-and-hold" or "buy_and_hold")
            return false;

        var months = normalized switch
        {
            "monthly" => 1,
            "quarterly" => 3,
            "semiannual" or "semi-annual" => 6,
            "annual" or "yearly" => 12,
            _ => throw new ArgumentException("Unsupported rebalance frequency."),
        };

        var previousBucket = previousDate.Year * 12 + previousDate.Month;
        var currentBucket = date.Year * 12 + date.Month;
        return currentBucket / months > previousBucket / months;
    }

    private static decimal AnnualizedReturn(decimal totalReturnPercent, int calendarDays)
    {
        if (calendarDays <= 0)
            return 0m;

        var factor = 1m + totalReturnPercent / 100m;
        if (factor <= 0m)
            return -100m;

        var years = calendarDays / 365.25m;
        return (decimal)(Math.Pow((double)factor, 1d / (double)years) - 1d) * 100m;
    }

    private static decimal AnnualizedVolatility(List<decimal> dailyReturns)
    {
        if (dailyReturns.Count < 2)
            return 0m;

        var mean = dailyReturns.Average();
        var variance =
            dailyReturns.Sum(value => (value - mean) * (value - mean)) / (dailyReturns.Count - 1);
        return (decimal)Math.Sqrt((double)variance) * (decimal)Math.Sqrt(252d);
    }
}
