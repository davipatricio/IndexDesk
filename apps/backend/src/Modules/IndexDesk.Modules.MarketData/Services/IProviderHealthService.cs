using IndexDesk.Modules.MarketData.Domain;

namespace IndexDesk.Modules.MarketData.Services;

public interface IProviderHealthService
{
    Task<ProviderHealthResponseDto> GetProvidersHealthAsync(
        CancellationToken cancellationToken = default
    );
}
