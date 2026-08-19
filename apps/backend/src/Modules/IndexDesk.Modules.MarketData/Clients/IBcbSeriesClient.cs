using IndexDesk.BuildingBlocks.Common.Results;

namespace IndexDesk.Modules.MarketData.Clients;

public interface IBcbSeriesClient
{
    /// <summary>
    /// Known BCB SGS series codes used by IndexDesk.
    /// </summary>
    int CdiSeriesCode { get; } // 12
    int SelicSeriesCode { get; } // 11
    int IpcaSeriesCode { get; } // 433
    int IgpmSeriesCode { get; } // 189

    Task<Result<IReadOnlyList<BcbSeriesPoint>>> GetSeriesAsync(
        int seriesCode,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default
    );
}

public readonly record struct BcbSeriesPoint(DateOnly Date, decimal Value);
