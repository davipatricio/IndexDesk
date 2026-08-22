namespace IndexDesk.Modules.MarketData.Domain;

/// <summary>Compact row used by the asset catalog/screener list endpoint.</summary>
public sealed record AssetSummaryDto(
    string Ticker,
    string Name,
    string AssetType,
    string Currency,
    string? BenchmarkSymbol,
    decimal? LastPrice,
    decimal? ChangeDayPercent,
    decimal? Return1mPercent,
    decimal? Return12mPercent,
    decimal? AnnualizedVolatilityPercent,
    decimal? SharpeRatio,
    decimal? MaxDrawdownPercent,
    DateOnly? FirstQuoteDate,
    DateOnly? LastQuoteDate
);

/// <summary>Rich single-asset sheet including the fiscal "raio-x".</summary>
public sealed record AssetDetailDto(
    string Ticker,
    string Name,
    string AssetType,
    string? Cnpj,
    string? Isin,
    string Currency,
    string? TradingViewSymbol,
    AssetQuoteStatsDto Stats,
    FiscalProfileDto Fiscal
);

public sealed record AssetQuoteStatsDto(
    decimal? LastPrice,
    decimal? ChangeDayPercent,
    decimal? Return1mPercent,
    decimal? Return6mPercent,
    decimal? Return12mPercent,
    decimal? ReturnYtdPercent,
    decimal? AnnualizedVolatilityPercent,
    decimal? SharpeRatio,
    decimal? MaxDrawdownPercent,
    decimal? AvgVolume30D,
    DateOnly? FirstQuoteDate,
    DateOnly? LastQuoteDate
);

/// <summary>
/// Ranked asset row returned by GET /api/v1/assets/rankings. Rows arrive pre-sorted by the
/// requested metric; <see cref="MetricValue"/> mirrors the column used for the ordering.
/// </summary>
public sealed record AssetRankingDto(
    int Rank,
    string Ticker,
    string Name,
    string AssetType,
    string Currency,
    decimal? MetricValue,
    decimal? LastPrice,
    decimal? ChangeDayPercent,
    decimal? Return30dPercent,
    decimal? Return6mPercent,
    decimal? Return12mPercent,
    decimal? ReturnYtdPercent,
    decimal? AnnualizedVolatilityPercent,
    decimal? SharpeRatio,
    decimal? MaxDrawdownPercent,
    decimal? AvgVolume30D,
    DateOnly? FirstQuoteDate,
    DateOnly? LastQuoteDate
);

/// <summary>Tax breakdown aligned with the platform's fiscal-education goals.</summary>
public sealed record FiscalProfileDto(
    string TaxDomicile, // BRAZIL, USA, IRELAND_UCITS, OTHER
    bool IsEtf,
    bool IsBdr,
    bool IsFii,
    decimal IncomeTaxRatePercent, // Standard swing-tax IR
    decimal DayTradeTaxRatePercent,
    bool HasComeCotas,
    bool HasMonthlySalesTaxExemption,
    bool IsTaxWithheldAtSource, // DARF vs retenção na fonte
    decimal ForeignDividendWithholdingPercent, // US 30% vs IRL 15%
    string TaxSummary
);

/// <summary>Time series OHLCV quote returned by the quotes endpoint.</summary>
public sealed record QuoteSeriesDto(
    DateOnly Date,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal AdjustedClose,
    decimal Volume
);

/// <summary>Performance/return between two dates with optional benchmark comparison.</summary>
public sealed record PerformanceResponseDto(
    string Ticker,
    DateOnly From,
    DateOnly To,
    decimal StartPrice,
    decimal EndPrice,
    decimal PriceReturnPercent,
    decimal TotalReturnPercent,
    decimal AnnualizedReturnPercent,
    int DividendPayments,
    decimal DividendsTotal,
    IReadOnlyList<BenchmarkReturnDto> Benchmarks
);

public sealed record BenchmarkReturnDto(
    string Code,
    string Name,
    decimal ReturnPercent,
    bool Available
);

/// <summary>Macro indicator snapshot for the market strip (CDI/Selic/IPCA).</summary>
public sealed record MarketIndicatorDto(
    string Code,
    string Name,
    decimal LatestValue,
    DateOnly LatestDate,
    decimal? Accum12mPercent
);

/// <summary>Single closing point used to render inline sparklines.</summary>
public sealed record QuoteSparkPointDto(DateOnly Date, decimal Close);

/// <summary>Closing-price window for one ticker in a batch sparkline request.</summary>
public sealed record AssetQuotesBatchItemDto(
    string Ticker,
    IReadOnlyList<QuoteSparkPointDto> Quotes
);
