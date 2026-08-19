using FluentAssertions;
using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.Modules.MarketData.Clients;
using IndexDesk.Modules.MarketData.Domain;
using IndexDesk.Modules.MarketData.Pipeline;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IndexDesk.UnitTests.MarketData;

public class FallbackMarketDataServiceTests
{
    private readonly MarketDataValidator _validator = new();

    [Fact]
    public async Task WhenPrimarySucceeds_ReturnsPrimaryQuotes_AndDoesNotCallSecondary()
    {
        // Arrange
        var primaryQuote = new NormalizedQuote(
            "MXRF11",
            new DateOnly(2024, 2, 15),
            10.5m,
            10.6m,
            10.4m,
            10.55m,
            10.55m,
            1000,
            null,
            "Brapi",
            DateTimeOffset.UtcNow
        );

        var primaryMock = new FakeMarketDataClient(
            "Brapi",
            priority: 1,
            quotesToReturn: new[] { primaryQuote }
        );
        var secondaryMock = new FakeMarketDataClient("YahooFinance", priority: 2, shouldFail: true);

        var service = new FallbackMarketDataService(
            new[] { primaryMock, secondaryMock },
            _validator,
            NullLogger<FallbackMarketDataService>.Instance
        );

        // Act
        var result = await service.GetQuotesWithFallbackAsync(
            "MXRF11",
            new DateOnly(2024, 1, 1),
            new DateOnly(2024, 12, 31)
        );

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].SourceProvider.Should().Be("Brapi");
        primaryMock.CallCount.Should().Be(1);
        secondaryMock.CallCount.Should().Be(0); // Never called
    }

    [Fact]
    public async Task WhenPrimaryRateLimited_FallsBackToSecondary_AndSucceeds()
    {
        // Arrange
        var secondaryQuote = new NormalizedQuote(
            "VWRA11",
            new DateOnly(2024, 2, 15),
            140.0m,
            142.0m,
            139.5m,
            141.2m,
            141.2m,
            5000,
            null,
            "YahooFinance",
            DateTimeOffset.UtcNow
        );

        var primaryMock = new FakeMarketDataClient(
            "Brapi",
            priority: 1,
            shouldFail: true,
            failureError: Error.Failure("Brapi.RateLimit", "Rate limit 429")
        );
        var secondaryMock = new FakeMarketDataClient(
            "YahooFinance",
            priority: 2,
            quotesToReturn: new[] { secondaryQuote }
        );
        var contingencyMock = new FakeMarketDataClient(
            "HGBrasil",
            priority: 3,
            quotesToReturn: Array.Empty<NormalizedQuote>()
        );

        var service = new FallbackMarketDataService(
            new[] { primaryMock, secondaryMock, contingencyMock },
            _validator,
            NullLogger<FallbackMarketDataService>.Instance
        );

        // Act
        var result = await service.GetQuotesWithFallbackAsync(
            "VWRA11",
            new DateOnly(2024, 1, 1),
            new DateOnly(2024, 12, 31)
        );

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].SourceProvider.Should().Be("YahooFinance");
        primaryMock.CallCount.Should().Be(1);
        secondaryMock.CallCount.Should().Be(1);
        contingencyMock.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task WhenPrimaryAndSecondaryFail_FallsBackToContingency()
    {
        // Arrange
        var contingencyQuote = new NormalizedQuote(
            "GOLD11",
            DateOnly.FromDateTime(DateTime.UtcNow),
            11.5m,
            11.5m,
            11.5m,
            11.5m,
            11.5m,
            0,
            null,
            "HGBrasil",
            DateTimeOffset.UtcNow
        );

        var primaryMock = new FakeMarketDataClient("Brapi", priority: 1, shouldFail: true);
        var secondaryMock = new FakeMarketDataClient("YahooFinance", priority: 2, shouldFail: true);
        var contingencyMock = new FakeMarketDataClient(
            "HGBrasil",
            priority: 3,
            quotesToReturn: new[] { contingencyQuote }
        );

        var service = new FallbackMarketDataService(
            new[] { primaryMock, secondaryMock, contingencyMock },
            _validator,
            NullLogger<FallbackMarketDataService>.Instance
        );

        // Act
        var result = await service.GetQuotesWithFallbackAsync(
            "GOLD11",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            DateOnly.FromDateTime(DateTime.UtcNow)
        );

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value[0].SourceProvider.Should().Be("HGBrasil");
        primaryMock.CallCount.Should().Be(1);
        secondaryMock.CallCount.Should().Be(1);
        contingencyMock.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task WhenAllProvidersFail_ReturnsFailureResult()
    {
        // Arrange
        var primaryMock = new FakeMarketDataClient("Brapi", priority: 1, shouldFail: true);
        var secondaryMock = new FakeMarketDataClient("YahooFinance", priority: 2, shouldFail: true);

        var service = new FallbackMarketDataService(
            new[] { primaryMock, secondaryMock },
            _validator,
            NullLogger<FallbackMarketDataService>.Instance
        );

        // Act
        var result = await service.GetQuotesWithFallbackAsync(
            "MXRF11",
            new DateOnly(2024, 1, 1),
            new DateOnly(2024, 12, 31)
        );

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("FallbackEngine.Exhausted");
    }

    [Fact]
    public async Task WhenPrimaryReturnsCorruptedQuotes_ValidatorRejectsAndTriggersSecondary()
    {
        // Arrange: Primary returns negative price (-10.5)
        var corruptedQuote = new NormalizedQuote(
            "MXRF11",
            new DateOnly(2024, 2, 15),
            -10.5m,
            10.6m,
            10.4m,
            10.55m,
            10.55m,
            1000,
            null,
            "Brapi",
            DateTimeOffset.UtcNow
        );

        var validSecondaryQuote = new NormalizedQuote(
            "MXRF11",
            new DateOnly(2024, 2, 15),
            10.5m,
            10.6m,
            10.4m,
            10.55m,
            10.55m,
            1000,
            null,
            "YahooFinance",
            DateTimeOffset.UtcNow
        );

        var primaryMock = new FakeMarketDataClient(
            "Brapi",
            priority: 1,
            quotesToReturn: new[] { corruptedQuote }
        );
        var secondaryMock = new FakeMarketDataClient(
            "YahooFinance",
            priority: 2,
            quotesToReturn: new[] { validSecondaryQuote }
        );

        var service = new FallbackMarketDataService(
            new[] { primaryMock, secondaryMock },
            _validator,
            NullLogger<FallbackMarketDataService>.Instance
        );

        // Act
        var result = await service.GetQuotesWithFallbackAsync(
            "MXRF11",
            new DateOnly(2024, 1, 1),
            new DateOnly(2024, 12, 31)
        );

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value[0].SourceProvider.Should().Be("YahooFinance");
        primaryMock.CallCount.Should().Be(1);
        secondaryMock.CallCount.Should().Be(1);
    }

    private sealed class FakeMarketDataClient : IMarketDataClient
    {
        private readonly IReadOnlyList<NormalizedQuote> _quotesToReturn;
        private readonly bool _shouldFail;
        private readonly Error _failureError;

        public string ProviderName { get; }
        public int Priority { get; }
        public int CallCount { get; private set; }

        public FakeMarketDataClient(
            string providerName,
            int priority,
            IReadOnlyList<NormalizedQuote>? quotesToReturn = null,
            bool shouldFail = false,
            Error? failureError = null
        )
        {
            ProviderName = providerName;
            Priority = priority;
            _quotesToReturn = quotesToReturn ?? Array.Empty<NormalizedQuote>();
            _shouldFail = shouldFail;
            _failureError =
                failureError ?? Error.Failure($"{providerName}.Error", "Simulated failure");
        }

        public bool SupportsTicker(string ticker) => true;

        public Task<Result<IReadOnlyList<NormalizedQuote>>> GetHistoricalQuotesAsync(
            string ticker,
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken cancellationToken = default
        )
        {
            CallCount++;
            if (_shouldFail)
            {
                return Task.FromResult(
                    Result<IReadOnlyList<NormalizedQuote>>.Failure(_failureError)
                );
            }

            return Task.FromResult(Result<IReadOnlyList<NormalizedQuote>>.Success(_quotesToReturn));
        }

        public Task<Result<IReadOnlyList<NormalizedDividend>>> GetDividendsAsync(
            string ticker,
            CancellationToken cancellationToken = default
        )
        {
            return Task.FromResult(
                Result<IReadOnlyList<NormalizedDividend>>.Success(Array.Empty<NormalizedDividend>())
            );
        }
    }
}
