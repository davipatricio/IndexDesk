namespace IndexDesk.Modules.Portfolio.Calculators;

/// <summary>
/// Classes fiscais estáveis usadas pela camada de impostos. Derivam de
/// <c>Assets.AssetType</c> (STOCK/ETF/BDR_ETF/FII/INDEX) ou do índice sintético
/// da transação (CDI/SELIC → RF; LCI/LCA/CRI/CRA → RF isenta).
/// </summary>
public static class TaxAssetClasses
{
    /// <summary>Ações — vendas PF até R$ 20.000/mês isentas (swing).</summary>
    public const string Stock = "STOCK";

    /// <summary>ETF listado na B3 — NÃO usa a isenção de R$ 20 mil.</summary>
    public const string Etf = "ETF";

    /// <summary>BDR-ETF — NÃO usa a isenção de R$ 20 mil.</summary>
    public const string BdrEtf = "BDR_ETF";

    /// <summary>FII — mercado secundário isento até R$ 35.000/mês (PF).</summary>
    public const string Fii = "FII";

    /// <summary>Índices e tipos futuros: tratados como renda variável (premissa registrada).</summary>
    public const string Index = "INDEX";

    /// <summary>Renda fixa tributável (CDI/Selic sintéticos e similares) — tabela regressiva + IOF.</summary>
    public const string RendaFixa = "RF";

    /// <summary>LCI/LCA/CRI/CRA — totalmente isentos de IR para pessoa física.</summary>
    public const string RendaFixaIsenta = "RF_ISENTA";
}

/// <summary>
/// Resultado educacional da simulação de resgate/venda. Todos os valores em BRL;
/// arredondamentos bancários a 2 casas por valor monetário.
/// </summary>
public sealed record RedemptionBreakdown(
    decimal GrossAmount,
    decimal CostBasis,
    decimal Profit,
    decimal IrPercent,
    decimal IrAmount,
    decimal IofAmount,
    decimal NetAmount,
    bool ExemptApplied,
    string? ExemptReason,
    decimal ComeCotasAlreadyPaid,
    IReadOnlyList<string> Premises
);

/// <summary>
/// Entrada da simulação: classe fiscal + quantities/preços do snapshot da posição.
/// <see cref="DaysHeld"/> só é relevante para classes RF; <see cref="DatedYields"/>
/// (rendimentos datados) alimenta o cálculo de come-cotas já pago.
/// </summary>
public sealed record RedemptionInput(
    string AssetClass,
    decimal QuantityRedeemed,
    decimal AvailableQuantity,
    decimal AveragePrice,
    decimal UnitPrice,
    decimal Fees,
    int DaysHeld,
    DateOnly? ReferenceDate = null,
    IReadOnlyList<(DateOnly Date, decimal Yield)>? DatedYields = null
);

/// <summary>
/// Um lançamento de venda/resgate realizado no mês (granularidade de transação).
/// O agrupamento por classe e as isenções mensais são aplicados dentro de ProjectDarf.
/// </summary>
public sealed record DarfPositionInput(
    string AssetClass,
    decimal RealizedPnl,
    decimal GrossSaleAmount,
    int DaysHeld = 0
);

/// <summary>Total fiscal de uma classe de ativo no mês.</summary>
public sealed record DarfProjectionItem(
    string AssetClass,
    decimal RealizedPnl,
    decimal TaxDue,
    string DarfCode,
    DateOnly DueDate
);

/// <summary>Projeção DARF do mês com premissas e disclaimer educacional.</summary>
public sealed record DarfProjection(
    int Year,
    int Month,
    IReadOnlyList<DarfProjectionItem> Items,
    IReadOnlyList<string> Premises,
    string Disclaimer
);

/// <summary>
/// Calculadoras fiscais puras (sem I/O) do módulo Portfolio — camada educacional do
/// imposto brasileiro para investidor pessoa física. Premissas simplificadoras ficam
/// sempre explícitas nas listas retornadas; nada aqui substitui orientação profissional.
/// </summary>
public static class TaxCalculators
{
    public const string Disclaimer =
        "Cálculo educacional com premissas simplificadas; não substitui orientação fiscal profissional.";

    /// <summary>Código DARF para ganhos em renda variável (operações comuns/swing).</summary>
    public const string DarfSwingVariavelCode = "6015";

    /// <summary>Alíquota fixa de IR para renda variável em operações comuns (não day trade).</summary>
    public const decimal SwingIrPercent = 15m;

    /// <summary>Come-cotas: antecipação de 15% do rendimento do semestre (último dia útil de maio/novembro).</summary>
    public const decimal ComeCotasPercent = 15m;

    /// <summary>Isenção mensal PF para vendas de ações.</summary>
    public const decimal MonthlyExemptionStock = 20_000m;

    /// <summary>Isenção mensal PF para vendas de FII no mercado secundário.</summary>
    public const decimal MonthlyExemptionFii = 35_000m;

    // ---------- alíquotas ----------

    /// <summary>
    /// Tabela regressiva de IR sobre renda fixa pelo prazo detido (dias corridos),
    /// com fronteiras diárias inclusivas: ≤180 → 22,5% · ≤360 → 20% · ≤720 → 17,5% · >720 → 15%.
    /// </summary>
    public static decimal RegressiveIrPercent(int daysHeld) =>
        daysHeld <= 180 ? 22.5m
        : daysHeld <= 360 ? 20m
        : daysHeld <= 720 ? 17.5m
        : 15m;

    /// <summary>
    /// Alíquota de IOF para resgates de renda fixa antes de 30 dias:
    /// IR_do_prazo × (1 − dias/30), arredondada a 4 casas; dia 0 cobra o IR cheio; ≥30 dias zera.
    /// </summary>
    public static decimal IofPercent(int daysHeld) =>
        daysHeld >= 30
            ? 0m
            : decimal.Round(RegressiveIrPercent(daysHeld) * (1m - daysHeld / 30m), 4);

    public static bool IsFullyExemptClass(string assetClass) =>
        assetClass == TaxAssetClasses.RendaFixaIsenta;

    /// <summary>Limite mensal de isenção PF por classe (0 = sem isenção por volume).</summary>
    public static decimal MonthlyExemptionFor(string assetClass) =>
        assetClass switch
        {
            TaxAssetClasses.Stock => MonthlyExemptionStock,
            TaxAssetClasses.Fii => MonthlyExemptionFii,
            _ => 0m,
        };

    // ---------- come-cotas ----------

    /// <summary>Data do último dia útil do mês (só finais de semana; feriados não considerados — limitação documentada).</summary>
    public static DateOnly LastBusinessDayOfMonth(int year, int month)
    {
        var date = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            date = date.AddDays(-1);
        return date;
    }

    /// <summary>Eventos de come-cotas (último dia útil de maio e novembro) dentro de [start, end], ordenados.</summary>
    public static List<DateOnly> ComeCotasEventsBetween(DateOnly start, DateOnly end)
    {
        var events = new List<DateOnly>();
        for (var year = start.Year; year <= end.Year; year++)
        {
            var may = LastBusinessDayOfMonth(year, 5);
            var nov = LastBusinessDayOfMonth(year, 11);
            if (may >= start && may <= end)
                events.Add(may);
            if (nov >= start && nov <= end)
                events.Add(nov);
        }
        return events.OrderBy(d => d).ToList();
    }

    /// <summary>
    /// Soma o come-cotas já pago: a cada evento (último dia útil de maio/novembro) anterior ou igual
    /// à data de referência, antecipa 15% do rendimento acumulado no semestre corrente.
    /// Rendimentos após a referência são ignorados.
    /// </summary>
    public static decimal SumComeCotasPaid(
        IReadOnlyList<(DateOnly Date, decimal Yield)> datedYields,
        DateOnly referenceDate,
        decimal percent = ComeCotasPercent
    )
    {
        if (datedYields.Count == 0)
            return 0m;

        var yields = datedYields.Where(y => y.Date <= referenceDate).OrderBy(y => y.Date).ToList();
        if (yields.Count == 0)
            return 0m;

        var total = 0m;
        var previousEvent = DateOnly.MinValue;
        foreach (var @event in ComeCotasEventsBetween(yields[0].Date, referenceDate))
        {
            var semesterYield = yields
                .Where(y => y.Date > previousEvent && y.Date <= @event)
                .Sum(y => y.Yield);
            total += Round2(semesterYield * percent / 100m);
            previousEvent = @event;
        }
        return total;
    }

    // ---------- datas fiscais ----------

    /// <summary>Vencimento do DARF: último dia útil do mês seguinte ao da apuração.</summary>
    public static DateOnly DarfDueDate(int year, int month)
    {
        var nextMonth =
            month == 12 ? new DateOnly(year + 1, 1, 1) : new DateOnly(year, month + 1, 1);
        return LastBusinessDayOfMonth(nextMonth.Year, nextMonth.Month);
    }

    // ---------- simulação de resgate ----------

    /// <summary>
    /// Simula resgate/venda (total ou parcial por quantidade) e devolve o breakdown fiscal
    /// completo com premissas. RF usa tabela regressiva + IOF + crédito de come-cotas;
    /// classes de renda variável usam 15% sobre o lucro (isenções mensais avaliadas na projeção DARF).
    /// </summary>
    public static RedemptionBreakdown SimulateRedemption(RedemptionInput input)
    {
        Validate(input);

        var grossAmount = Round2(input.QuantityRedeemed * input.UnitPrice);
        var costBasis = Round2(input.QuantityRedeemed * input.AveragePrice);
        var fees = Round2(input.Fees);
        var profit = Round2(grossAmount - costBasis - fees);
        var taxableProfit = profit > 0 ? profit : 0m;

        var premises = new List<string>
        {
            $"Classe fiscal '{input.AssetClass}': {input.QuantityRedeemed:N8} cotas resgatadas de {input.AvailableQuantity:N8} ao preço unitário informado.",
            "Valores calculados sobre o resultado positivo (lucro); prejuízo não gera imposto nesta simulação.",
        };

        if (IsFullyExemptClass(input.AssetClass))
        {
            premises.Add(
                "LCI/LCA/CRI/CRA são totalmente isentos de IR para pessoa física — nenhum imposto incide."
            );
            return new RedemptionBreakdown(
                grossAmount,
                costBasis,
                profit,
                0m,
                0m,
                0m,
                profit,
                true,
                "LCI/LCA/CRI/CRA: isenção total de IR para PF.",
                0m,
                premises
            );
        }

        var isFixedIncome =
            input.AssetClass == TaxAssetClasses.RendaFixa
            || input.AssetClass == TaxAssetClasses.RendaFixaIsenta;

        decimal irPercent;
        decimal irDue;
        decimal iofAmount = 0m;
        var comeCotasAlreadyPaid = 0m;

        if (isFixedIncome)
        {
            irPercent = RegressiveIrPercent(input.DaysHeld);
            var irTable = Round2(taxableProfit * irPercent / 100m);
            var iofPercent = IofPercent(input.DaysHeld);
            iofAmount = Round2(taxableProfit * iofPercent / 100m);

            if (input.DatedYields is not null && input.ReferenceDate is not null)
                comeCotasAlreadyPaid = SumComeCotasPaid(
                    input.DatedYields,
                    input.ReferenceDate.Value
                );

            irDue = irTable > comeCotasAlreadyPaid ? Round2(irTable - comeCotasAlreadyPaid) : 0m;

            premises.Insert(
                1,
                $"Tabela regressiva aplicada ao prazo de {input.DaysHeld} dias corridos: {irPercent}% de IR."
            );
            premises.Add(
                iofPercent > 0
                    ? $"IOF regressivo de {iofPercent}% (resgate com {input.DaysHeld} dia(s): IR × (1 − dias/30))."
                    : $"Sem IOF: resgate com {input.DaysHeld} dia(s) atingiu o prazo mínimo de 30 dias."
            );
            if (comeCotasAlreadyPaid > 0)
                premises.Add(
                    $"Come-cotas semestral já retido (R$ {comeCotasAlreadyPaid:N2}) abatido do IR devido, pois é antecipação do mesmo imposto."
                );
        }
        else
        {
            irPercent = SwingIrPercent;
            irDue = Round2(taxableProfit * SwingIrPercent / 100m);
            premises.Add(
                $"Renda variável (operações comuns/swing): alíquota fixa de {SwingIrPercent}% sobre o lucro."
            );
            premises.Add(
                input.AssetClass == TaxAssetClasses.Stock
                    ? $"Vendas de ações até R$ {MonthlyExemptionStock:N2}/mês são isentas — avaliado no agregado do mês (Projeção DARF); a regra NÃO vale para ETF nem BDR-ETF."
                : input.AssetClass == TaxAssetClasses.Fii
                    ? $"Vendas de FII no mercado secundário até R$ {MonthlyExemptionFii:N2}/mês são isentas — avaliado na Projeção DARF; rendimentos mensais de FII seguem regra própria fora desta simulação."
                : $"{input.AssetClass} não tem isenção de volume mensal: os {SwingIrPercent}% incidem sobre todo lucro em swing."
            );
        }

        var netAmount = Round2(profit - irDue - iofAmount);

        return new RedemptionBreakdown(
            grossAmount,
            costBasis,
            profit,
            irPercent,
            irDue,
            iofAmount,
            netAmount,
            false,
            null,
            comeCotasAlreadyPaid,
            premises
        );
    }

    // ---------- projeção mensal DARF ----------

    /// <summary>
    /// Agrupa as vendas realizadas no mês por classe fiscal, aplica as isenções mensais PF
    /// (ações R$ 20 mil; FII secundário R$ 35 mil; LCI/LCA/CRI/CRA sempre isentos), calcula o
    /// imposto devido e marca código DARF 6015 + vencimento (último dia útil do mês seguinte).
    /// </summary>
    public static DarfProjection ProjectDarf(
        int year,
        int month,
        IReadOnlyList<DarfPositionInput> positions
    )
    {
        if (month is < 1 or > 12)
            throw new ArgumentException("Mês deve estar entre 1 e 12.", nameof(month));
        if (year is < 1900 or > 2200)
            throw new ArgumentException("Ano fora do intervalo suportado.", nameof(year));

        var dueDate = DarfDueDate(year, month);
        var items = new List<DarfProjectionItem>();
        var premises = new List<string>
        {
            $"Apuração de {year:D4}-{month:D2}: vendas/resgates realizados no mês, dados locais da carteira.",
            $"Vencimento do DARF apurado: último dia útil do mês seguinte ({dueDate:yyyy-MM-dd}).",
            "Prejuízos não são compensados com outros meses/ativos nesta projeção educacional.",
        };

        foreach (
            var group in positions
                .GroupBy(p => p.AssetClass)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
        )
        {
            var assetClass = group.Key;
            var realizedPnl = Round2(group.Sum(p => p.RealizedPnl));
            var grossSales = Round2(group.Sum(p => p.GrossSaleAmount));
            decimal taxDue;

            if (IsFullyExemptClass(assetClass))
            {
                taxDue = 0m;
                premises.Add(
                    $"{assetClass}: LCI/LCA/CRI/CRA são isentos de IR para PF — nada a recolher."
                );
            }
            else if (assetClass == TaxAssetClasses.RendaFixa)
            {
                // RF é tributado por posição/prazo: aplica a regressiva lançamento a lançamento.
                taxDue = Round2(
                    group.Sum(p =>
                        p.RealizedPnl > 0
                            ? p.RealizedPnl * RegressiveIrPercent(p.DaysHeld) / 100m
                            : 0m
                    )
                );
                premises.Add(
                    $"{assetClass}: tabela regressiva (≤180d 22,5% · ≤360d 20% · ≤720d 17,5% · >720d 15%) aplicada lançamento a lançamento; IOF e come-cotas ficam fora desta projeção mensal."
                );
            }
            else
            {
                var exemptionLimit = MonthlyExemptionFor(assetClass);
                if (realizedPnl <= 0)
                {
                    taxDue = 0m;
                    premises.Add(
                        $"{assetClass}: resultado não positivo no mês — nenhum imposto devido."
                    );
                }
                else if (exemptionLimit > 0 && grossSales <= exemptionLimit)
                {
                    taxDue = 0m;
                    premises.Add(
                        $"{assetClass}: vendas brutas de R$ {grossSales:N2} ≤ limite de isenção mensal de R$ {exemptionLimit:N2} — lucro isento."
                    );
                }
                else
                {
                    taxDue = Round2(realizedPnl * SwingIrPercent / 100m);
                    premises.Add(
                        assetClass == TaxAssetClasses.Etf || assetClass == TaxAssetClasses.BdrEtf
                            ? $"{assetClass}: NÃO há isenção de R$ 20 mil para ETF/BDR-ETF — {SwingIrPercent}% sobre o lucro de R$ {realizedPnl:N2}."
                        : exemptionLimit > 0
                            ? $"{assetClass}: vendas brutas de R$ {grossSales:N2} ultrapassaram o limite isento de R$ {exemptionLimit:N2} — {SwingIrPercent}% sobre todo o lucro do mês (premissa simplificadora)."
                        : $"{assetClass}: {SwingIrPercent}% sobre o lucro realizado de R$ {realizedPnl:N2}."
                    );
                }
            }

            var isSwingVariable = taxDue > 0 && assetClass != TaxAssetClasses.RendaFixa;
            items.Add(
                new DarfProjectionItem(
                    assetClass,
                    realizedPnl,
                    taxDue,
                    isSwingVariable ? DarfSwingVariavelCode : string.Empty,
                    dueDate
                )
            );
        }

        return new DarfProjection(year, month, items, premises, Disclaimer);
    }

    // ---------- helpers ----------

    private static decimal Round2(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static void Validate(RedemptionInput input)
    {
        if (string.IsNullOrWhiteSpace(input.AssetClass))
            throw new ArgumentException("Classe fiscal obrigatória.", nameof(input));
        if (input.QuantityRedeemed <= 0)
            throw new ArgumentException("Quantidade resgatada deve ser positiva.", nameof(input));
        if (input.QuantityRedeemed > input.AvailableQuantity)
            throw new ArgumentException(
                "Quantidade resgatada maior que a posição disponível.",
                nameof(input)
            );
        if (input.AveragePrice < 0 || input.UnitPrice < 0 || input.Fees < 0)
            throw new ArgumentException("Preços e taxas não podem ser negativos.", nameof(input));
    }
}
