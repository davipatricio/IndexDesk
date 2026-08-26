using IndexDesk.Modules.MarketData.Domain;
using Microsoft.Extensions.Logging;

namespace IndexDesk.Modules.MarketData.Pipeline;

public class MarketDataValidator
{
    private readonly ILogger<MarketDataValidator>? _logger;

    public MarketDataValidator(ILogger<MarketDataValidator>? logger = null)
    {
        _logger = logger;
    }

    public bool IsValidQuote(in NormalizedQuote quote, out string reason)
    {
        if (string.IsNullOrWhiteSpace(quote.Ticker))
        {
            reason = "Ticker is null or empty";
            return false;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)); // small margin for UTC vs local
        if (quote.Date > today)
        {
            reason = $"Quote date {quote.Date} is in the future";
            return false;
        }

        if (quote.Date < new DateOnly(1990, 1, 1))
        {
            reason = $"Quote date {quote.Date} is earlier than allowed minimum (1990-01-01)";
            return false;
        }

        if (
            quote.Open <= 0
            || quote.High <= 0
            || quote.Low <= 0
            || quote.Close <= 0
            || quote.AdjClose <= 0
        )
        {
            reason =
                $"One or more OHLC prices are <= 0 (Open: {quote.Open}, High: {quote.High}, Low: {quote.Low}, Close: {quote.Close}, AdjClose: {quote.AdjClose})";
            return false;
        }

        // Magnitude guard: asset_quotes stores prices as decimal(14,4) and no B3
        // asset legitimately trades at 8+ digits per share. Yahoo occasionally
        // emits glitch bars far beyond that; rejecting keeps the upsert alive.
        const decimal maxPrice = 9_999_999m;
        if (
            quote.Open >= maxPrice
            || quote.High >= maxPrice
            || quote.Low >= maxPrice
            || quote.Close >= maxPrice
            || quote.AdjClose >= maxPrice
        )
        {
            reason =
                $"One or more prices exceed the plausible maximum {maxPrice} (Open: {quote.Open}, High: {quote.High}, Low: {quote.Low}, Close: {quote.Close}, AdjClose: {quote.AdjClose})";
            return false;
        }

        // Allow tiny delta margin (0.0001) for numeric inaccuracies
        const decimal epsilon = 0.0001m;
        if (quote.High < quote.Low - epsilon)
        {
            reason = $"High ({quote.High}) is strictly less than Low ({quote.Low})";
            return false;
        }

        if (quote.Volume < 0)
        {
            reason = $"Volume is negative ({quote.Volume})";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public bool IsValidDividend(in NormalizedDividend dividend, out string reason)
    {
        if (string.IsNullOrWhiteSpace(dividend.Ticker))
        {
            reason = "Ticker is null or empty";
            return false;
        }

        if (dividend.Rate <= 0)
        {
            reason = $"Dividend rate is <= 0 ({dividend.Rate})";
            return false;
        }

        // Magnitude guard: asset_dividends stores rate as decimal(14,6) (max 8 integer
        // digits). Yahoo sometimes reports total distribution values instead of
        // per-share rates for old events (e.g. PDGR3 2008-2009 in the tens of millions),
        // which would overflow the column and poison the whole save batch.
        if (dividend.Rate >= 9_999_999m)
        {
            reason =
                $"Dividend rate {dividend.Rate} exceeds the plausible per-share maximum 9999999";
            return false;
        }

        if (dividend.ComDate < new DateOnly(1990, 1, 1))
        {
            reason = $"Com date {dividend.ComDate} is earlier than 1990-01-01";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public IReadOnlyList<NormalizedQuote> SanitizeQuotes(
        IEnumerable<NormalizedQuote> rawQuotes,
        string ticker
    )
    {
        var valid = new List<NormalizedQuote>();
        foreach (var quote in rawQuotes)
        {
            if (IsValidQuote(quote, out var reason))
            {
                valid.Add(quote);
            }
            else
            {
                _logger?.LogWarning(
                    "[Validator:QuoteRejected] Rejected invalid quote for {Ticker} on {Date}: {Reason}",
                    ticker,
                    quote.Date,
                    reason
                );
            }
        }

        // Deduplicate by Date, keeping latest fetched
        return valid.GroupBy(q => q.Date).Select(g => g.Last()).OrderBy(q => q.Date).ToList();
    }

    public IReadOnlyList<NormalizedDividend> SanitizeDividends(
        IEnumerable<NormalizedDividend> rawDividends,
        string ticker
    )
    {
        var valid = new List<NormalizedDividend>();
        foreach (var div in rawDividends)
        {
            if (IsValidDividend(div, out var reason))
            {
                valid.Add(div);
            }
            else
            {
                _logger?.LogWarning(
                    "[Validator:DividendRejected] Rejected invalid dividend for {Ticker} on {ComDate}: {Reason}",
                    ticker,
                    div.ComDate,
                    reason
                );
            }
        }

        return valid
            .GroupBy(d => new { d.ComDate, d.Rate })
            .Select(g => g.First())
            .OrderBy(d => d.ComDate)
            .ToList();
    }
}
