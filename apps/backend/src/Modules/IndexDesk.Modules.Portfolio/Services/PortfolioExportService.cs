using System.Globalization;
using System.Text;
using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IndexDesk.Modules.Portfolio.Services;

/// <summary>
/// Exportação CSV (posições e transações) com BOM UTF-8, separador ";" e decimais pt-BR.
/// XLSX formatado fica pendente de decisão de pacote (ClosedXML) — documentado no plano M-P5.
/// </summary>
public sealed class PortfolioExportService(IndexDeskDbContext db) : IPortfolioExportService
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public async Task<Result<(byte[] Content, string FileName)>> PositionsCsvAsync(
        Guid userId,
        Guid portfolioId,
        CancellationToken ct
    )
    {
        var owned = await db.Portfolios.AnyAsync(p => p.Id == portfolioId && p.UserId == userId, ct);
        if (!owned)
            return Result<(byte[], string)>.Failure(
                Error.NotFound("Portfolio", portfolioId.ToString())
            );

        var summaryResult = await new PortfolioService(db)
            .GetSummaryInternalAsync(portfolioId, ct);
        if (summaryResult.IsFailure)
            return Result<(byte[], string)>.Failure(summaryResult.Error);

        var summary = summaryResult.Value;

        var sb = new StringBuilder();
        sb.AppendLine(
            "Ticker;Nome;Corretora;Quantidade;Preço médio;Investido;Preço atual;Valor atual;Não realizado;Realizado;Renda recebida;Peso %"
        );
        foreach (var p in summary.Positions)
        {
            sb.AppendLine(
                string.Join(
                    ';',
                    p.Ticker.Replace(';', ','),
                    p.Name.Replace(';', ','),
                    p.Broker,
                    Num(p.Quantity),
                    Num(p.AveragePrice),
                    Num(p.InvestedAmount),
                    Num(p.CurrentPrice),
                    Num(p.CurrentValue),
                    Num(p.UnrealizedPnl),
                    Num(p.RealizedPnl),
                    Num(p.IncomeReceived),
                    Percent(p.CurrentValue, summary.TotalValue)
                )
            );
        }

        return Result<(byte[], string)>.Success(
            (Encoding.UTF8.GetBytes(sb.ToString()), $"carteira-{portfolioId:N}-posicoes.csv")
        );
    }

    public async Task<Result<(byte[] Content, string FileName)>> TransactionsCsvAsync(
        Guid userId,
        Guid portfolioId,
        CancellationToken ct
    )
    {
        var owned = await db.Portfolios.AnyAsync(p => p.Id == portfolioId && p.UserId == userId, ct);
        if (!owned)
            return Result<(byte[], string)>.Failure(
                Error.NotFound("Portfolio", portfolioId.ToString())
            );

        var rows = await (
            from t in db.PortfolioTransactions.Where(t => t.PortfolioId == portfolioId)
            join a in db.Assets on t.AssetId equals a.Id into assets
            from a in assets.DefaultIfEmpty()
            orderby t.TradeDate descending
            select new
            {
                t.Type,
                t.Broker,
                Ticker = a != null ? a.Ticker : t.SyntheticIndexCode,
                t.Quantity,
                t.UnitPrice,
                t.GrossAmount,
                t.Fees,
                t.Currency,
                t.TradeDate,
                t.Notes,
            }
        ).ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("Data;Tipo;Ativo;Corretora;Quantidade;Valor unitário;Total;Despesas;Moeda;Notas");
        foreach (var t in rows)
        {
            sb.AppendLine(
                string.Join(
                    ';',
                    t.TradeDate.ToString("yyyy-MM-dd"),
                    t.Type,
                    Clean(t.Ticker),
                    Clean(t.Broker),
                    Num(t.Quantity ?? 0),
                    Num(t.UnitPrice ?? 0),
                    Num(t.GrossAmount),
                    Num(t.Fees),
                    t.Currency,
                    Clean(t.Notes)
                )
            );
        }

        return Result<(byte[], string)>.Success(
            (Encoding.UTF8.GetBytes(sb.ToString()), $"carteira-{portfolioId:N}-transacoes.csv")
        );
    }

    // ---------- helpers ----------

    private static string Num(decimal value) => value.ToString("0.########", PtBr);

    private static string Percent(decimal value, decimal total) =>
        total > 0 ? (value / total * 100m).ToString("0.00", PtBr) : "0,00";

    private static string Clean(string? text) =>
        (text ?? string.Empty).Replace(';', ',').Replace('\n', ' ').Replace('\r', ' ');
}

public interface IPortfolioExportService
{
    Task<Result<(byte[] Content, string FileName)>> PositionsCsvAsync(
        Guid userId,
        Guid portfolioId,
        CancellationToken ct
    );

    Task<Result<(byte[] Content, string FileName)>> TransactionsCsvAsync(
        Guid userId,
        Guid portfolioId,
        CancellationToken ct
    );
}
