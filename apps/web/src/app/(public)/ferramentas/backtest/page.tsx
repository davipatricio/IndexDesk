'use client';

import * as React from 'react';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { formatCurrencyBRL, formatPercent } from '@/lib/utils';
import { BarChart3, DollarSign, Calendar, Percent, ShieldCheck } from 'lucide-react';

export default function BacktestPage() {
  const [initialCapital, setInitialCapital] = React.useState(10000);
  const [monthlyContribution, setMonthlyContribution] = React.useState(1000);
  const [annualRatePercent, setAnnualRatePercent] = React.useState(13.0);
  const [years, setYears] = React.useState(5);

  const simulation = React.useMemo(() => {
    const totalMonths = Math.max(1, years * 12);
    const nominalRate = Math.max(0, annualRatePercent) / 100;
    // Monthly compounding rate from annual rate: (1 + i)^(1/12) - 1
    const monthlyRate = Math.pow(1 + nominalRate, 1 / 12) - 1;

    let capital = initialCapital;
    const totalInvested = initialCapital + monthlyContribution * totalMonths;

    for (let i = 1; i <= totalMonths; i++) {
      capital += monthlyContribution;
      capital *= 1 + monthlyRate;
    }

    const totalReturn = capital - totalInvested;
    const totalReturnPercent = totalInvested > 0 ? (totalReturn / totalInvested) * 100 : 0;
    // Realized CAGR over the full period:
    const cagr =
      totalInvested > 0 && years > 0 ? (Math.pow(capital / totalInvested, 1 / years) - 1) * 100 : 0;

    return {
      finalCapital: Math.round(capital),
      totalInvested,
      totalReturn: Math.round(totalReturn),
      totalReturnPercent: Number(totalReturnPercent.toFixed(2)),
      cagr: Number(cagr.toFixed(2)),
      totalMonths,
    };
  }, [initialCapital, monthlyContribution, annualRatePercent, years]);

  return (
    <div className="container mx-auto px-4 py-8 flex flex-col gap-6 max-w-5xl">
      <div className="flex flex-col gap-2">
        <div className="flex items-center gap-2">
          <Badge variant="outline" className="text-xs">
            Simulador de Carteiras
          </Badge>
          <span className="text-xs text-muted-foreground">
            Aportes Periódicos & Juros Compostos
          </span>
        </div>
        <h1 className="text-2xl sm:text-3xl font-bold tracking-tight flex items-center gap-2.5 text-foreground">
          <BarChart3 className="size-6 text-primary" />
          Simulador de Projeção Patrimonial
        </h1>
        <p className="text-muted-foreground text-sm">
          Calcule o crescimento estimado da sua carteira com aportes mensais recorrentes e
          reinvestimento com base na taxa de retorno definida.
        </p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Form Controls */}
        <Card className="lg:col-span-1">
          <CardHeader>
            <CardTitle className="text-xs font-semibold uppercase text-muted-foreground">
              Parâmetros da Projeção
            </CardTitle>
            <CardDescription className="text-xs">
              Defina o capital, aportes e taxa estimada:
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-4">
            <div className="flex flex-col gap-1.5">
              <label
                htmlFor="initial-capital"
                className="text-xs font-medium flex items-center gap-1 text-foreground"
              >
                <DollarSign className="size-3.5 text-primary" />
                Capital Inicial (R$)
              </label>
              <Input
                id="initial-capital"
                type="number"
                value={initialCapital}
                onChange={(e) => setInitialCapital(Number(e.target.value) || 0)}
                className="font-mono text-sm"
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <label
                htmlFor="monthly-contribution"
                className="text-xs font-medium flex items-center gap-1 text-foreground"
              >
                <DollarSign className="size-3.5 text-primary" />
                Aporte Mensal Recorrente (R$)
              </label>
              <Input
                id="monthly-contribution"
                type="number"
                value={monthlyContribution}
                onChange={(e) => setMonthlyContribution(Number(e.target.value) || 0)}
                className="font-mono text-sm"
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <label
                htmlFor="annual-rate"
                className="text-xs font-medium flex items-center gap-1 text-foreground"
              >
                <Percent className="size-3.5 text-primary" />
                Taxa de Retorno Estimada (% a.a.)
              </label>
              <Input
                id="annual-rate"
                type="number"
                step="0.1"
                value={annualRatePercent}
                onChange={(e) => setAnnualRatePercent(Number(e.target.value) || 0)}
                className="font-mono text-sm"
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <label
                htmlFor="simulation-years"
                className="text-xs font-medium flex items-center gap-1 text-foreground"
              >
                <Calendar className="size-3.5 text-primary" />
                Prazo em Anos
              </label>
              <Input
                id="simulation-years"
                type="number"
                min={1}
                max={40}
                value={years}
                onChange={(e) => setYears(Number(e.target.value) || 1)}
                className="font-mono text-sm"
              />
            </div>

            <div className="pt-2">
              <div className="text-xs font-semibold mb-2 text-foreground">Exemplo de Alocação:</div>
              <div className="flex flex-col gap-1.5 text-xs text-muted-foreground">
                <div className="flex justify-between p-2 rounded bg-muted/40 font-mono">
                  <span>IVVB11 (S&P 500)</span>
                  <span className="font-semibold text-foreground">50%</span>
                </div>
                <div className="flex justify-between p-2 rounded bg-muted/40 font-mono">
                  <span>B5P211 (IMA-B 5 Renda Fixa)</span>
                  <span className="font-semibold text-foreground">30%</span>
                </div>
                <div className="flex justify-between p-2 rounded bg-muted/40 font-mono">
                  <span>SMAL11 (Small Caps B3)</span>
                  <span className="font-semibold text-foreground">20%</span>
                </div>
              </div>
            </div>
          </CardContent>
        </Card>

        {/* Results Overview */}
        <div className="lg:col-span-2 flex flex-col gap-6">
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <Card variant="accent">
              <CardHeader className="pb-2">
                <CardTitle className="text-xs font-semibold uppercase text-primary">
                  Patrimônio Final Estimado
                </CardTitle>
              </CardHeader>
              <CardContent>
                <div className="text-3xl font-bold font-mono text-primary">
                  {formatCurrencyBRL(simulation.finalCapital)}
                </div>
                <p className="text-xs text-muted-foreground mt-1">
                  Ganho gerado de {formatCurrencyBRL(simulation.totalReturn)} (
                  {formatPercent(simulation.totalReturnPercent)})
                </p>
              </CardContent>
            </Card>

            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-xs font-semibold uppercase text-muted-foreground">
                  Total em Aportes
                </CardTitle>
              </CardHeader>
              <CardContent>
                <div className="text-3xl font-bold font-mono text-foreground">
                  {formatCurrencyBRL(simulation.totalInvested)}
                </div>
                <p className="text-xs text-muted-foreground mt-1">
                  {simulation.totalMonths} aportes de {formatCurrencyBRL(monthlyContribution)} +
                  capital inicial
                </p>
              </CardContent>
            </Card>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <Card>
              <CardHeader className="pb-1">
                <CardTitle className="text-xs uppercase text-muted-foreground font-semibold">
                  Taxa Anual Adotada
                </CardTitle>
              </CardHeader>
              <CardContent>
                <div className="text-xl font-bold font-mono text-foreground">
                  {formatPercent(annualRatePercent)} a.a.
                </div>
                <p className="text-xs text-muted-foreground mt-0.5">
                  Taxa nominal composta configurada no painel
                </p>
              </CardContent>
            </Card>

            <Card>
              <CardHeader className="pb-1">
                <CardTitle className="text-xs uppercase text-muted-foreground font-semibold">
                  Ganho sobre o Capital
                </CardTitle>
              </CardHeader>
              <CardContent>
                <div className="text-xl font-bold font-mono text-positive">
                  {formatPercent(simulation.totalReturnPercent)}
                </div>
                <p className="text-xs text-muted-foreground mt-0.5">
                  Relação entre rendimento e aportes totais
                </p>
              </CardContent>
            </Card>
          </div>

          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-xs uppercase text-muted-foreground font-semibold flex items-center gap-1.5">
                <ShieldCheck className="size-3.5 text-primary" />
                Nota Metodológica
              </CardTitle>
            </CardHeader>
            <CardContent className="text-xs text-muted-foreground leading-relaxed">
              Esta simulação aplica juros compostos determinísticos considerando aportes mensais no
              início de cada período e reinvestimento integral. Valores apresentados têm finalidade
              exclusivamente ilustrativa e não constituem garantia de rentabilidade futura.
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}
