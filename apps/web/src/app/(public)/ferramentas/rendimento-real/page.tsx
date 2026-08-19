'use client';

import * as React from 'react';
import { useQueryState, parseAsFloat } from 'nuqs';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { formatPercent } from '@/lib/utils';
import { Calculator, Info, ShieldCheck } from 'lucide-react';

export default function RendimentoRealPage() {
  const [nominalRate, setNominalRate] = useQueryState('nominal', parseAsFloat.withDefault(12.0));
  const [inflationRate, setInflationRate] = useQueryState(
    'inflacao',
    parseAsFloat.withDefault(4.0),
  );

  // Fisher Equation: (1 + i) = (1 + r) * (1 + inf) => r = ((1 + i) / (1 + inf)) - 1
  const realYield = React.useMemo(() => {
    const nom = (nominalRate ?? 0) / 100;
    const inf = (inflationRate ?? 0) / 100;
    if (inf <= -1) return 0;
    const real = ((1 + nom) / (1 + inf) - 1) * 100;
    return Number(real.toFixed(4));
  }, [nominalRate, inflationRate]);

  // Subtraction approximation error
  const naiveYield = (nominalRate ?? 0) - (inflationRate ?? 0);
  const mathDifference = Number((naiveYield - realYield).toFixed(4));

  return (
    <div className="container mx-auto px-4 py-8 flex flex-col gap-6 max-w-4xl">
      <div className="flex flex-col gap-2">
        <div className="flex items-center gap-2">
          <Badge variant="outline" className="text-xs">
            Calculadora Matemática Oficial
          </Badge>
          <span className="text-xs text-muted-foreground">Equação de Irving Fisher</span>
        </div>
        <h1 className="text-3xl font-extrabold tracking-tight flex items-center gap-2.5">
          <Calculator className="size-8 text-emerald-500" />
          Calculadora de Rendimento Real (Equação de Fisher)
        </h1>
        <p className="text-muted-foreground text-sm">
          Descubra a rentabilidade real exata descontando o impacto da inflação (IPCA/IGP-M) sobre a
          rentabilidade nominal dos seus investimentos em ETFs e Renda Fixa.
        </p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        {/* Input Form Card */}
        <Card>
          <CardHeader>
            <CardTitle className="text-sm font-semibold uppercase text-muted-foreground">
              Parâmetros da Simulação
            </CardTitle>
            <CardDescription className="text-xs">
              Altere os valores para atualizar o cálculo em tempo real e na URL:
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-4">
            <div className="flex flex-col gap-1.5">
              <label htmlFor="nominal-rate" className="text-xs font-semibold">
                Taxa Nominal Anual (% a.a.)
              </label>
              <Input
                id="nominal-rate"
                type="number"
                step="0.01"
                value={nominalRate ?? 12}
                onChange={(e) => setNominalRate(parseFloat(e.target.value) || 0)}
                placeholder="12.00"
                className="font-mono"
              />
              <span className="text-[11px] text-muted-foreground">
                Ex: Rentabilidade bruta do CDB ou ETF de Renda Fixa.
              </span>
            </div>

            <div className="flex flex-col gap-1.5">
              <label htmlFor="inflation-rate" className="text-xs font-semibold">
                Taxa de Inflação Acumulada IPCA (% a.a.)
              </label>
              <Input
                id="inflation-rate"
                type="number"
                step="0.01"
                value={inflationRate ?? 4}
                onChange={(e) => setInflationRate(parseFloat(e.target.value) || 0)}
                placeholder="4.00"
                className="font-mono"
              />
              <span className="text-[11px] text-muted-foreground">
                Ex: IPCA ou IGP-M anual projetado pelo Focus.
              </span>
            </div>
          </CardContent>
        </Card>

        {/* Results Card */}
        <Card className="border-emerald-500/30 bg-emerald-500/5 flex flex-col justify-between">
          <CardHeader>
            <CardTitle className="text-sm font-semibold text-emerald-400 uppercase flex items-center gap-1.5">
              <ShieldCheck className="size-4" />
              Resultado: Taxa Real Exata
            </CardTitle>
            <CardDescription className="text-xs text-muted-foreground">
              Rendimento líquido do poder de compra
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-4">
            <div className="flex flex-col">
              <span className="text-4xl sm:text-5xl font-extrabold font-mono text-emerald-400">
                {formatPercent(realYield, 4)}
              </span>
              <span className="text-xs text-muted-foreground mt-1">ao ano acima da inflação</span>
            </div>

            <div className="p-3 rounded-lg bg-background/80 border text-xs flex flex-col gap-1.5 text-muted-foreground">
              <div className="flex justify-between font-mono">
                <span>Subtração simples (Incorreta):</span>
                <span className="font-semibold text-foreground">
                  {formatPercent(naiveYield)} a.a.
                </span>
              </div>
              <div className="flex justify-between font-mono text-amber-500">
                <span>Diferença / Erro matemático:</span>
                <span className="font-semibold">{formatPercent(mathDifference, 4)} a.a.</span>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Educational Formula Explanation */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-xs uppercase text-muted-foreground font-semibold flex items-center gap-1.5">
            <Info className="size-3.5" />
            Por que a subtração simples (Nominal - Inflação) está incorreta?
          </CardTitle>
        </CardHeader>
        <CardContent className="text-xs text-muted-foreground leading-relaxed flex flex-col gap-2">
          <p>
            No mercado financeiro, as taxas de juros e de inflação incidem de maneira composta. A{' '}
            <strong>Equação de Irving Fisher</strong> define que o capital corrigido pela taxa
            nominal é igual ao produto da correção pela inflação e pelo juro real:
          </p>
          <div className="p-3 rounded bg-muted/60 font-mono text-foreground text-center font-bold">
            (1 + Taxa Nominal) = (1 + Taxa Real) × (1 + Inflação)
          </div>
          <p>
            Isolando a Taxa Real: <code>Taxa Real = ((1 + Nominal) / (1 + Inflação)) - 1</code>.
          </p>
        </CardContent>
      </Card>
    </div>
  );
}
