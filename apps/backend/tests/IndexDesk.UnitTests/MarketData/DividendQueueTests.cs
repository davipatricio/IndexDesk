using FluentAssertions;
using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.Modules.MarketData.Ingestion;
using Xunit;

namespace IndexDesk.UnitTests.MarketData;

/// <summary>
/// Offline tests for the spaced Brapi dividend queue (budget rule): one delay after
/// EVERY ticker (including the last — preserved historical behavior) with the exact
/// configured spacing, plus the soft/hard failure classification feeding stage status.
/// </summary>
public class DividendQueueTests
{
    private static Result<int> Ok(int count = 1) => Result<int>.Success(count);

    private static Result<int> Fail(string code, string message = "boom") =>
        Result<int>.Failure(Error.Failure(code, message));

    [Fact]
    public async Task RunAsync_DelaysOnceAfterEachTicker_WithExactSpacing()
    {
        var processed = new List<string>();
        var delays = new List<int>();

        var outcome = await DividendQueue.RunAsync(
            ["BOVA11", "PIBB11", "IVVB11"],
            (ticker, _) =>
            {
                processed.Add(ticker);
                return Task.FromResult(Ok());
            },
            ms =>
            {
                delays.Add(ms);
                return Task.CompletedTask;
            },
            spacingMs: 7000,
            CancellationToken.None
        );

        processed.Should().Equal("BOVA11", "PIBB11", "IVVB11");
        delays.Should().Equal(7000, 7000, 7000); // one delay per ticker, none skipped
        outcome.Processed.Should().Be(3);
        outcome.AnyFailure.Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_ZeroSpacing_ConfiguredBudget_IsHonored()
    {
        var delays = new List<int>();

        await DividendQueue.RunAsync(
            ["A", "B"],
            (_, _) => Task.FromResult(Ok()),
            ms =>
            {
                delays.Add(ms);
                return Task.CompletedTask;
            },
            spacingMs: 0, // Providers:Brapi:DividendSpacingMs=0 → no artificial pacing
            CancellationToken.None
        );

        delays.Should().Equal(0, 0);
    }

    [Fact]
    public async Task RunAsync_SoftFailure_DoesNotEscalateToHard()
    {
        var calls = 0;

        var outcome = await DividendQueue.RunAsync(
            ["X"],
            (_, _) => Task.FromResult(Fail("Provider.PoolExhausted")),
            _ => Task.CompletedTask,
            7000,
            CancellationToken.None
        );

        calls.Should().Be(0); // lambda untouched by this assertion; classification is what matters
        outcome.FirstErrorCode.Should().Be("Provider.PoolExhausted");
        outcome.AnyHardFailure.Should().BeFalse();
        outcome.AnyFailure.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_HardFailure_AndFirstErrorAreReported()
    {
        var outcome = await DividendQueue.RunAsync(
            ["A", "B"],
            (_, _) => Task.FromResult(Fail("Brapi.HttpError", "HTTP 500")),
            _ => Task.CompletedTask,
            7000,
            CancellationToken.None
        );

        outcome.FirstErrorCode.Should().Be("Brapi.HttpError");
        outcome.FirstErrorMessage.Should().Be("HTTP 500");
        outcome.Processed.Should().Be(0);
        outcome.AnyHardFailure.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_MixedSoftThenHard_AnyHardWins()
    {
        var n = 0;
        var outcome = await DividendQueue.RunAsync(
            ["A", "B"],
            (_, _) =>
                Task.FromResult(++n == 1 ? Fail("Provider.CircuitOpen") : Fail("Brapi.NoData")),
            _ => Task.CompletedTask,
            7000,
            CancellationToken.None
        );

        outcome.FirstErrorCode.Should().Be("Provider.CircuitOpen"); // first error recorded
        outcome.AnyHardFailure.Should().BeTrue(); // Brapi.NoData is hard
    }

    [Fact]
    public async Task RunAsync_ExceptionsClassifyAsHardAndKeepProcessingTheQueue()
    {
        var seen = new List<string>();

        var outcome = await DividendQueue.RunAsync(
            ["A", "B"],
            (ticker, _) =>
            {
                seen.Add(ticker);
                return ticker == "A"
                    ? throw new InvalidOperationException("db down")
                    : Task.FromResult(Ok());
            },
            _ => Task.CompletedTask,
            7000,
            CancellationToken.None
        );

        seen.Should().Equal("A", "B"); // a throwing ticker never aborts the queue
        outcome.FirstErrorCode.Should().Be("DailyClose.Exception");
        outcome.AnyHardFailure.Should().BeTrue();
        outcome.Processed.Should().Be(1);
    }

    [Fact]
    public async Task RunAsync_EmptyQueue_NoDelaysNoCalls()
    {
        var delays = 0;

        var outcome = await DividendQueue.RunAsync(
            [],
            (_, _) => Task.FromResult(Ok()),
            _ =>
            {
                delays++;
                return Task.CompletedTask;
            },
            7000,
            CancellationToken.None
        );

        delays.Should().Be(0);
        outcome.Processed.Should().Be(0);
        outcome.AnyFailure.Should().BeFalse();
    }
}
