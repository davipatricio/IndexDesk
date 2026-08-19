using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.Modules.MarketData.Clients;
using IndexDesk.Modules.MarketData.Domain;
using Microsoft.Extensions.Logging;

namespace IndexDesk.Modules.MarketData.Pipeline;

public class FallbackMarketDataService : IFallbackMarketDataService
{
    private readonly IReadOnlyList<IMarketDataClient> _providers;
    private readonly MarketDataValidator _validator;
    private readonly ILogger<FallbackMarketDataService> _logger;

    public FallbackMarketDataService(
        IEnumerable<IMarketDataClient> providers,
        MarketDataValidator validator,
        ILogger<FallbackMarketDataService> logger
    )
    {
        _providers = providers.OrderBy(p => p.Priority).ToList();
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<NormalizedQuote>>> GetQuotesWithFallbackAsync(
        string ticker,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default
    )
    {
        var errors = new List<Error>();
        var eligibleProviders = _providers.Where(p => p.SupportsTicker(ticker)).ToList();

        if (eligibleProviders.Count == 0)
        {
            return Result<IReadOnlyList<NormalizedQuote>>.Failure(
                Error.NotFound(
                    "Provider.NoEligible",
                    $"No registered provider supports ticker {ticker}"
                )
            );
        }

        foreach (var provider in eligibleProviders)
        {
            try
            {
                _logger.LogInformation(
                    "[FallbackEngine:Attempt] Querying {Provider} (Priority {Priority}) for {Ticker} ({StartDate} to {EndDate})...",
                    provider.ProviderName,
                    provider.Priority,
                    ticker,
                    startDate,
                    endDate
                );

                var result = await provider.GetHistoricalQuotesAsync(
                    ticker,
                    startDate,
                    endDate,
                    cancellationToken
                );
                if (result.IsSuccess && result.Value.Count > 0)
                {
                    var sanitized = _validator.SanitizeQuotes(result.Value, ticker);
                    if (sanitized.Count > 0)
                    {
                        _logger.LogInformation(
                            "[FallbackEngine:Success] Successfully extracted and validated {Count} quotes for {Ticker} from {Provider}.",
                            sanitized.Count,
                            ticker,
                            provider.ProviderName
                        );

                        return Result<IReadOnlyList<NormalizedQuote>>.Success(sanitized);
                    }

                    _logger.LogWarning(
                        "[FallbackEngine:SanitizationFailed] All quotes returned by {Provider} for {Ticker} failed validation.",
                        provider.ProviderName,
                        ticker
                    );

                    errors.Add(
                        Error.Failure(
                            "Provider.ValidationFailed",
                            $"All quotes from {provider.ProviderName} failed validation"
                        )
                    );
                }
                else
                {
                    var errMsg = result.Error.Message ?? "No quotes returned";
                    _logger.LogWarning(
                        "[FallbackEngine:Degraded] Provider {Provider} failed for {Ticker}: {Error}. Falling back to next available provider...",
                        provider.ProviderName,
                        ticker,
                        errMsg
                    );

                    errors.Add(result.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[FallbackEngine:Exception] Unhandled exception from provider {Provider} for {Ticker}. Escalating fallback...",
                    provider.ProviderName,
                    ticker
                );

                errors.Add(
                    Error.Failure("Provider.Exception", $"{provider.ProviderName}: {ex.Message}")
                );
            }
        }

        var aggregatedMessage = string.Join("; ", errors.Select(e => $"[{e.Code}] {e.Message}"));
        _logger.LogError(
            "[FallbackEngine:Exhausted] All {Count} providers failed for {Ticker}. Errors: {Errors}",
            eligibleProviders.Count,
            ticker,
            aggregatedMessage
        );

        return Result<IReadOnlyList<NormalizedQuote>>.Failure(
            Error.Failure(
                "FallbackEngine.Exhausted",
                $"All providers failed for {ticker}: {aggregatedMessage}"
            )
        );
    }

    public async Task<Result<IReadOnlyList<NormalizedDividend>>> GetDividendsWithFallbackAsync(
        string ticker,
        CancellationToken cancellationToken = default
    )
    {
        var errors = new List<Error>();
        var eligibleProviders = _providers.Where(p => p.SupportsTicker(ticker)).ToList();

        foreach (var provider in eligibleProviders)
        {
            try
            {
                var result = await provider.GetDividendsAsync(ticker, cancellationToken);
                if (result.IsSuccess && result.Value.Count > 0)
                {
                    var sanitized = _validator.SanitizeDividends(result.Value, ticker);
                    return Result<IReadOnlyList<NormalizedDividend>>.Success(sanitized);
                }

                if (result.IsFailure)
                {
                    errors.Add(result.Error);
                }
            }
            catch (Exception ex)
            {
                errors.Add(
                    Error.Failure("Provider.Exception", $"{provider.ProviderName}: {ex.Message}")
                );
            }
        }

        return Result<IReadOnlyList<NormalizedDividend>>.Success(Array.Empty<NormalizedDividend>());
    }
}
