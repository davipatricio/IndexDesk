using IndexDesk.Modules.Portfolio.Calculators;
using Xunit;

namespace IndexDesk.UnitTests.Portfolio;

public class GoalProjectionCalculatorTests
{
    // ---------- RunRateMonths ----------

    [Fact]
    public void Run_rate_dobra_em_aproximadamente_70_meses_a_1_porcento()
    {
        // ln(2)/ln(1,01) ≈ 69,66 → teto = 70 meses
        var months = GoalProjectionCalculator.RunRateMonths(1000m, 2000m, 1m);

        Assert.Equal(70, months);
    }

    [Fact]
    public void Run_rate_retorna_null_quando_retorno_nao_positivo()
    {
        Assert.Null(GoalProjectionCalculator.RunRateMonths(1000m, 2000m, 0m));
        Assert.Null(GoalProjectionCalculator.RunRateMonths(1000m, 2000m, -0.5m));
    }

    [Fact]
    public void Run_rate_zero_meses_quando_meta_ja_atingida()
    {
        Assert.Equal(0, GoalProjectionCalculator.RunRateMonths(1500m, 1000m, 1m));
        Assert.Equal(0, GoalProjectionCalculator.RunRateMonths(1000m, 1000m, 1m));
    }

    [Fact]
    public void Run_rate_null_sem_base_para_compoundar()
    {
        Assert.Null(GoalProjectionCalculator.RunRateMonths(0m, 1000m, 1m));
    }

    // ---------- CompoundMonths ----------

    [Fact]
    public void Sem_juros_alvo_vem_somente_dos_aportes()
    {
        // R$ 1.000/mês a 0% a.a. → 12.000 leva exatamente 12 meses
        var months = GoalProjectionCalculator.CompoundMonths(0m, 1000m, 0m, 12000m);

        Assert.Equal(12, months);
    }

    [Fact]
    public void Juros_aceleram_a_conquista_da_meta()
    {
        // Aportes de 1.000 até 11.500: sem juros = 12 meses; a 12% a.a. (1% a.m.) = 11 meses.
        var semJuros = GoalProjectionCalculator.CompoundMonths(0m, 1000m, 0m, 11500m);
        var comJuros = GoalProjectionCalculator.CompoundMonths(0m, 1000m, 12m, 11500m);

        Assert.Equal(12, semJuros);
        Assert.Equal(11, comJuros);
        Assert.True(comJuros < semJuros);
    }

    [Fact]
    public void Capital_so_com_juros_composto_conhecido()
    {
        // 10.000 a 1% a.m.: 1,01^m ≥ 1,10 → ~9,65 → teto = 10 meses
        var months = GoalProjectionCalculator.CompoundMonths(10000m, 0m, 12m, 11000m);

        Assert.Equal(10, months);
    }

    [Fact]
    public void Inatingivel_sem_crescimento_retorna_null()
    {
        // 100 parado, sem aporte, 0% a.a.: nunca alcança 101 mesmo iterando até o cap
        var months = GoalProjectionCalculator.CompoundMonths(100m, 0m, 0m, 101m);

        Assert.Null(months);
    }

    [Fact]
    public void Respeita_cap_de_1200_meses()
    {
        // 1/mês a 0% levaria 1.000.000 meses — estoura o cap de MaxMonths → null
        var months = GoalProjectionCalculator.CompoundMonths(0m, 1m, 0m, 1_000_000m);

        Assert.Null(months);
    }

    [Fact]
    public void Zero_meses_quando_principal_ja_cobre_a_meta()
    {
        Assert.Equal(0, GoalProjectionCalculator.CompoundMonths(5000m, 100m, 6m, 4000m));
    }
}
