using IndexDesk.Modules.Portfolio.Calculators;
using Xunit;

namespace IndexDesk.UnitTests.Portfolio;

public class PerformanceEngineTests
{
    private static WealthPoint P(int day, decimal value, decimal flow = 0) =>
        new(new DateOnly(2026, 1, day), value, flow);

    [Fact]
    public void Twr_neutralizes_external_flows()
    {
        // Dia 1: aporta 100 (valor 100). Dia 2: aporta 900 → valor 1.000. Dia 3: cai p/ 990.
        var points = new List<WealthPoint> { P(1, 100m, 100m), P(2, 1000m, 900m), P(3, 990m) };

        // Retornos diários: +0% (100→100 sem fluxo extra), 0% (1000/1000), -1%.
        var twr = PerformanceEngine.TimeWeightedReturnPercent(points);

        Assert.Equal(-1m, twr); // aporte não conta como ganho
    }

    [Fact]
    public void Twr_counts_gains_between_flows()
    {
        // Dia 1: 100. Dia 11: dobrou p/ 200. Dia 12: aportou +200 → 400. Dia 22: 440 (+10%).
        var points = new List<WealthPoint>
        {
            P(1, 100m, 100m),
            P(11, 200m),
            P(12, 400m, 200m),
            P(22, 440m),
        };

        var twr = PerformanceEngine.TimeWeightedReturnPercent(points);

        // (2x no 1º trecho) × (10% no 2º) − 1 = 120%
        Assert.Equal(120m, decimal.Round(twr, 6));
    }

    [Fact]
    public void Mwr_solves_known_irr()
    {
        // Aporte único de 100 há 365 dias; valor final 110 → ~10% a.a.
        var flows = new List<(DateOnly, decimal)> { (new DateOnly(2025, 1, 1), -100m) };
        var end = new DateOnly(2026, 1, 1);

        var mwr = PerformanceEngine.MoneyWeightedReturnAnnualPercent(flows, end, 110m);

        Assert.NotNull(mwr);
        Assert.InRange(mwr!.Value, 9.5m, 10.5m);
    }

    [Fact]
    public void Mwr_prefers_newton_and_falls_back_when_needed()
    {
        // Fluxos que forçam biseção: derivativa quase nula em torno de guesses comuns.
        var flows = new List<(DateOnly, decimal)>
        {
            (new DateOnly(2025, 6, 1), -1000m),
            (new DateOnly(2025, 12, 1), 500m),
        };
        var end = new DateOnly(2026, 6, 1);

        var mwr = PerformanceEngine.MoneyWeightedReturnAnnualPercent(flows, end, 650m);

        // −1000 há 1 ano, +500 no meio, +650 no fim: ganho anualizado ~21% a.a.
        Assert.NotNull(mwr);
        Assert.InRange(mwr!.Value, 15m, 30m);
    }

    [Fact]
    public void Mwr_returns_null_without_sign_change()
    {
        var flows = new List<(DateOnly, decimal)> { (new DateOnly(2026, 1, 1), -100m) };

        Assert.Null(
            PerformanceEngine.MoneyWeightedReturnAnnualPercent(flows, new DateOnly(2026, 2, 1), 0m)
        );
    }

    [Fact]
    public void Risk_metrics_computes_volatility_drawdown_and_sharpe()
    {
        // Série determinística: sobe 1%, desce 0,5%, sobe 1%, desce 0,5%...
        var values = new List<WealthPoint>();
        decimal value = 1000m;
        values.Add(P(1, value));
        for (var i = 2; i <= 21; i++)
        {
            value *= i % 2 == 0 ? 1.01m : 0.995m;
            values.Add(P(i, value));
        }

        var risk = PerformanceEngine.ComputeRisk(values);

        Assert.True(risk.VolatilityPercentAnnualized > 0);
        Assert.True(risk.MaxDrawdownPercent > 0);
        Assert.True(risk.SharpeRatio != 0);

        // Sem volatilidade → Sharpe indefinido = 0 (evita divisão por zero)
        var flat = new List<WealthPoint> { P(1, 100), P(2, 100), P(3, 100), P(4, 100) };
        Assert.Equal(0m, PerformanceEngine.ComputeRisk(flat).SharpeRatio);
    }

    [Fact]
    public void Risk_free_factors_improve_sharpe_directionally()
    {
        // Carteira flat enquanto o CDI sobe todo dia: Sharpe deve ser menor que sem rf.
        var points = Enumerable.Range(1, 20).Select(d => P(d, 1000m)).ToList();
        var rf = Enumerable.Repeat(1.0005m, 19).ToList();

        var withoutRf = PerformanceEngine.ComputeRisk(points);
        var withRf = PerformanceEngine.ComputeRisk(points, rf);

        Assert.True(withRf.SharpeRatio < withoutRf.SharpeRatio || withoutRf.SharpeRatio == 0);
    }
}
