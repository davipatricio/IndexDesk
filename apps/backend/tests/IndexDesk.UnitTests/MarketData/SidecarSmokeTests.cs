using FluentAssertions;
using IndexDesk.Modules.MarketData.Clients;
using IndexDesk.UnitTests.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IndexDesk.UnitTests.MarketData;

/// <summary>
/// MANUAL SMOKE (network allowed). Never runs in CI or plain `dotnet test`:
/// opt in with SIDECAR_SMOKE=1. Exercises each sidecar client against its real
/// provider through the real uv-spawned Python process.
///
///   SIDECAR_SMOKE=1 dotnet test --filter "FullyQualifiedName~SidecarSmokeTests"
///
/// InfoMoney additionally needs INFOMONEY_SUBSCRIPTION_KEY in the environment
/// (public frontend key per tools/providers/recon/recon.md); without it that
/// smoke reports blocked-pending-key.
/// </summary>
public sealed class SmokeFactAttribute : FactAttribute
{
    public SmokeFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("SIDECAR_SMOKE") != "1")
        {
            Skip = "manual smoke: set SIDECAR_SMOKE=1 to run against live providers";
        }
    }
}

public sealed class SidecarSmokeTests
{
    private static SidecarProcessRunner NewConfiguredRunner() =>
        new(
            new ConfigurationBuilder().AddEnvironmentVariables("Providers_").Build(),
            NullLogger<SidecarProcessRunner>.Instance
        );

    [SmokeFact]
    public async Task Smoke_YahooFinance_ReturnsRowsForPetr4()
    {
        var client = new YfinanceSidecarClient(
            NewConfiguredRunner(),
            NullLogger<YfinanceSidecarClient>.Instance,
            ResilienceTestKit.NewResilience()
        );
        var end = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = end.AddDays(-30);

        var result = await client.GetHistoricalQuotesAsync("PETR4", start, end);

        result
            .IsSuccess.Should()
            .BeTrue($"expected Yahoo rows, got {result.Error.Code}: {result.Error.Message}");
        result.Value.Should().NotBeEmpty();
        Console.WriteLine(
            $"[smoke] YahooSidecar PETR4.SA rows={result.Value.Count} "
                + $"last={result.Value[^1].Date} close={result.Value[^1].Close}"
        );
    }

    [SmokeFact]
    public async Task Smoke_TradingView_ReturnsRowsForBova11()
    {
        var client = new TradingViewSidecarClient(
            NewConfiguredRunner(),
            new ConfigurationBuilder().AddEnvironmentVariables("Providers_").Build(),
            NullLogger<TradingViewSidecarClient>.Instance,
            ResilienceTestKit.NewResilience()
        );
        var end = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = end.AddDays(-60);

        var result = await client.GetHistoricalQuotesAsync("BOVA11", start, end);

        result
            .IsSuccess.Should()
            .BeTrue($"expected TV rows, got {result.Error.Code}: {result.Error.Message}");
        result.Value.Should().NotBeEmpty();
        Console.WriteLine(
            $"[smoke] TradingView BMFBOVESPA:BOVA11 rows={result.Value.Count} "
                + $"last={result.Value[^1].Date} close={result.Value[^1].Close}"
        );
    }

    [SmokeFact]
    public async Task Smoke_InfoMoney_ReturnsRowsForMglu3()
    {
        var key = Environment.GetEnvironmentVariable("INFOMONEY_SUBSCRIPTION_KEY");
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                "InfoMoney smoke blocked-pending-key: export INFOMONEY_SUBSCRIPTION_KEY "
                    + "(public frontend key, see tools/providers/recon/recon.md)"
            );
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?> { ["Providers:InfoMoney:SubscriptionKeys:0"] = key }
            )
            .Build();
        var client = new InfoMoneySidecarClient(
            NewConfiguredRunner(),
            configuration,
            NullLogger<InfoMoneySidecarClient>.Instance,
            ResilienceTestKit.NewPoolFromConfig(configuration),
            ResilienceTestKit.NewResilience()
        );
        var end = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = end.AddDays(-30);

        var result = await client.GetHistoricalQuotesAsync("MGLU3", start, end);

        result
            .IsSuccess.Should()
            .BeTrue($"expected InfoMoney rows, got {result.Error.Code}: {result.Error.Message}");
        result.Value.Should().NotBeEmpty();
        Console.WriteLine(
            $"[smoke] InfoMoney MGLU3 rows={result.Value.Count} "
                + $"last={result.Value[^1].Date} close={result.Value[^1].Close}"
        );
    }
}
