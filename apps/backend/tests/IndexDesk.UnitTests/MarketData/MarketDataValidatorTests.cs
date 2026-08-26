using FluentAssertions;
using IndexDesk.Modules.MarketData.Domain;
using IndexDesk.Modules.MarketData.Pipeline;
using Xunit;

namespace IndexDesk.UnitTests.MarketData;

public class MarketDataValidatorTests
{
    private readonly MarketDataValidator _validator = new();

    [Fact]
    public void IsValidQuote_WithValidData_ReturnsTrue()
    {
        // Arrange
        var quote = new NormalizedQuote(
            Ticker: "MXRF11",
            Date: new DateOnly(2024, 2, 15),
            Open: 10.50m,
            High: 10.60m,
            Low: 10.45m,
            Close: 10.55m,
            AdjClose: 10.55m,
            Volume: 1500000m,
            TradesCount: 1200,
            SourceProvider: "Brapi",
            FetchedAtUtc: DateTimeOffset.UtcNow
        );

        // Act
        var isValid = _validator.IsValidQuote(quote, out var reason);

        // Assert
        isValid.Should().BeTrue();
        reason.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, 10.6, 10.4, 10.5)]
    [InlineData(10.5, -1, 10.4, 10.5)]
    [InlineData(10.5, 10.6, 0, 10.5)]
    [InlineData(10.5, 10.6, 10.4, -5)]
    public void IsValidQuote_WithZeroOrNegativePrice_ReturnsFalse(
        decimal open,
        decimal high,
        decimal low,
        decimal close
    )
    {
        // Arrange
        var quote = new NormalizedQuote(
            Ticker: "VWRA11",
            Date: new DateOnly(2024, 2, 15),
            Open: open,
            High: high,
            Low: low,
            Close: close,
            AdjClose: close,
            Volume: 1000m,
            TradesCount: null,
            SourceProvider: "Brapi",
            FetchedAtUtc: DateTimeOffset.UtcNow
        );

        // Act
        var isValid = _validator.IsValidQuote(quote, out var reason);

        // Assert
        isValid.Should().BeFalse();
        reason.Should().Contain("<= 0");
    }

    [Fact]
    public void IsValidQuote_WhenHighIsLessThanLow_ReturnsFalse()
    {
        // Arrange
        var quote = new NormalizedQuote(
            Ticker: "GOLD11",
            Date: new DateOnly(2024, 2, 15),
            Open: 11.50m,
            High: 10.00m, // High < Low
            Low: 11.00m,
            Close: 10.50m,
            AdjClose: 10.50m,
            Volume: 50000m,
            TradesCount: null,
            SourceProvider: "YahooFinance",
            FetchedAtUtc: DateTimeOffset.UtcNow
        );

        // Act
        var isValid = _validator.IsValidQuote(quote, out var reason);

        // Assert
        isValid.Should().BeFalse();
        reason.Should().Contain("less than Low");
    }

    [Fact]
    public void SanitizeQuotes_FiltersCorruptedAndDeduplicates()
    {
        // Arrange
        var date1 = new DateOnly(2024, 2, 15);
        var date2 = new DateOnly(2024, 2, 16);

        var quotes = new List<NormalizedQuote>
        {
            new(
                "MXRF11",
                date1,
                10.5m,
                10.6m,
                10.4m,
                10.55m,
                10.55m,
                1000,
                null,
                "Brapi",
                DateTimeOffset.UtcNow
            ),
            new(
                "MXRF11",
                date1,
                10.5m,
                10.6m,
                10.4m,
                10.58m,
                10.58m,
                1500,
                null,
                "Brapi",
                DateTimeOffset.UtcNow
            ), // Duplicate date
            new(
                "MXRF11",
                date2,
                -1m,
                10.6m,
                10.4m,
                10.55m,
                10.55m,
                1000,
                null,
                "Brapi",
                DateTimeOffset.UtcNow
            ), // Invalid open price
        };

        // Act
        var sanitized = _validator.SanitizeQuotes(quotes, "MXRF11");

        // Assert
        sanitized.Should().HaveCount(1);
        sanitized[0].Date.Should().Be(date1);
        sanitized[0].Close.Should().Be(10.58m);
    }

    [Fact]
    public void IsValidQuote_WhenPriceExceedsPlausibleMaximum_ReturnsFalse()
    {
        // Yahoo glitch bar far beyond decimal(14,4) storage intent
        var quote = new NormalizedQuote(
            Ticker: "PDGR3",
            Date: new DateOnly(2024, 2, 15),
            Open: 10.50m,
            High: 99_999_999m,
            Low: 10.45m,
            Close: 10.55m,
            AdjClose: 10.55m,
            Volume: 1500000m,
            TradesCount: null,
            SourceProvider: "YahooSidecar",
            FetchedAtUtc: DateTimeOffset.UtcNow
        );

        var isValid = _validator.IsValidQuote(quote, out var reason);

        isValid.Should().BeFalse();
        reason.Should().Contain("plausible maximum");
    }

    [Fact]
    public void SanitizeDividends_RejectsRatesAbovePerShareMaximum()
    {
        // Real Yahoo payload for PDGR3: total-distribution values in the tens of
        // millions overflow asset_dividends.rate decimal(14,6)
        var dividends = new List<NormalizedDividend>
        {
            new(
                "PDGR3",
                new DateOnly(2008, 4, 30),
                new DateOnly(2008, 4, 30),
                16_324_412m,
                "DIVIDEND",
                "BRL",
                "YahooSidecar"
            ),
            new(
                "PDGR3",
                new DateOnly(2026, 4, 30),
                new DateOnly(2026, 4, 30),
                1.05m,
                "DIVIDEND",
                "BRL",
                "YahooSidecar"
            ),
        };

        var sanitized = _validator.SanitizeDividends(dividends, "PDGR3");

        sanitized.Should().ContainSingle();
        sanitized[0].Rate.Should().Be(1.05m);
    }

    [Fact]
    public void DividendsUpsert_RoundsProviderFloatNoiseToColumnScale()
    {
        // Yahoo emits 2.0307214999…; the column stores numeric(14,6) = 2.030721.
        // Exact decimal equality would treat it as new and collide on
        // (AssetId, ComDate, Rate) once PostgreSQL rounds on insert (23505).
        const decimal providerNoise = 2.0307214999999999m;
        var rate = decimal.Round(providerNoise, 6, MidpointRounding.AwayFromZero);
        var existing = 2.030721m;

        rate.Should().Be(existing);
        (Math.Abs(existing - rate) < 0.0000005m).Should().BeTrue();
    }
}
