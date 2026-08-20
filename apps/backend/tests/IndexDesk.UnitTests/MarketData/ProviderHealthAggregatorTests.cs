using FluentAssertions;
using IndexDesk.BuildingBlocks.Persistence.Entities;
using IndexDesk.Modules.MarketData.Domain;
using IndexDesk.Modules.MarketData.Health;
using Xunit;

namespace IndexDesk.UnitTests.MarketData;

public class ProviderHealthAggregatorTests
{
    private static SyncJobLogEntity Log(
        string provider,
        string status,
        DateTimeOffset startedAt,
        string? jobName = null,
        int processed = 0,
        int updated = 0,
        string? error = null
    ) =>
        new()
        {
            JobName = jobName ?? $"Job_{provider}",
            ProviderName = provider,
            Status = status,
            RecordsProcessed = processed,
            RecordsUpdated = updated,
            ErrorDetails = error,
            StartedAt = startedAt,
            CompletedAt = startedAt.AddSeconds(1),
            ExecutionTimeMs = 100,
        };

    [Fact]
    public void WithNoLogs_AllKnownProvidersAreNeverSynced_AndOverallIsUnknown()
    {
        var providers = ProviderHealthAggregator.BuildProviders(Array.Empty<SyncJobLogEntity>());

        providers.Should().HaveCountGreaterThan(0);
        providers.Should().OnlyContain(p => p.Status == "NEVER_SYNCED");
        providers.Should().Contain(p => p.Provider == "Brapi");
        providers.Should().Contain(p => p.Provider == "BCB");

        ProviderHealthAggregator.OverallStatus(providers).Should().Be("UNKNOWN");
    }

    [Fact]
    public void LatestSuccessMarksProviderHealthy_AndTotalsAreSummed()
    {
        var now = DateTimeOffset.UtcNow;
        var logs = new[]
        {
            Log("YahooFinance", "SUCCESS", now.AddHours(-1), processed: 100, updated: 100),
            Log("YahooFinance", "SUCCESS", now.AddHours(-2), processed: 50, updated: 50),
        };

        var providers = ProviderHealthAggregator.BuildProviders(logs);
        var yahoo = providers.Single(p => p.Provider == "YahooFinance");

        yahoo.Status.Should().Be("HEALTHY");
        yahoo.Totals.Jobs.Should().Be(2);
        yahoo.Totals.RecordsProcessed.Should().Be(150);
        yahoo.Totals.RecordsUpdated.Should().Be(150);
        yahoo.Totals.SuccessCount.Should().Be(2);
        yahoo.Totals.WarningCount.Should().Be(0);
        yahoo.Totals.ErrorCount.Should().Be(0);
        yahoo.LastSync.Should().NotBeNull();
        yahoo.LastSync!.Value.JobName.Should().Be("Job_YahooFinance");
        yahoo.Issues.Should().BeEmpty();
    }

    [Fact]
    public void LatestFailureMarksProviderUnhealthy_AndIssuesAreSurfaced()
    {
        var now = DateTimeOffset.UtcNow;
        var logs = new[]
        {
            Log("Brapi", "FAILED", now, error: "HTTP 401 Unauthorized"),
            Log("Brapi", "SUCCESS", now.AddHours(-1)),
        };

        var providers = ProviderHealthAggregator.BuildProviders(logs);
        var brapi = providers.Single(p => p.Provider == "Brapi");

        brapi.Status.Should().Be("UNHEALTHY");
        brapi.Totals.ErrorCount.Should().Be(1);
        brapi.Issues.Should().ContainSingle(i => i.Status == "FAILED");
        brapi.Issues.Single().Message.Should().Be("HTTP 401 Unauthorized");
    }

    [Fact]
    public void LatestPartialWarningMarksProviderDegraded()
    {
        var now = DateTimeOffset.UtcNow;
        var logs = new[] { Log("HGBrasil", "PARTIAL_WARNING", now) };

        var providers = ProviderHealthAggregator.BuildProviders(logs);
        providers.Single(p => p.Provider == "HGBrasil").Status.Should().Be("DEGRADED");
    }

    [Fact]
    public void OverallStatus_IsUnhealthyWhenAnyProviderIsDown()
    {
        var now = DateTimeOffset.UtcNow;
        var logs = new[]
        {
            Log("Brapi", "FAILED", now),
            Log("YahooFinance", "SUCCESS", now),
            Log("BCB", "SUCCESS", now),
        };

        var providers = ProviderHealthAggregator.BuildProviders(logs);
        ProviderHealthAggregator.OverallStatus(providers).Should().Be("UNHEALTHY");
    }

    [Fact]
    public void UnknownProviderInLogs_IsIncludedWithRoleLabel()
    {
        var now = DateTimeOffset.UtcNow;
        var logs = new[] { Log("ALL", "FAILED", now, error: "No provider responded") };

        var providers = ProviderHealthAggregator.BuildProviders(logs);
        var all = providers.Single(p => p.Provider == "ALL");

        all.Status.Should().Be("UNHEALTHY");
        all.DisplayName.Should().Contain("fallback");
        all.Issues.Should().ContainSingle();
    }
}
