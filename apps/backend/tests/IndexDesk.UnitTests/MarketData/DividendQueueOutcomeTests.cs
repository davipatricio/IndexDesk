using FluentAssertions;
using IndexDesk.Modules.MarketData.Ingestion;
using Xunit;

namespace IndexDesk.UnitTests.MarketData;

/// <summary>
/// <see cref="DividendQueueOutcome.Empty"/> is a sentinel used by
/// <c>DailyCloseSyncService</c> when the Brapi dividend queue is intentionally
/// skipped (catch-up runs for past dates — Brapi free only exposes today).
/// </summary>
public class DividendQueueOutcomeTests
{
    [Fact]
    public void Empty_HasNoFailureAndZeroProcessed()
    {
        var outcome = DividendQueueOutcome.Empty;
        outcome.Processed.Should().Be(0);
        outcome.FirstErrorCode.Should().BeNull();
        outcome.FirstErrorMessage.Should().BeNull();
        outcome.AnyHardFailure.Should().BeFalse();
        outcome.AnyFailure.Should().BeFalse();
    }

    [Fact]
    public void AnyFailure_IsTrueWhenErrorCodeIsSet()
    {
        var outcome = new DividendQueueOutcome(
            Processed: 3,
            FirstErrorCode: "Brapi.HttpError",
            FirstErrorMessage: "boom",
            AnyHardFailure: false
        );
        outcome.AnyFailure.Should().BeTrue();
    }

    [Fact]
    public void AnyFailure_IsTrueWhenHardFailureEvenWithoutErrorCode()
    {
        var outcome = new DividendQueueOutcome(
            Processed: 0,
            FirstErrorCode: null,
            FirstErrorMessage: null,
            AnyHardFailure: true
        );
        outcome.AnyFailure.Should().BeTrue();
    }
}
