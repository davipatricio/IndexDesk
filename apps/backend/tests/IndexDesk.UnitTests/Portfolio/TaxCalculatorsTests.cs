using IndexDesk.Modules.Portfolio.Calculators;
using Xunit;

namespace IndexDesk.UnitTests.Portfolio;

public class TaxCalculatorsTests
{
    // ---------- tabela regressiva: fronteiras exatas 180/360/720 ----------

    [Theory]
    [InlineData(1, 22.5)]
    [InlineData(180, 22.5)] // fronteira inclusiva
    [InlineData(181, 20)]
    [InlineData(360, 20)] // fronteira inclusiva
    [InlineData(361, 17.5)]
    [InlineData(720, 17.5)] // fronteira inclusiva
    [InlineData(721, 15)]
    public void RegressiveIr_AppliesExactDayThresholds(int daysHeld, decimal expectedPercent) =>
        Assert.Equal(expectedPercent, TaxCalculators.RegressiveIrPercent(daysHeld));

    // ---------- IOF dia 0 / 15 / 29 / 30+ ----------

    [Fact]
    public void Iof_DayZero_ChargesFullIr() => Assert.Equal(22.5m, TaxCalculators.IofPercent(0));

    [Fact]
    public void Iof_DayFifteen_IsHalfOfIr() => Assert.Equal(11.25m, TaxCalculators.IofPercent(15)); // 22,5 × (1 − 15/30)

    [Fact]
    public void Iof_DayTwentyNine_IsOneThirtiethOfIr() =>
        Assert.Equal(0.75m, TaxCalculators.IofPercent(29)); // 22,5 × (1 − 29/30)

    [Theory]
    [InlineData(30)]
    [InlineData(45)]
    [InlineData(3000)]
    public void Iov_FromDayThirty_Onwards_IsZero(int daysHeld) =>
        Assert.Equal(0m, TaxCalculators.IofPercent(daysHeld));

    // ---------- simulação de resgate ----------

    [Fact]
    public void SimulateRedemption_PartialVariableSale_SplitsCostBasisByQuantity()
    {
        var breakdown = TaxCalculators.SimulateRedemption(
            new RedemptionInput(
                AssetClass: TaxAssetClasses.Stock,
                QuantityRedeemed: 400m,
                AvailableQuantity: 1000m,
                AveragePrice: 10m,
                UnitPrice: 15m,
                Fees: 0m,
                DaysHeld: 400
            )
        );

        Assert.Equal(6000m, breakdown.GrossAmount); // 400 × 15
        Assert.Equal(4000m, breakdown.CostBasis); // 400 × 10 (custo proporcional)
        Assert.Equal(2000m, breakdown.Profit);
        Assert.Equal(15m, breakdown.IrPercent);
        Assert.Equal(300m, breakdown.IrAmount);
        Assert.Equal(0m, breakdown.IofAmount);
        Assert.Equal(1700m, breakdown.NetAmount); // lucro 2000 − IR 300
        Assert.False(breakdown.ExemptApplied);
    }

    [Fact]
    public void SimulateRedemption_QuantityAbovePosition_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            TaxCalculators.SimulateRedemption(
                new RedemptionInput(
                    TaxAssetClasses.Stock,
                    QuantityRedeemed: 1001m,
                    AvailableQuantity: 1000m,
                    AveragePrice: 10m,
                    UnitPrice: 15m,
                    Fees: 0m,
                    DaysHeld: 10
                )
            )
        );
    }

    [Fact]
    public void SimulateRedemption_LciIsFullyExempt()
    {
        var breakdown = TaxCalculators.SimulateRedemption(
            new RedemptionInput(
                AssetClass: TaxAssetClasses.RendaFixaIsenta,
                QuantityRedeemed: 10_000m,
                AvailableQuantity: 10_000m,
                AveragePrice: 1m,
                UnitPrice: 1.2m,
                Fees: 0m,
                DaysHeld: 100
            )
        );

        Assert.True(breakdown.ExemptApplied);
        Assert.Equal(0m, breakdown.IrAmount);
        Assert.Equal(2000m, breakdown.Profit);
        Assert.Equal(2000m, breakdown.NetAmount);
        Assert.Contains(
            "isenção total",
            breakdown.ExemptReason,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void SimulateRedemption_FixedIncome_CreditsComeCotasAgainstIr()
    {
        // Compra em 05/01/2026; rendimento de R$ 200 creditado em 10/06/2026.
        // Come-cotas do semestre (30/11/2026): 15% × 200 = 30 → abate do IR devido.
        var datedYields = new List<(DateOnly, decimal)> { (new(2026, 6, 10), 200m) };

        var breakdown = TaxCalculators.SimulateRedemption(
            new RedemptionInput(
                AssetClass: TaxAssetClasses.RendaFixa,
                QuantityRedeemed: 1000m,
                AvailableQuantity: 1000m,
                AveragePrice: 1m,
                UnitPrice: 2m,
                Fees: 0m,
                DaysHeld: 709, // 05/01/2025 → 15/12/2026 ⇒ faixa de 17,5% (>360 e ≤720)
                ReferenceDate: new DateOnly(2026, 12, 15),
                DatedYields: datedYields
            )
        );

        Assert.Equal(17.5m, breakdown.IrPercent);
        Assert.Equal(30m, breakdown.ComeCotasAlreadyPaid); // antecipação já paga
        Assert.Equal(145m, breakdown.IrAmount); // 175 da tabela − 30 de come-cotas
        Assert.Equal(0m, breakdown.IofAmount); // > 30 dias
        Assert.Equal(855m, breakdown.NetAmount); // lucro 1000 − IR 145
        Assert.Contains(breakdown.Premises, p => p.Contains("Come-cotas"));
    }

    // ---------- come-cotas semestral ----------

    private static List<(DateOnly, decimal)> MonthlyYields(int year, int fromMonth, int toMonth) =>
        Enumerable
            .Range(fromMonth, toMonth - fromMonth + 1)
            .Select(m => (new DateOnly(year, m, 10), 100m))
            .ToList();

    [Fact]
    public void ComeCotas_ChargesOnlyUpToReferenceDate()
    {
        var yields = MonthlyYields(2026, 1, 4);

        Assert.Equal(0m, TaxCalculators.SumComeCotasPaid(yields, new DateOnly(2026, 4, 30)));
    }

    [Fact]
    public void ComeCotas_MayEvent_TakesSemesterYieldSinceJanuary()
    {
        var yields = MonthlyYields(2026, 1, 5);

        // Evento: último dia útil de maio/2026 = sexta, 29/05. Rende Jan–Mai = 500 → 15% = 75.
        Assert.Equal(75m, TaxCalculators.SumComeCotasPaid(yields, new DateOnly(2026, 5, 29)));
    }

    [Fact]
    public void ComeCotas_NovemberEvent_ExcludesAlreadyChargedSemester()
    {
        var yields = MonthlyYields(2026, 1, 11);

        // Maio cobra 75 (Jan–Mai). Novembro (seg, 30/11) cobra 15% × Jun–Nov = 600 → 90. Total 165.
        Assert.Equal(165m, TaxCalculators.SumComeCotasPaid(yields, new DateOnly(2026, 11, 30)));
    }

    [Fact]
    public void ComeCotas_YieldAfterReference_IsIgnored()
    {
        var yields = MonthlyYields(2026, 1, 11);

        // Referência em julho: só o evento de maio passou — dezembro ainda não incide.
        Assert.Equal(75m, TaxCalculators.SumComeCotasPaid(yields, new DateOnly(2026, 7, 1)));
    }

    // ---------- datas fiscais ----------

    [Fact]
    public void DarfDueDate_IsLastBusinessDayOfFollowingMonth()
    {
        // Setembro/2026 termina em quarta-feira, 30/09.
        Assert.Equal(new DateOnly(2026, 9, 30), TaxCalculators.DarfDueDate(2026, 8));
    }

    [Fact]
    public void DarfDueDate_DecemberRollsToJanuary()
    {
        // 31/01/2027 é domingo ⇒ vencimento cai para sexta, 29/01.
        Assert.Equal(new DateOnly(2027, 1, 29), TaxCalculators.DarfDueDate(2026, 12));
    }

    // ---------- projeção DARF: isenções ações vs ETF vs FII ----------

    private static DarfProjection Project(string assetClass, decimal gross, decimal pnl) =>
        TaxCalculators.ProjectDarf(2026, 8, [new DarfPositionInput(assetClass, pnl, gross)]);

    [Fact]
    public void Projection_StockSalesUnder20k_AreExempt()
    {
        var projection = Project(TaxAssetClasses.Stock, 18_000m, 2_000m);
        var item = projection.Items.Single();

        Assert.Equal(0m, item.TaxDue);
        Assert.Empty(item.DarfCode);
        Assert.Contains(projection.Premises, p => p.Contains("limite de isenção"));
    }

    [Fact]
    public void Projection_StockSalesOver20k_PaySwingIrWithCode6015()
    {
        var projection = Project(TaxAssetClasses.Stock, 25_000m, 5_000m);
        var item = projection.Items.Single();

        Assert.Equal(750m, item.TaxDue); // 15% × 5.000
        Assert.Equal("6015", item.DarfCode);
        Assert.Equal(new DateOnly(2026, 9, 30), item.DueDate);
    }

    [Fact]
    public void Projection_EtfNeverGetsThe20kExemption()
    {
        // Mesmo volume isento de ações (18 mil) não protege ETF: 15% sobre todo o lucro.
        var projection = Project(TaxAssetClasses.Etf, 18_000m, 2_000m);
        var item = projection.Items.Single();

        Assert.Equal(300m, item.TaxDue);
        Assert.Equal("6015", item.DarfCode);
        Assert.Contains(projection.Premises, p => p.Contains("NÃO há isenção"));
    }

    [Fact]
    public void Projection_BdrEtfNeverGetsThe20kExemption()
    {
        var projection = Project(TaxAssetClasses.BdrEtf, 10_000m, 1_000m);

        Assert.Equal(150m, projection.Items.Single().TaxDue);
    }

    [Fact]
    public void Projection_FiiSalesUnder35k_AreExempt()
    {
        var projection = Project(TaxAssetClasses.Fii, 30_000m, 3_000m);
        var item = projection.Items.Single();

        Assert.Equal(0m, item.TaxDue);
        Assert.Contains(
            projection.Premises,
            p => p.Contains("35.000,00") || p.Contains("35,000.00")
        );
    }

    [Fact]
    public void Projection_FiiSalesOver35k_PayOnWholeProfit()
    {
        var projection = Project(TaxAssetClasses.Fii, 40_000m, 3_000m);
        var item = projection.Items.Single();

        Assert.Equal(450m, item.TaxDue); // 15% sobre TODO o lucro (premissa registrada)
        Assert.Equal("6015", item.DarfCode);
    }

    [Fact]
    public void Projection_LciClassPaysNothing()
    {
        var projection = Project(TaxAssetClasses.RendaFixaIsenta, 50_000m, 5_000m);

        Assert.Equal(0m, projection.Items.Single().TaxDue);
    }

    [Fact]
    public void Projection_FixedIncomeUsesRegressiveRatePerLot()
    {
        var projection = TaxCalculators.ProjectDarf(
            2026,
            8,
            [
                new DarfPositionInput(TaxAssetClasses.RendaFixa, 1_000m, 21_000m, DaysHeld: 90), // 22,5% → 225
                new DarfPositionInput(TaxAssetClasses.RendaFixa, 2_000m, 22_000m, DaysHeld: 800), // 15% → 300
            ]
        );
        var item = projection.Items.Single();

        Assert.Equal(525m, item.TaxDue);
        Assert.Empty(item.DarfCode); // DARF 6015 é exclusivo de renda variável nesta camada
    }

    [Fact]
    public void Projection_LossMonthHasNoTax()
    {
        var projection = Project(TaxAssetClasses.Stock, 25_000m, -1_000m);
        var item = projection.Items.Single();

        Assert.Equal(-1_000m, item.RealizedPnl);
        Assert.Equal(0m, item.TaxDue);
        Assert.Empty(item.DarfCode);
    }

    [Fact]
    public void Projection_EmptyMonth_ReturnsNoItemsButKeepsDisclaimer()
    {
        var projection = TaxCalculators.ProjectDarf(2026, 8, []);

        Assert.Empty(projection.Items);
        Assert.Equal(TaxCalculators.Disclaimer, projection.Disclaimer);
    }

    [Fact]
    public void Projection_InvalidMonth_Throws()
    {
        Assert.Throws<ArgumentException>(() => TaxCalculators.ProjectDarf(2026, 13, []));
    }
}
