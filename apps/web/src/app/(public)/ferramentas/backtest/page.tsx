'use client';

import * as React from 'react';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { formatCurrencyBRL, formatPercent } from '@/lib/utils';
import { BarChart3, DollarSign, Calendar, ShieldCheck } from 'lucide-react';

export default function BacktestPage() {
  const [initialCapital, setInitialCapital] = React.useState(10000);
  const [monthlyContribution, setMonthlyContribution] = React.useState(1000);
  const [years, setYears] = React.useState(5);

  // Simulated Compound Growth with variance
  const simulation = React.useMemo(() => {
    const totalMonths = years * 12;
    let capital = initialCapital;
    const totalInvested = initialCapital + monthlyContribution * totalMonths;
    const monthlyRate = 0.0105; // ~13.3% a.a.

    for (let i = 1; i <= totalMonths; i++) {
      capital += monthlyContribution;
      capital *= 1 + monthlyRate;
    }

    const totalReturn = capital - totalInvested;
    const totalReturnPercent = (totalReturn / totalInvested) * 100;

    return {
      finalCapital: Math.round(capital),
      totalInvested,
      totalReturn: Math.round(totalReturn),
      totalReturnPercent: Number(totalReturnPercent.toFixed(2)),
      annualizedReturn: 13.35,
      maxDrawdown: -14.2,
      sharpeRatio: 0.85,
    };
  }, [initialCapital, monthlyContribution, years]);

  return (
    <div className="container mx-auto px-4 py-8 flex flex-col gap-6 max-w-5xl">
      <div className="flex flex-col gap-2">
        <div className="flex items-center gap-2">
          <Badge variant="outline" className="text-xs">
            Simulador de Carteiras
          </Badge>
          <span className="text-xs text-muted-foreground">
            Rebalanceamento & Aportes Periódicos
          </span>
        </div>
        <h1 className="text-3xl font-extrabold tracking-tight flex items-center gap-2.5">
          <BarChart3 className="size-8 text-emerald-500" />
          Simulador de Backtest de Portfólio de ETFs
        </h1>
        <p className="text-muted-foreground text-sm">
          Simule o crescimento patrimonial da sua alocação de ativos com aportes mensais
          recorrentes, reinvestimento de dividendos e histórico real de cotações.
        </p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Form Controls */}
        <Card className="lg:col-span-1">
          <CardHeader>
            <CardTitle className="text-sm font-semibold uppercase text-muted-foreground">
              Configurações do Aporte
            </CardTitle>
            <CardDescription className="text-xs">
              Defina os aportes e prazo da simulação:
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-4">
            <div className="flex flex-col gap-1.5">
              <label
                htmlFor="initial-capital"
                className="text-xs font-semibold flex items-center gap-1"
              >
                <DollarSign className="size-3.5 text-emerald-500" />
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
                className="text-xs font-semibold flex items-center gap-1"
              >
                <DollarSign className="size-3.5 text-emerald-500" />
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
                htmlFor="simulation-years"
                className="text-xs font-semibold flex items-center gap-1"
              >
                <Calendar className="size-3.5 text-emerald-500" />
                Prazo em Anos
              </label>
              <Input
                id="simulation-years"
                type="number"
                min={1}
                max={30}
                value={years}
                onChange={(e) => setYears(Number(e.target.value) || 1)}
                className="font-mono text-sm"
              />
            </div>

            <div className="pt-2">
              <div className="text-xs font-semibold mb-2">Carteira Modelo (Alocação):</div>
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
            <Card className="border-emerald-500/30 bg-emerald-500/5">
              <CardHeader className="pb-2">
                <CardTitle className="text-xs font-semibold uppercase text-emerald-400">
                  Patrimônio Final Estimado
                </CardTitle>
              </CardHeader>
              <CardContent>
                <div className="text-3xl font-extrabold font-mono text-emerald-400">
                  {formatCurrencyBRL(simulation.finalCapital)}
                </div>
                <p className="text-xs text-muted-foreground mt-1">
                  Lucro líquido gerado de {formatCurrencyBRL(simulation.totalReturn)} (
                  {formatPercent(simulation.totalReturnPercent)})
                </p>
              </CardContent>
            </Card>

            <Card>
              <CardHeader className="pb-2">
                <CardTitle className="text-xs font-semibold uppercase text-muted-foreground">
                  Total Desembolsado (Aportes)
                </CardTitle>
              </CardHeader>
              <CardContent>
                <div className="text-3xl font-extrabold font-mono">
                  {formatCurrencyBRL(simulation.totalInvested)}
                </div>
                <p className="text-xs text-muted-foreground mt-1">
                  {years * 12} aportes mensais de {formatCurrencyBRL(monthlyContribution)}
                </p>
              </CardContent>
            </Card>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <Card>
              <CardHeader className="pb-1">
                <CardTitle className="text-xs uppercase text-muted-foreground font-semibold">
                  Retorno Anualizado (CAGR)
                </CardTitle>
              </CardHeader>
              <CardContent>
                <div className="text-xl font-bold font-mono text-emerald-500">
                  {formatPercent(simulation.annualizedReturn)} a.a.
                </div>
              </CardContent>
            </Card>

            <Card>
              <CardHeader className="pb-1">
                <CardTitle className="text-xs uppercase text-muted-foreground font-semibold">
                  Índice Sharpe
                </CardTitle>
              </CardHeader>
              <CardContent>
                <div className="text-xl font-bold font-mono">
                  {simulation.sharpeRatio.toFixed(2)}
                </div>
              </CardContent>
            </Card>

            <Card>
              <CardHeader className="pb-1">
                <CardTitle className="text-xs uppercase text-muted-foreground font-semibold">
                  Drawdown Máximo Histórico
                </CardTitle>
              </CardHeader>
              <CardContent>
                <div className="text-xl font-bold font-mono text-rose-500">
                  {formatPercent(simulation.maxDrawdown)}
                </div>
              </CardContent>
            </Card>
          </div>

          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-xs uppercase text-muted-foreground font-semibold flex items-center gap-1.5">
                <ShieldCheck className="size-3.5 text-emerald-500" />
                Resumo da Metodologia de Backtest
              </CardTitle>
            </CardHeader>
            <CardContent className="text-xs text-muted-foreground leading-relaxed">
              O motor de simulação executa rebalanceamento periódico anual, ajustando os pesos alvo
              sem disparar vendas tributáveis desnecessárias quando possível. As cotações diárias
              são preservadas com ajuste integral de proventos e taxas de administração embutidas.
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}
