using FluentAssertions;
using IndexDesk.Modules.MarketData.Ingestion;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace IndexDesk.UnitTests.MarketData;

/// <summary>
/// Pure decision rules of the daily-close chain (Fase 4): pool-exhausted / circuit-open
/// failures degrade to PARTIAL_WARNING (the chain falls through) instead of FAILED.
/// </summary>
public class DailyCloseChainTests
{
    [Theory]
    [InlineData(null, 5, 5, "SUCCESS")]
    [InlineData(null, 3, 5, "PARTIAL_WARNING")] // incomplete coverage without errors
    [InlineData("Brapi.HttpError", 0, 5, "FAILED")] // hard error, nothing covered
    [InlineData("Brapi.HttpError", 2, 5, "PARTIAL_WARNING")] // hard error but partial data
    [InlineData("Provider.PoolExhausted", 0, 5, "PARTIAL_WARNING")] // soft → fallback ran
    [InlineData("Provider.CircuitOpen", 0, 0, "PARTIAL_WARNING")]
    public void StatusFor_ClassifiesSoftFailuresAsPartialWarning(
        string? errorCode,
        int touched,
        int expected,
        string expectedStatus
    ) =>
        DailyCloseChain
            .StatusFor(errorCode, anyHardFailure: false, touched: touched, expected: expected)
            .Should()
            .Be(expectedStatus);

    [Fact]
    public void StatusFor_AnyHardFailureWinsOverMixedSoftErrors() =>
        DailyCloseChain
            .StatusFor("Provider.PoolExhausted", anyHardFailure: true, touched: 0, expected: 5)
            .Should()
            .Be("FAILED");

    [Fact]
    public void Overall_FailedStageSinksJob_WarningsOnlyDegrade()
    {
        DailyCloseChain
            .Overall(["SUCCESS", "SUCCESS", "SUCCESS", "SUCCESS"])
            .Should()
            .Be("SUCCESS");
        DailyCloseChain
            .Overall(["PARTIAL_WARNING", "SUCCESS", "SUCCESS", "SUCCESS"])
            .Should()
            .Be("PARTIAL_WARNING");
        DailyCloseChain
            .Overall(["PARTIAL_WARNING", "SUCCESS", "FAILED", "SUCCESS"])
            .Should()
            .Be("FAILED");
    }

    [Fact]
    public void ResolveDividendSpacingMs_DefaultsToSevenSeconds()
    {
        DailyCloseChain
            .ResolveDividendSpacingMs(new ConfigurationBuilder().Build())
            .Should()
            .Be(7000);
    }

    [Theory]
    [InlineData("Providers:Brapi:DividendSpacingMs", "9000", 9000)]
    [InlineData("Providers:Brapi:DividendSpacingMs", "0", 0)]
    public void ResolveDividendSpacingMs_HonorsConfigOverride(
        string key,
        string value,
        int expected
    )
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [key] = value })
            .Build();

        DailyCloseChain.ResolveDividendSpacingMs(config).Should().Be(expected);
    }
}
