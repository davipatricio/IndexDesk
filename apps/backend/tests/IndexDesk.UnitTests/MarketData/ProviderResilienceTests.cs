using System.Net.Http;
using FluentAssertions;
using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.Modules.MarketData.Resilience;
using IndexDesk.UnitTests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IndexDesk.UnitTests.MarketData;

/// <summary>
/// Offline tests for the per-provider Polly pipeline: retry counts (transient-only),
/// circuit breaker opening after the failure threshold and recovering via half-open,
/// and short-circuit mapping to <c>Provider.CircuitOpen</c>.
/// </summary>
public class ProviderResilienceTests
{
    private static Result<string> Ok(string value = "data") => Result<string>.Success(value);

    private static Result<string> Fail(string code, string message = "boom") =>
        Result<string>.Failure(Error.Failure(code, message));

    [Fact]
    public async Task Retries_TransientCodes_UpToConfiguredAttempts()
    {
        var resilience = ResilienceTestKit.NewResilience(
            new ProviderResilienceOptions
            {
                HttpMaxRetries = 2,
                HttpRetryBaseDelay = TimeSpan.FromMilliseconds(1),
            }
        );
        var attempts = 0;

        var result = await resilience.ExecuteAsync(
            "Brapi",
            "test",
            () =>
            {
                attempts++;
                return attempts < 3
                    ? Task.FromResult(Fail("Brapi.HttpError"))
                    : Task.FromResult(Ok());
            }
        );

        result.IsSuccess.Should().BeTrue();
        attempts.Should().Be(3); // 1 original + 2 retries
    }

    [Theory]
    [InlineData("Brapi.NoData")]
    [InlineData("InfoMoney.NoApiKey")]
    [InlineData("Sidecar.ParseError")]
    [InlineData("Scrape.WafBlocked")]
    public async Task PermanentCodes_PassThroughWithoutRetry(string errorCode)
    {
        var resilience = ResilienceTestKit.NewResilience(
            new ProviderResilienceOptions { HttpMaxRetries = 2 }
        );
        var attempts = 0;

        var result = await resilience.ExecuteAsync(
            "Brapi",
            "test",
            () =>
            {
                attempts++;
                return Task.FromResult(Fail(errorCode));
            }
        );

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(errorCode);
        attempts.Should().Be(1);
    }

    [Fact]
    public async Task PoolExhausted_IsSoft_PassesThroughUntouched()
    {
        var resilience = ResilienceTestKit.NewResilience();
        var attempts = 0;

        var result = await resilience.ExecuteAsync(
            "Brapi",
            "test",
            () =>
            {
                attempts++;
                return Task.FromResult(Fail("Provider.PoolExhausted"));
            }
        );

        result.Error.Code.Should().Be("Provider.PoolExhausted");
        attempts.Should().Be(1);
    }

    [Fact]
    public async Task SidecarMode_RetriesTimeoutAndFetchFailedOnly()
    {
        var resilience = ResilienceTestKit.NewResilience(
            new ProviderResilienceOptions
            {
                SidecarMaxRetries = 1,
                HttpRetryBaseDelay = TimeSpan.FromMilliseconds(1),
            }
        );
        var timeoutAttempts = 0;
        var parseAttempts = 0;

        var timeoutResult = await resilience.ExecuteAsync(
            "YahooSidecar",
            "test",
            () =>
            {
                timeoutAttempts++;
                return Task.FromResult(timeoutAttempts == 1 ? Fail("Sidecar.Timeout") : Ok());
            },
            mode: ProviderMode.Sidecar
        );
        await resilience.ExecuteAsync(
            "YahooSidecar",
            "test-parse",
            () =>
            {
                parseAttempts++;
                return Task.FromResult(Fail("Sidecar.ParseError"));
            },
            mode: ProviderMode.Sidecar
        );

        timeoutResult.IsSuccess.Should().BeTrue(); // recovered on the single retry
        timeoutAttempts.Should().Be(2);
        parseAttempts.Should().Be(1); // parse errors are terminal
    }

    [Fact]
    public async Task Breaker_OpensAfterFailureThreshold_AndShortCircuits()
    {
        var resilience = ResilienceTestKit.NewResilience(
            new ProviderResilienceOptions
            {
                BreakerFailures = 3,
                BreakerSamplingDuration = TimeSpan.FromSeconds(60),
                BreakerBreakDuration = TimeSpan.FromSeconds(30),
            }
        );
        var attempts = 0;
        Task<Result<string>> AlwaysFail()
        {
            attempts++;
            return Task.FromResult(Fail("Brapi.HttpError"));
        }

        for (var i = 0; i < 3; i++)
        {
            (await resilience.ExecuteAsync("Brapi", "test", AlwaysFail))
                .IsFailure.Should()
                .BeTrue();
        }

        attempts.Should().Be(3);

        // Circuit is open: the callback must not even be invoked.
        var open = await resilience.ExecuteAsync("Brapi", "test", AlwaysFail);
        open.IsFailure.Should().BeTrue();
        open.Error.Code.Should().Be("Provider.CircuitOpen");
        attempts.Should().Be(3);
    }

    [Fact]
    public async Task Breaker_HalfOpenTrialRecovers_AfterBreakDurationElapses()
    {
        var resilience = ResilienceTestKit.NewResilience(
            new ProviderResilienceOptions
            {
                BreakerFailures = 3,
                // Polly enforces a 500 ms minimum break duration — smallest deterministic
                // window available without clock injection.
                BreakerBreakDuration = TimeSpan.FromMilliseconds(500),
            }
        );
        for (var i = 0; i < 3; i++)
        {
            await resilience.ExecuteAsync(
                "TradingView",
                "test",
                () => Task.FromResult(Fail("Sidecar.FetchFailed"))
            );
        }

        (await resilience.ExecuteAsync("TradingView", "test", () => Task.FromResult(Ok())))
            .Error.Code.Should()
            .Be("Provider.CircuitOpen");

        // Real-clock wait past the break window (small margin over the 500 ms minimum).
        await Task.Delay(800);

        var recovered = await resilience.ExecuteAsync(
            "TradingView",
            "test",
            () => Task.FromResult(Ok())
        );
        recovered.IsSuccess.Should().BeTrue(); // half-open trial succeeded → closed

        // And subsequent calls flow normally again.
        (await resilience.ExecuteAsync("TradingView", "test", () => Task.FromResult(Ok())))
            .IsSuccess.Should()
            .BeTrue();
    }

    [Fact]
    public async Task BreakerState_IsPerProvider()
    {
        var resilience = ResilienceTestKit.NewResilience(
            new ProviderResilienceOptions { BreakerFailures = 2 }
        );
        for (var i = 0; i < 2; i++)
        {
            await resilience.ExecuteAsync(
                "Brapi",
                "test",
                () => Task.FromResult(Fail("Brapi.HttpError"))
            );
        }

        (await resilience.ExecuteAsync("Brapi", "test", () => Task.FromResult(Ok())))
            .Error.Code.Should()
            .Be("Provider.CircuitOpen"); // Brapi open…

        (await resilience.ExecuteAsync("AwesomeApi", "test", () => Task.FromResult(Ok())))
            .IsSuccess.Should()
            .BeTrue(); // …but AwesomeApi untouched
    }

    [Fact]
    public void AsProviderCall_WrapsTransportExceptionsUnderTheProviderNamespace() =>
        ProviderResilience
            .AsProviderCall("YahooSidecar", new HttpRequestException("tls blocked"))
            .ErrorCode.Should()
            .Be("YahooSidecar.Exception");
}
