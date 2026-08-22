using IndexDesk.BuildingBlocks.Cache;
using IndexDesk.BuildingBlocks.Common.Pagination;
using IndexDesk.BuildingBlocks.Persistence;
using IndexDesk.BuildingBlocks.Persistence.Entities;
using IndexDesk.Modules.MarketData.Calculators;
using IndexDesk.Modules.MarketData.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IndexDesk.Modules.MarketData.Services;

public class AssetQueryService : IAssetQueryService
{
    private readonly IndexDeskDbContext _dbContext;
    private readonly ICacheService _cache;
    private readonly ILogger<AssetQueryService> _logger;

    public AssetQueryService(
        IndexDeskDbContext dbContext,
        ICacheService cache,
        ILogger<AssetQueryService> logger
    )
    {
        _dbContext = dbContext;
        _cache = cache;
        _logger = logger;
    }

    public async Task<PagedResult<AssetSummaryDto>> ListAsync(
        string? search,
        string? assetType,
        string? currency,
        string? orderBy,
        string? orderDirection,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default
    )
    {
        var cacheKey =
            $"marketdata:list:{search ?? "*"}:{assetType ?? "*"}:{currency ?? "*"}:{orderBy ?? "ticker"}:{orderDirection ?? "asc"}:{page}:{pageSize}";

        var result = await _cache.GetOrCreateAsync(
            cacheKey,
            async ct =>
            {
                var query = _dbContext.Assets.Where(a => a.IsActive);

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var term = search.Trim();
                    query = query.Where(a =>
                        a.Ticker.Contains(term) || a.Name.Contains(term) || a.Cnpj!.Contains(term)
                    );
                }

                if (!string.IsNullOrWhiteSpace(assetType))
                {
                    var type = assetType.Trim();
                    query = query.Where(a => a.AssetType.ToUpper() == type.ToUpper());
                }

                if (!string.IsNullOrWhiteSpace(currency))
                {
                    var cur = currency.Trim().ToUpper();
                    query = query.Where(a => a.Currency.ToUpper() == cur);
                }

                var totalCount = await query.LongCountAsync(ct);
                var ordered = ApplyOrder(query, orderBy, orderDirection);
                var assets = await ordered
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(ct);

                var riskFreeAnnual = await GetRiskFreeAnnualPercentAsync(ct);
                var items = new List<AssetSummaryDto>(assets.Count);
                foreach (var asset in assets)
                {
                    var stats = await LoadStatsAsync(asset.Id, riskFreeAnnual, ct);
                    items.Add(ToSummary(asset, stats));
                }

                return new PagedResult<AssetSummaryDto>(items, page, pageSize, totalCount);
            },
            TimeSpan.FromMinutes(10),
            cancellationToken
        );

        return result;
    }

    public async Task<AssetDetailDto?> GetDetailAsync(
        string ticker,
        CancellationToken cancellationToken = default
    )
    {
        var asset = await FindAssetAsync(ticker, cancellationToken);
        if (asset is null)
            return null;

        var riskFreeAnnual = await GetRiskFreeAnnualPercentAsync(cancellationToken);
        var stats = await LoadStatsAsync(asset.Id, riskFreeAnnual, cancellationToken);
        return new AssetDetailDto(
            Ticker: asset.Ticker,
            Name: asset.Name,
            AssetType: asset.AssetType,
            Cnpj: asset.Cnpj,
            Isin: asset.Isin,
            Currency: asset.Currency,
            TradingViewSymbol: asset.TradingViewSymbol,
            Stats: stats,
            Fiscal: BuildFiscalProfile(asset.Ticker, asset.AssetType)
        );
    }

    public async Task<IReadOnlyList<QuoteSeriesDto>> GetQuotesAsync(
        string ticker,
        DateOnly? from,
        DateOnly? to,
        int? days,
        CancellationToken cancellationToken = default
    )
    {
        var asset = await FindAssetAsync(ticker, cancellationToken);
        if (asset is null)
            return Array.Empty<QuoteSeriesDto>();

        var end = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var start = from ?? (days.HasValue ? end.AddDays(-days.Value) : end.AddDays(-365)); // default 1 year of quotes
        if (start > end)
            (start, end) = (end, start);

        return await LoadQuotesAsync(asset.Id, start, end, cancellationToken);
    }

    public async Task<PerformanceResponseDto?> GetPerformanceAsync(
        string ticker,
        DateOnly from,
        DateOnly to,
        string returnType,
        bool includeBenchmarks,
        CancellationToken cancellationToken = default
    )
    {
        var asset = await FindAssetAsync(ticker, cancellationToken);
        if (asset is null)
            return null;

        if (from >= to)
            throw new ArgumentException("'from' must be earlier than 'to'.");

        var quotes = await LoadQuotesAsync(asset.Id, from, to, cancellationToken);
        if (quotes.Count < 2)
        {
            throw new InvalidOperationException(
                $"Insufficient quote data for {ticker} between {from} and {to}."
            );
        }

        var start = quotes[0];
        var end = quotes[^1];

        var dividendsInWindow = await _dbContext
            .AssetDividends.Where(d =>
                d.AssetId == asset.Id && d.ComDate >= from && d.PaymentDate <= to
            )
            .ToListAsync(cancellationToken);

        var dividendAmounts = dividendsInWindow.Select(d => d.Rate).ToList();
        var dividendsTotal = dividendAmounts.Sum();

        var priceReturn = PerformanceCalculators.ReturnBetween(start.Close, end.Close);
        var totalReturn = PerformanceCalculators.TotalReturnWithDividends(
            start.Close,
            end.Close,
            dividendAmounts
        );
        var calendarDays = to.DayNumber - from.DayNumber;
        var finalReturn = returnType.Equals("total", StringComparison.OrdinalIgnoreCase)
            ? totalReturn
            : priceReturn;
        var annualized = PerformanceCalculators.AnnualizedReturn(finalReturn, calendarDays);

        IReadOnlyList<BenchmarkReturnDto> benchmarks = Array.Empty<BenchmarkReturnDto>();
        if (includeBenchmarks)
        {
            benchmarks = await LoadBenchmarksAsync(from, to, cancellationToken);
        }

        return new PerformanceResponseDto(
            Ticker: asset.Ticker,
            From: from,
            To: to,
            StartPrice: start.Close,
            EndPrice: end.Close,
            PriceReturnPercent: Math.Round(priceReturn, 2),
            TotalReturnPercent: Math.Round(totalReturn, 2),
            AnnualizedReturnPercent: Math.Round(annualized, 2),
            DividendPayments: dividendAmounts.Count,
            DividendsTotal: dividendsTotal,
            Benchmarks: benchmarks
        );
    }

    // ---- internals ------------------------------------------------------

    private async Task<AssetEntity?> FindAssetAsync(
        string ticker,
        CancellationToken cancellationToken
    )
    {
        return await _dbContext.Assets.FirstOrDefaultAsync(
            a => a.Ticker == ticker.ToUpperInvariant(),
            cancellationToken
        );
    }

    private async Task<AssetQuoteStatsDto> LoadStatsAsync(
        Guid assetId,
        decimal riskFreeAnnualPercent,
        CancellationToken cancellationToken
    )
    {
        var quotes = await _dbContext
            .AssetQuotes.Where(q => q.AssetId == assetId)
            .OrderBy(q => q.Date)
            .ToListAsync(cancellationToken);

        if (quotes.Count == 0)
            return EmptyStats();

        var closes = quotes.Select(q => q.Close).ToList();
        var last = quotes[^1];
        var first = quotes[0];
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var prevClose = quotes.Count >= 2 ? quotes[^2].Close : last.Close;
        var dayChange = PerformanceCalculators.ReturnBetween(prevClose, last.Close);

        decimal? ReturnAtOrBefore(DateOnly target)
        {
            if (target <= first.Date)
                return null;
            var refQuote = quotes.LastOrDefault(q => q.Date <= target);
            return refQuote is null
                ? null
                : PerformanceCalculators.ReturnBetween(refQuote.Close, last.Close);
        }

        var oneMonth = today.AddDays(-30);
        var sixMonth = today.AddDays(-182);
        var twelveMonth = today.AddDays(-365);

        var dailyReturns = PerformanceCalculators.DailyReturns(closes);
        var volatility = PerformanceCalculators.AnnualizedVolatility(dailyReturns);

        decimal? sharpe = null;
        if (volatility > 0 && dailyReturns.Count > 0)
        {
            var meanDailyReturn = dailyReturns.Average(); // percent/day
            var annualizedReturn = meanDailyReturn * 252m; // percent/year
            sharpe = Math.Round((annualizedReturn - riskFreeAnnualPercent) / volatility, 2);
        }

        return new AssetQuoteStatsDto(
            LastPrice: last.Close,
            ChangeDayPercent: Math.Round(dayChange, 2),
            Return1mPercent: RoundNullable(ReturnAtOrBefore(oneMonth)),
            Return6mPercent: RoundNullable(ReturnAtOrBefore(sixMonth)),
            Return12mPercent: RoundNullable(ReturnAtOrBefore(twelveMonth)),
            ReturnYtdPercent: RoundNullable(ReturnAtOrBefore(new DateOnly(today.Year, 1, 1))),
            AnnualizedVolatilityPercent: volatility,
            SharpeRatio: sharpe,
            MaxDrawdownPercent: PerformanceCalculators.MaxDrawdown(closes),
            FirstQuoteDate: first.Date,
            LastQuoteDate: last.Date
        );
    }

    private async Task<decimal> GetRiskFreeAnnualPercentAsync(CancellationToken cancellationToken)
    {
        // Risk-free proxy = CDI (BCB series 12) accumulated over the trailing 252 business
        // days (~1y). Values are daily percent rates; AccumulateRateSeries compounds them.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = today.AddDays(-365);
        var rates = await _dbContext
            .MacroEconomicSeries.Where(s => s.SeriesCode == 12 && s.Date >= from)
            .OrderBy(s => s.Date)
            .Select(s => s.Value)
            .ToListAsync(cancellationToken);

        return rates.Count == 0 ? 0m : PerformanceCalculators.AccumulateRateSeries(rates);
    }

    private async Task<IReadOnlyList<QuoteSeriesDto>> LoadQuotesAsync(
        Guid assetId,
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken
    )
    {
        var quotes = await _dbContext
            .AssetQuotes.Where(q => q.AssetId == assetId && q.Date >= start && q.Date <= end)
            .OrderBy(q => q.Date)
            .Select(q => new QuoteSeriesDto(
                q.Date,
                q.Open,
                q.High,
                q.Low,
                q.Close,
                q.AdjClose,
                q.Volume
            ))
            .ToListAsync(cancellationToken);

        return quotes;
    }

    private async Task<IReadOnlyList<BenchmarkReturnDto>> LoadBenchmarksAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken
    )
    {
        var result = new List<BenchmarkReturnDto>
        {
            await BenchmarkFromRateSeriesAsync("CDI", "CDI", from, to, cancellationToken),
            await BenchmarkFromRateSeriesAsync("IPCA", "IPCA", from, to, cancellationToken),
            await BenchmarkFromIndexAsync("^BVSP", "Ibovespa", from, to, cancellationToken),
            await BenchmarkFromIndexAsync("^GSPC", "S&P 500", from, to, cancellationToken),
        };

        return result;
    }

    private async Task<BenchmarkReturnDto> BenchmarkFromRateSeriesAsync(
        string code,
        string name,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken
    )
    {
        var seriesCode = code switch
        {
            "CDI" => 12,
            "IPCA" => 433,
            _ => 0,
        };

        var points = await _dbContext
            .MacroEconomicSeries.Where(s =>
                s.SeriesCode == seriesCode && s.Date >= from && s.Date <= to
            )
            .OrderBy(s => s.Date)
            .Select(s => s.Value)
            .ToListAsync(cancellationToken);

        if (points.Count == 0)
            return new BenchmarkReturnDto(code, name, 0m, Available: false);

        var ret = PerformanceCalculators.AccumulateRateSeries(points);
        return new BenchmarkReturnDto(code, name, Math.Round(ret, 2), Available: true);
    }

    private async Task<BenchmarkReturnDto> BenchmarkFromIndexAsync(
        string ticker,
        string name,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken
    )
    {
        var asset = await _dbContext.Assets.FirstOrDefaultAsync(
            a => a.Ticker == ticker,
            cancellationToken
        );
        if (asset is null)
            return new BenchmarkReturnDto(ticker, name, 0m, Available: false);

        var quotes = await LoadQuotesAsync(asset.Id, from, to, cancellationToken);
        if (quotes.Count < 2)
            return new BenchmarkReturnDto(ticker, name, 0m, Available: false);

        var ret = PerformanceCalculators.ReturnBetween(quotes[0].Close, quotes[^1].Close);
        return new BenchmarkReturnDto(ticker, name, Math.Round(ret, 2), Available: true);
    }

    private static IQueryable<AssetEntity> ApplyOrder(
        IQueryable<AssetEntity> query,
        string? orderBy,
        string? orderDirection
    )
    {
        var desc = orderDirection?.Equals("desc", StringComparison.OrdinalIgnoreCase) ?? false;

        return (orderBy?.ToLowerInvariant(), desc) switch
        {
            ("name", false) => query.OrderBy(a => a.Name),
            ("name", true) => query.OrderByDescending(a => a.Name),
            ("type", false) => query.OrderBy(a => a.AssetType).ThenBy(a => a.Ticker),
            ("type", true) => query.OrderByDescending(a => a.AssetType).ThenBy(a => a.Ticker),
            ("created", false) => query.OrderBy(a => a.CreatedAt),
            ("created", true) => query.OrderByDescending(a => a.CreatedAt),
            (_, true) => query.OrderByDescending(a => a.Ticker),
            _ => query.OrderBy(a => a.Ticker),
        };
    }

    private static AssetSummaryDto ToSummary(AssetEntity asset, AssetQuoteStatsDto stats) =>
        new(
            Ticker: asset.Ticker,
            Name: asset.Name,
            AssetType: asset.AssetType,
            Currency: asset.Currency,
            BenchmarkSymbol: null,
            LastPrice: stats.LastPrice,
            ChangeDayPercent: stats.ChangeDayPercent,
            Return1mPercent: stats.Return1mPercent,
            Return12mPercent: stats.Return12mPercent,
            AnnualizedVolatilityPercent: stats.AnnualizedVolatilityPercent,
            SharpeRatio: stats.SharpeRatio,
            MaxDrawdownPercent: stats.MaxDrawdownPercent,
            FirstQuoteDate: stats.FirstQuoteDate,
            LastQuoteDate: stats.LastQuoteDate
        );

    private static AssetQuoteStatsDto EmptyStats() =>
        new(null, null, null, null, null, null, null, null, null, null, null);

    private static decimal? RoundNullable(decimal? value) =>
        value is null ? null : Math.Round(value.Value, 2);

    private static FiscalProfileDto BuildFiscalProfile(string ticker, string assetType)
    {
        var isFii = assetType.Equals("FII", StringComparison.OrdinalIgnoreCase);
        var isBdr = assetType.StartsWith("BDR", StringComparison.OrdinalIgnoreCase);
        var isEtf =
            assetType.Equals("ETF", StringComparison.OrdinalIgnoreCase)
            || assetType.Equals("BDR_ETF", StringComparison.OrdinalIgnoreCase);

        var isVwra = ticker.Equals("VWRA11", StringComparison.OrdinalIgnoreCase);
        var taxDomicile = isVwra ? "IRELAND_UCITS" : "BRAZIL";
        var foreignWithholding = isVwra ? 15m : 0m;

        var summary =
            isFii
                ? "FIIs são isentos de IR sobre rendimentos distribuídos (Lei 8.668/93); o ganho de capital na venda é tributado em até 20% (swing trade)."
            : isVwra
                ? "VWRA11 é um BDR de ETF UCITS acumulador domiciliado na Irlanda: ganho de capital de 15% no swing trade e 20% no day trade, recolhido via DARF 6015. Não há isenção de R$ 20 mil/mês nem come-cotas; dividendos são reinvestidos no fundo e a retenção norte-americana é de 15% no nível do veículo."
            : isBdr
                ? "BDRs de ETF no exterior: ganho de capital 15% (swing) e 20% (day trade). Não há isenção de R$ 20 mil/mês; retenção estrangeira sobre dividendos depende do domicílio."
            : "ETF de bolsa: ganho de capital 15% (swing) / 20% (day trade). Não há isenção de R$ 20 mil/mês para ETFs.";

        return new FiscalProfileDto(
            TaxDomicile: taxDomicile,
            IsEtf: isEtf,
            IsBdr: isBdr,
            IsFii: isFii,
            IncomeTaxRatePercent: 15m,
            DayTradeTaxRatePercent: 20m,
            HasComeCotas: false,
            HasMonthlySalesTaxExemption: false,
            IsTaxWithheldAtSource: false,
            ForeignDividendWithholdingPercent: foreignWithholding,
            TaxSummary: summary
        );
    }
}
