using IndexDesk.BuildingBlocks.Common.Results;
using IndexDesk.BuildingBlocks.Persistence;
using IndexDesk.BuildingBlocks.Persistence.Entities;
using IndexDesk.Modules.Portfolio.Calculators;
using Microsoft.EntityFrameworkCore;

namespace IndexDesk.Modules.Portfolio.Services;

/// <summary>
/// Builds the daily wealth series on demand from the transaction log valued with local data only
/// (asset_quotes forward-filled, fx_rates for foreign currency, macro_economic_series for CDI).
/// Deterministic and re-runnable — the same inputs always produce the same series.
/// </summary>
public sealed class PortfolioPerformanceService(IndexDeskDbContext db)
    : IPortfolioPerformanceService
{
    public async Task<Result<PerformanceResultDto>> GetAsync(
        Guid userId,
        Guid portfolioId,
        DateOnly? from,
        DateOnly? to,
        string? benchmarks,
        CancellationToken ct
    )
    {
        var owned = await db.Portfolios.FirstOrDefaultAsync(
            p => p.Id == portfolioId && p.UserId == userId,
            ct
        );
        if (owned is null)
            return Result<PerformanceResultDto>.Failure(
                Error.NotFound("Portfolio", portfolioId.ToString())
            );

        var transactions = await db
            .PortfolioTransactions.Where(t =>
                t.PortfolioId == portfolioId && t.ReversedByTransactionId == null
            )
            .OrderBy(t => t.TradeDate)
            .ToListAsync(ct);
        if (transactions.Count == 0)
            return Result<PerformanceResultDto>.Failure(
                Error.Validation("PortfolioEmpty", "A carteira ainda não tem transações.")
            );

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var firstTradeDate = transactions[0].TradeDate;
        var start = from is null || from < firstTradeDate ? firstTradeDate : from.Value;
        var end = to is null || to > today ? today : to.Value;
        if (end < start)
            return Result<PerformanceResultDto>.Failure(
                Error.Validation("RangeInvalid", "Data final anterior à inicial.")
            );

        // ----- local market data -----
        var assetIds = transactions
            .Where(t => t.AssetId != null)
            .Select(t => t.AssetId!.Value)
            .Distinct()
            .ToList();
        var currencies = await db
            .Assets.Where(a => assetIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Currency, ct);

        var closes = new Dictionary<Guid, List<(DateOnly Date, decimal Close)>>();
        foreach (var id in assetIds)
        {
            closes[id] = await db
                .AssetQuotes.Where(q => q.AssetId == id && q.Date >= start && q.Date <= end)
                .OrderBy(q => q.Date)
                .Select(q => new ValueTuple<DateOnly, decimal>(q.Date, q.Close))
                .ToListAsync(ct);
        }

        var pairs = currencies
            .Values.Where(c => c != "BRL")
            .Select(c => $"{c}-BRL")
            .Distinct()
            .ToList();
        var fxRates =
            pairs.Count == 0
                ? []
                : await db
                    .FxRates.Where(f => pairs.Contains(f.Pair) && f.Date >= start && f.Date <= end)
                    .OrderBy(f => f.Date)
                    .ToListAsync(ct);

        var requestedCodes = string.IsNullOrWhiteSpace(benchmarks)
            ? new List<string> { "CDI", "IBOV" }
            : benchmarks
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(c => c.ToUpperInvariant())
                .Distinct()
                .ToList();
        // SGS codes (macro_economic_series): 12 = CDI, 11 = Selic, 433 = IPCA.
        var macroSgsByCode = new Dictionary<string, int>
        {
            ["CDI"] = 12,
            ["SELIC"] = 11,
            ["IPCA"] = 433,
        };
        var macroCodes = requestedCodes
            .Where(macroSgsByCode.ContainsKey)
            .Select(c => macroSgsByCode[c])
            .ToList();
        var macroByCode = new Dictionary<string, Dictionary<DateOnly, decimal>>();
        if (macroCodes.Count > 0)
        {
            var rows = await db
                .MacroEconomicSeries.Where(m =>
                    macroCodes.Contains(m.SeriesCode) && m.Date >= start && m.Date <= end
                )
                .OrderBy(m => m.Date)
                .ToListAsync(ct);
            foreach (var group in rows.GroupBy(m => m.SeriesCode))
            {
                var name = macroSgsByCode.First(kv => kv.Value == group.Key).Key;
                macroByCode[name] = group.ToDictionary(m => m.Date, m => m.Value);
            }
        }

        // ----- day grid: trade days ∪ quote days ∪ end -----
        var days = new SortedSet<DateOnly> { end };
        foreach (var t in transactions)
            if (t.TradeDate >= start && t.TradeDate <= end)
                days.Add(t.TradeDate);
        foreach (var list in closes.Values)
        foreach (var (d, _) in list)
            days.Add(d);

        // ----- walk the grid: apply flows, forward-fill prices, value positions -----
        var quantities = new Dictionary<Guid, decimal>();
        var cashQuantity = 0m; // synthetic CDI/Selic deposits valued at unit price 1
        var cashIncome = 0m; // INCOME accumulates as cash (accrual arrives in M-P3)
        var lastClose = new Dictionary<Guid, decimal>();
        var lastFx = new Dictionary<string, decimal>();

        var txByDate = transactions
            .GroupBy(t => t.TradeDate)
            .ToDictionary(g => g.Key, g => g.ToList());
        var quoteCursor = closes.ToDictionary(kv => kv.Key, kv => 0);
        var fxCursor = pairs.ToDictionary(p => p, p => fxRates.Where(f => f.Pair == p).ToList());
        var fxIndex = pairs.ToDictionary(p => p, _ => 0);

        var points = new List<WealthPoint>(days.Count);

        foreach (var day in days)
        {
            decimal flowToday = 0;

            if (txByDate.TryGetValue(day, out var todays))
            {
                foreach (var tx in todays)
                    flowToday += ApplyTransaction(tx, quantities, ref cashQuantity, ref cashIncome);
            }

            foreach (var id in assetIds)
            {
                var list = closes[id];
                var i = quoteCursor[id];
                while (i < list.Count && list[i].Date <= day)
                {
                    lastClose[id] = list[i].Close;
                    i++;
                }
                quoteCursor[id] = i;
            }

            foreach (var pair in pairs)
            {
                var list = fxCursor[pair];
                var i = fxIndex[pair];
                while (i < list.Count && list[i].Date <= day)
                {
                    lastFx[pair] = list[i].Bid;
                    i++;
                }
                fxIndex[pair] = i;
            }

            var value = cashIncome + cashQuantity;
            foreach (var (id, qty) in quantities)
            {
                if (qty <= 0 || !lastClose.TryGetValue(id, out var close))
                    continue;
                var currency = currencies[id];
                var factor =
                    currency == "BRL" ? 1m : lastFx.GetValueOrDefault($"{currency}-BRL", 0m);
                if (factor > 0)
                    value += qty * close * factor;
            }

            points.Add(new WealthPoint(day, decimal.Round(value, 2), decimal.Round(flowToday, 2)));
        }

        if (points.Count == 0 || points[^1].Value <= 0)
            return Result<PerformanceResultDto>.Failure(
                Error.Validation(
                    "PortfolioNotValued",
                    "Sem dados de mercado suficientes para valorizar a carteira no período."
                )
            );

        // Pontos antes da primeira cotação disponível valem 0 (feriados/fim de semana):
        // poda os zeros à esquerda preservando os fluxos externos acumulados neles.
        var firstValued = points.FindIndex(p => p.Value > 0);
        if (firstValued > 0)
        {
            var carriedFlow = points.Take(firstValued).Sum(p => p.ExternalFlow);
            var head = points[firstValued];
            points.RemoveRange(0, firstValued);
            points[0] = head with { ExternalFlow = head.ExternalFlow + carriedFlow };
        }

        // ----- metrics -----
        var totalReturn = points[^1].Value / points[0].Value - 1m;
        var twr = PerformanceEngine.TimeWeightedReturnPercent(points);
        // Engine XIRR espera investido como NEGATIVO; a série usa +aporte / −resgate.
        var flows = points
            .Where(p => p.ExternalFlow != 0)
            .Select(p => (p.Date, -p.ExternalFlow))
            .ToList();
        var mwr = PerformanceEngine.MoneyWeightedReturnAnnualPercent(flows, end, points[^1].Value);
        var riskFreeFactors = BuildDailyRiskFreeFactors(
                points,
                macroByCode.GetValueOrDefault("CDI")
            )
            .ToList();
        var risk = PerformanceEngine.ComputeRisk(points, riskFreeFactors);

        // ----- benchmarks on the same grid -----
        var benchmarkSeries = new List<BenchmarkSeriesDto>();
        foreach (var code in requestedCodes)
        {
            IReadOnlyList<decimal>? values = code switch
            {
                "CDI" or "SELIC" or "IPCA" when macroByCode.TryGetValue(code, out var map) =>
                    AccumulateMacroOnGrid(map, points),
                "IBOV" => await NormalizeAssetOnGrid("IBOV", days, start, end, ct),
                _ => null,
            };
            if (values is not null)
                benchmarkSeries.Add(new BenchmarkSeriesDto(code, values));
        }

        return Result<PerformanceResultDto>.Success(
            new PerformanceResultDto(
                points[0].Date,
                points[^1].Date,
                points
                    .Select(p => new PerformancePointDto(p.Date, p.Value, p.ExternalFlow))
                    .ToList(),
                decimal.Round((totalReturn) * 100m, 4),
                decimal.Round(twr, 4),
                mwr is null ? null : decimal.Round(mwr.Value, 4),
                risk.VolatilityPercentAnnualized,
                risk.SharpeRatio,
                risk.MaxDrawdownPercent,
                benchmarkSeries
            )
        );
    }

    /// <summary>Daily snapshot job: upserts the latest valuation of every non-empty portfolio.</summary>
    public async Task<int> SnapshotAllPortfoliosAsync(CancellationToken ct)
    {
        var rows = await db
            .Portfolios.Where(p => db.PortfolioTransactions.Any(t => t.PortfolioId == p.Id))
            .Select(p => new { p.Id, p.UserId })
            .ToListAsync(ct);

        var count = 0;
        foreach (var row in rows)
        {
            // benchmarks vazios: snapshot só precisa do último ponto, não das séries comparativas
            var result = await GetAsync(row.UserId, row.Id, null, null, string.Empty, ct);
            if (result.IsFailure)
                continue;
            var last = result.Value.Series[^1];

            var existing = await db.PortfolioDailySnapshots.FirstOrDefaultAsync(
                s => s.PortfolioId == row.Id && s.SnapshotDate == last.Date,
                ct
            );
            if (existing is null)
            {
                db.PortfolioDailySnapshots.Add(
                    new PortfolioDailySnapshotEntity
                    {
                        PortfolioId = row.Id,
                        SnapshotDate = last.Date,
                        TotalValue = last.Value,
                    }
                );
            }
            else
            {
                existing.TotalValue = last.Value;
            }
            count++;
        }

        await db.SaveChangesAsync(ct);
        return count;
    }

    // ---------- helpers ----------

    /// <summary>Returns the external flow moved by the transaction; mutates position state.</summary>
    private static decimal ApplyTransaction(
        PortfolioTransactionEntity tx,
        Dictionary<Guid, decimal> quantities,
        ref decimal cashQuantity,
        ref decimal cashIncome
    )
    {
        switch (tx.Type)
        {
            case "BUY":
            {
                var injected = tx.GrossAmount + tx.Fees;
                if (tx.AssetId is null)
                {
                    // Caixa sintético: o bruto entra como principal; a taxa é consumida na
                    // entrada (o fluxo externo já a embute) — não subtrair de novo.
                    cashQuantity += Math.Max(0m, tx.Quantity ?? tx.GrossAmount);
                }
                else
                    quantities[tx.AssetId.Value] =
                        quantities.GetValueOrDefault(tx.AssetId.Value) + (tx.Quantity ?? 0);
                return injected;
            }
            case "SELL":
            {
                if (tx.AssetId is not null)
                    quantities[tx.AssetId.Value] = Math.Max(
                        0m,
                        quantities.GetValueOrDefault(tx.AssetId.Value) - (tx.Quantity ?? 0)
                    );
                else
                    cashQuantity -= Math.Min(
                        cashQuantity,
                        Math.Max(0m, tx.Quantity ?? tx.GrossAmount)
                    );
                return -(tx.GrossAmount - tx.Fees);
            }
            case "TRANSFER_IN":
            {
                if (tx.AssetId is not null)
                    quantities[tx.AssetId.Value] =
                        quantities.GetValueOrDefault(tx.AssetId.Value) + (tx.Quantity ?? 0);
                else
                    cashQuantity += Math.Max(0m, tx.Quantity ?? tx.GrossAmount);
                return tx.GrossAmount;
            }
            case "TRANSFER_OUT":
            {
                if (tx.AssetId is not null)
                    quantities[tx.AssetId.Value] = Math.Max(
                        0m,
                        quantities.GetValueOrDefault(tx.AssetId.Value) - (tx.Quantity ?? 0)
                    );
                else
                    cashQuantity -= Math.Min(
                        cashQuantity,
                        Math.Max(0m, tx.Quantity ?? tx.GrossAmount)
                    );
                return -tx.GrossAmount;
            }
            case "INCOME":
                cashIncome += tx.GrossAmount - tx.Fees; // retorno da carteira, não fluxo externo
                return 0m;
            case "CORP_ACTION":
                ApplyCorpAction(quantities, tx);
                return 0m;
            default:
                return 0m;
        }
    }

    private static void ApplyCorpAction(
        Dictionary<Guid, decimal> quantities,
        PortfolioTransactionEntity tx
    )
    {
        if (tx.AssetId is null || string.IsNullOrWhiteSpace(tx.CorpActionJson))
            return;
        try
        {
            var json = System.Text.Json.JsonDocument.Parse(tx.CorpActionJson).RootElement;
            var kind = json.TryGetProperty("kind", out var k)
                ? k.GetString()?.ToLowerInvariant()
                : null;
            var factor = json.TryGetProperty("factor", out var f) ? f.GetDecimal() : 1m;
            var percent = json.TryGetProperty("percent", out var p)
                ? p.GetDecimal()
                : (decimal?)null;

            var current = quantities.GetValueOrDefault(tx.AssetId.Value);
            switch (kind)
            {
                // Mesmos guardas do PositionProjector (factor > 0 && != 1) para a série de
                // performance não divergir das posições do resumo.
                case "split" when factor > 0 && factor != 1:
                    quantities[tx.AssetId.Value] = current * factor;
                    break;
                case "grupamento" or "inpc" when factor > 0 && factor != 1:
                    quantities[tx.AssetId.Value] = current / factor;
                    break;
                case "bonificacao" when percent is not null && 1 + percent.Value > 0:
                    quantities[tx.AssetId.Value] = current * (1 + percent.Value);
                    break;
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // validado upstream em TransactionService; aqui só ignora
        }
    }

    private static IEnumerable<decimal> BuildDailyRiskFreeFactors(
        IReadOnlyList<WealthPoint> points,
        Dictionary<DateOnly, decimal>? cdiByDate
    )
    {
        if (cdiByDate is null || cdiByDate.Count == 0)
        {
            for (var i = 0; i < points.Count - 1; i++)
                yield return 1m;
            yield break;
        }

        // factors[i] alinha ao retorno entre os pontos i e i+1.
        for (var i = 0; i < points.Count - 1; i++)
        {
            var factor = 1m;
            for (var d = points[i].Date.AddDays(1); d <= points[i + 1].Date; d = d.AddDays(1))
                if (cdiByDate.TryGetValue(d, out var rate))
                    factor *= 1m + rate / 100m;
            yield return factor;
        }
    }

    private static IReadOnlyList<decimal> AccumulateMacroOnGrid(
        Dictionary<DateOnly, decimal> ratesByDate,
        IReadOnlyList<WealthPoint> points
    )
    {
        var result = new List<decimal>(points.Count);
        var factor = 100m; // base 100 no primeiro ponto
        result.Add(factor);

        for (var i = 1; i < points.Count; i++)
        {
            for (var d = points[i - 1].Date.AddDays(1); d <= points[i].Date; d = d.AddDays(1))
                if (ratesByDate.TryGetValue(d, out var rate))
                    factor *= 1m + rate / 100m;
            result.Add(decimal.Round(factor, 6));
        }
        return result;
    }

    private async Task<IReadOnlyList<decimal>?> NormalizeAssetOnGrid(
        string ticker,
        SortedSet<DateOnly> days,
        DateOnly start,
        DateOnly end,
        CancellationToken ct
    )
    {
        var asset = await db.Assets.FirstOrDefaultAsync(a => a.Ticker == ticker && a.IsActive, ct);
        if (asset is null)
            return null;

        var quotes = await db
            .AssetQuotes.Where(q => q.AssetId == asset.Id && q.Date >= start && q.Date <= end)
            .OrderBy(q => q.Date)
            .Select(q => new ValueTuple<DateOnly, decimal>(q.Date, q.Close))
            .ToListAsync(ct);
        if (quotes.Count == 0)
            return null;

        var byDate = quotes.ToDictionary(x => x.Item1, x => x.Item2);
        var result = new List<decimal>(days.Count);
        decimal? last = null;
        decimal? baseClose = null;
        foreach (var d in days)
        {
            if (byDate.TryGetValue(d, out var v))
            {
                last = v;
                baseClose ??= v;
            }
            // 0 antes do primeiro pregão; normaliza base 100 no primeiro fechamento.
            result.Add(last is > 0 && baseClose > 0 ? last.Value / baseClose.Value * 100m : 0m);
        }
        return result.All(r => r == 0m) ? null : result;
    }
}
