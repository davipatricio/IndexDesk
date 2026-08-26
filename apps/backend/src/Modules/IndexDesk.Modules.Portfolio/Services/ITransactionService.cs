using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.BuildingBlocks.Persistence;

namespace IndexDesk.Modules.Portfolio.Services;

public interface ITransactionService
{
    Task<Result<TransactionDto>> CreateAsync(
        Guid userId,
        Guid portfolioId,
        CreateTransactionRequest request,
        CancellationToken ct
    );

    Task<Result<PagedTransactionsDto>> ListAsync(
        Guid userId,
        Guid portfolioId,
        int page,
        int pageSize,
        CancellationToken ct
    );

    /// <summary>Logical amendment: supersedes the original, history stays auditable.</summary>
    Task<Result<TransactionDto>> AmendAsync(
        Guid userId,
        Guid portfolioId,
        Guid transactionId,
        CreateTransactionRequest request,
        CancellationToken ct
    );
}
