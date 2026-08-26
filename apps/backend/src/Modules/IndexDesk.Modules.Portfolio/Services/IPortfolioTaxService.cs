using IndexDesk.BuildingBlocks.Common.Results;

namespace IndexDesk.Modules.Portfolio.Services;

/// <summary>
/// Camada fiscal educacional da carteira: simulação de resgate (IR regressivo/swing,
/// IOF, come-cotas, isenções) e projeção mensal de DARF — sempre sobre dados locais.
/// </summary>
public interface IPortfolioTaxService
{
    /// <summary>
    /// Simula o resgate/venda de um ativo da carteira. <paramref name="quantity"/> null
    /// usa a posição total; <paramref name="broker"/> null agrega todas as corretoras.
    /// </summary>
    Task<Result<RedemptionTaxDto>> SimulateRedemptionAsync(
        Guid userId,
        Guid portfolioId,
        Guid assetId,
        string? broker = null,
        decimal? quantity = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// Projeta o DARF do mês (código 6015 para renda variável) agrupando as vendas
    /// realizadas por classe de ativo, com as isenções mensais PF aplicadas.
    /// </summary>
    Task<Result<DarfProjectionDto>> GetProjectionAsync(
        Guid userId,
        Guid portfolioId,
        int year,
        int month,
        CancellationToken ct = default
    );
}
