using FluentAssertions;
using IndexDesk.Modules.MarketData.Ingestion;
using Xunit;

namespace IndexDesk.UnitTests.MarketData;

public class BenchmarkCatalogTests
{
    [Fact]
    public void All_ContainsIbovAndIfix_AsIndexType()
    {
        BenchmarkCatalog.All.Should().ContainKeys("IBOV", "IFIX");
        BenchmarkCatalog.All["IBOV"].YahooSymbol.Should().Be("^BVSP");
        BenchmarkCatalog.All["IFIX"].YahooSymbol.Should().Be("IFIX.SA");
        // IFIX has no Yahoo history: ingestion is forward-only, starting near today.
        BenchmarkCatalog.All["IFIX"].HistoryStart.Should().BeOnOrAfter(new DateOnly(2026, 1, 1));
        BenchmarkCatalog.All["IBOV"].HistoryStart.Should().Be(new DateOnly(2015, 1, 1));
    }

    [Theory]
    [InlineData("IBOV", true)]
    [InlineData("ifix", true)]
    [InlineData("MXRF11", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsBenchmark_MatchesOnlyCuratedTickers(string? ticker, bool expected)
    {
        BenchmarkCatalog.IsBenchmark(ticker!).Should().Be(expected);
    }
}
