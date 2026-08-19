'use client';

import * as React from 'react';
import { useQuery } from '@tanstack/react-query';
import { useQueryState, parseAsString } from 'nuqs';
import { fetchAssets } from '@/lib/api-client';
import { formatCurrencyBRL, formatPercent } from '@/lib/utils';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Layers, Plus, X } from 'lucide-react';

export default function ComparadorPage() {
  const [queryTicker] = useQueryState('ticker', parseAsString.withDefault('IVVB11'));
  const [selectedTickers, setSelectedTickers] = React.useState<string[]>(() => {
    const initial = queryTicker ? [queryTicker] : ['IVVB11'];
    const defaults = ['BOVA11', 'B5P211'];
    return Array.from(new Set([...initial, ...defaults]));
  });

  const { data: assets = [] } = useQuery({
    queryKey: ['assets'],
    queryFn: () => fetchAssets(),
  });

  const selectedTickerSet = React.useMemo(() => new Set(selectedTickers), [selectedTickers]);
  const comparedAssets = React.useMemo(
    () => assets.filter((asset) => selectedTickerSet.has(asset.ticker)),
    [assets, selectedTickerSet],
  );

  const toggleTicker = (ticker: string) => {
    if (selectedTickerSet.has(ticker)) {
      if (selectedTickers.length > 1) {
        setSelectedTickers((prev) => prev.filter((t) => t !== ticker));
      }
    } else {
      if (selectedTickers.length < 6) {
        setSelectedTickers((prev) => [...prev, ticker]);
      }
    }
  };

  return (
    <div className="container mx-auto px-4 py-8 flex flex-col gap-6">
      <div className="flex flex-col gap-2">
        <div className="flex items-center gap-2">
          <span className="text-xs text-muted-foreground">Até 6 ativos simultâneos</span>
        </div>
        <h1 className="text-2xl sm:text-3xl font-bold tracking-tight flex items-center gap-2.5 text-foreground">
          <Layers className="size-6 text-primary" />
          Comparador de ETFs e BDRs
        </h1>
        <p className="text-muted-foreground text-sm max-w-2xl">
          Compare lado a lado taxas de administração, patrimônio líquido, retorno acumulado no ano e
          regras de tributação.
        </p>
      </div>

      {/* Selected Tickers Selector */}
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-xs uppercase text-muted-foreground font-semibold">
            Ativos Selecionados para Comparação
          </CardTitle>
          <CardDescription className="text-xs">
            Clique nos ativos abaixo para adicionar ou remover da grade de comparação:
          </CardDescription>
        </CardHeader>
        <CardContent className="flex flex-wrap gap-2">
          {assets.map((asset) => {
            const isSelected = selectedTickerSet.has(asset.ticker);
            return (
              <Button
                key={asset.ticker}
                variant={isSelected ? 'default' : 'outline'}
                size="sm"
                onClick={() => toggleTicker(asset.ticker)}
                className="gap-1.5 font-mono"
              >
                {asset.ticker}
                {isSelected ? <X className="size-3" /> : <Plus className="size-3" />}
              </Button>
            );
          })}
        </CardContent>
      </Card>

      {/* Comparison Grid */}
      <div className="rounded-lg border bg-card shadow-sm overflow-x-auto">
        <Table>
          <TableHeader className="bg-muted/40">
            <TableRow>
              <TableHead className="w-48 text-xs font-semibold">Métrica / Atributo</TableHead>
              {comparedAssets.map((asset) => (
                <TableHead key={asset.ticker} className="text-xs font-bold text-center font-mono">
                  <div className="flex flex-col items-center gap-0.5">
                    <span className="text-primary text-sm font-semibold">{asset.ticker}</span>
                    <span className="text-xs text-muted-foreground font-sans font-normal truncate max-w-[120px]">
                      {asset.manager}
                    </span>
                  </div>
                </TableHead>
              ))}
            </TableRow>
          </TableHeader>
          <TableBody>
            <TableRow>
              <TableCell className="font-semibold text-xs text-muted-foreground">
                Nome do Fundo
              </TableCell>
              {comparedAssets.map((asset) => (
                <TableCell key={asset.ticker} className="text-xs text-center">
                  {asset.name}
                </TableCell>
              ))}
            </TableRow>
            <TableRow>
              <TableCell className="font-semibold text-xs text-muted-foreground">
                Índice Benchmark
              </TableCell>
              {comparedAssets.map((asset) => (
                <TableCell
                  key={asset.ticker}
                  className="text-xs text-center font-mono font-semibold"
                >
                  <Badge variant="secondary">{asset.benchmark}</Badge>
                </TableCell>
              ))}
            </TableRow>
            <TableRow>
              <TableCell className="font-semibold text-xs text-muted-foreground">
                Taxa de Administração
              </TableCell>
              {comparedAssets.map((asset) => (
                <TableCell
                  key={asset.ticker}
                  className="text-xs text-center font-mono font-medium text-foreground"
                >
                  {formatPercent(asset.managementFee)} a.a.
                </TableCell>
              ))}
            </TableRow>
            <TableRow>
              <TableCell className="font-semibold text-xs text-muted-foreground">
                Patrimônio Líquido
              </TableCell>
              {comparedAssets.map((asset) => (
                <TableCell key={asset.ticker} className="text-xs text-center font-mono">
                  {formatCurrencyBRL(asset.netAssets)}
                </TableCell>
              ))}
            </TableRow>
            <TableRow>
              <TableCell className="font-semibold text-xs text-muted-foreground">
                Cotação Atual
              </TableCell>
              {comparedAssets.map((asset) => (
                <TableCell
                  key={asset.ticker}
                  className="text-xs text-center font-mono font-semibold text-foreground"
                >
                  {formatCurrencyBRL(asset.lastPrice)}
                </TableCell>
              ))}
            </TableRow>
            <TableRow>
              <TableCell className="font-semibold text-xs text-muted-foreground">
                Retorno YTD (%)
              </TableCell>
              {comparedAssets.map((asset) => (
                <TableCell
                  key={asset.ticker}
                  className={`text-xs text-center font-mono font-semibold ${
                    asset.changeYtdPercent >= 0 ? 'text-positive' : 'text-negative'
                  }`}
                >
                  {formatPercent(asset.changeYtdPercent)}
                </TableCell>
              ))}
            </TableRow>
            <TableRow>
              <TableCell className="font-semibold text-xs text-muted-foreground">
                Tributação (IR)
              </TableCell>
              {comparedAssets.map((asset) => (
                <TableCell key={asset.ticker} className="text-xs text-center text-muted-foreground">
                  15% Swing / 20% Day Trade (Sem isenção 20k)
                </TableCell>
              ))}
            </TableRow>
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
