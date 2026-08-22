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
  const [queryTicker] = useQueryState('ticker', parseAsString.withDefault(''));
  const [userSelectedTickers, setUserSelectedTickers] = React.useState<string[] | null>(() => {
    return queryTicker ? [queryTicker.toUpperCase()] : null;
  });

  const { data: assets = [], isLoading } = useQuery({
    queryKey: ['assets'],
    queryFn: () => fetchAssets(),
  });

  const selectedTickers = React.useMemo(() => {
    if (userSelectedTickers !== null) {
      return userSelectedTickers;
    }
    if (assets.length > 0) {
      return assets.slice(0, Math.min(2, assets.length)).map((a) => a.ticker);
    }
    return queryTicker ? [queryTicker.toUpperCase()] : [];
  }, [userSelectedTickers, assets, queryTicker]);

  const selectedTickerSet = React.useMemo(() => new Set(selectedTickers), [selectedTickers]);
  const comparedAssets = React.useMemo(
    () => assets.filter((asset) => selectedTickerSet.has(asset.ticker)),
    [assets, selectedTickerSet],
  );

  const toggleTicker = (ticker: string) => {
    if (selectedTickerSet.has(ticker)) {
      if (selectedTickers.length > 1) {
        setUserSelectedTickers(selectedTickers.filter((t) => t !== ticker));
      }
    } else {
      if (selectedTickers.length < 6) {
        setUserSelectedTickers([...selectedTickers, ticker]);
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
          Comparador de Ativos B3
        </h1>
        <p className="text-muted-foreground text-sm max-w-2xl">
          Compare lado a lado indicadores de retorno, volatilidade, índice Sharpe e cotação dos
          ativos negociados na B3.
        </p>
      </div>

      {/* Selected Tickers Selector */}
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-xs uppercase text-muted-foreground font-semibold">
            Ativos Selecionados para Comparação
          </CardTitle>
          <CardDescription className="text-xs">
            {assets.length > 0
              ? 'Clique nos ativos abaixo para adicionar ou remover da grade de comparação:'
              : 'Nenhum ativo disponível no momento.'}
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
      {comparedAssets.length > 0 ? (
        <div className="rounded-lg border bg-card shadow-sm overflow-x-auto">
          <Table>
            <TableHeader className="bg-muted/40">
              <TableRow>
                <TableHead className="w-48 text-xs font-semibold">Métrica / Atributo</TableHead>
                {comparedAssets.map((asset) => (
                  <TableHead
                    key={asset.ticker}
                    className="text-xs font-bold text-center font-mono"
                  >
                    <div className="flex flex-col items-center gap-0.5">
                      <span className="text-primary text-sm font-semibold">{asset.ticker}</span>
                      <span className="text-xs text-muted-foreground font-sans font-normal truncate max-w-[140px]">
                        {asset.name}
                      </span>
                    </div>
                  </TableHead>
                ))}
              </TableRow>
            </TableHeader>
            <TableBody>
              <TableRow>
                <TableCell className="font-semibold text-xs text-muted-foreground">
                  Tipo de Ativo
                </TableCell>
                {comparedAssets.map((asset) => (
                  <TableCell key={asset.ticker} className="text-xs text-center font-mono">
                    <Badge variant="secondary">{asset.assetType}</Badge>
                  </TableCell>
                ))}
              </TableRow>
              <TableRow>
                <TableCell className="font-semibold text-xs text-muted-foreground">
                  Benchmark
                </TableCell>
                {comparedAssets.map((asset) => (
                  <TableCell
                    key={asset.ticker}
                    className="text-xs text-center font-mono font-semibold"
                  >
                    {asset.benchmarkSymbol ? (
                      <Badge variant="outline">{asset.benchmarkSymbol}</Badge>
                    ) : (
                      '—'
                    )}
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
                    {asset.lastPrice != null ? formatCurrencyBRL(asset.lastPrice) : '—'}
                  </TableCell>
                ))}
              </TableRow>
              <TableRow>
                <TableCell className="font-semibold text-xs text-muted-foreground">
                  Variação no Dia
                </TableCell>
                {comparedAssets.map((asset) => {
                  const val = asset.changeDayPercent;
                  const isPositive = (val ?? 0) >= 0;
                  return (
                    <TableCell
                      key={asset.ticker}
                      className={`text-xs text-center font-mono font-semibold ${
                        val == null ? 'text-muted-foreground' : isPositive ? 'text-positive' : 'text-negative'
                      }`}
                    >
                      {val != null ? formatPercent(val) : '—'}
                    </TableCell>
                  );
                })}
              </TableRow>
              <TableRow>
                <TableCell className="font-semibold text-xs text-muted-foreground">
                  Retorno 12 Meses
                </TableCell>
                {comparedAssets.map((asset) => {
                  const val = asset.return12mPercent;
                  const isPositive = (val ?? 0) >= 0;
                  return (
                    <TableCell
                      key={asset.ticker}
                      className={`text-xs text-center font-mono font-semibold ${
                        val == null ? 'text-muted-foreground' : isPositive ? 'text-positive' : 'text-negative'
                      }`}
                    >
                      {val != null ? formatPercent(val) : '—'}
                    </TableCell>
                  );
                })}
              </TableRow>
              <TableRow>
                <TableCell className="font-semibold text-xs text-muted-foreground">
                  Volatilidade Anualizada
                </TableCell>
                {comparedAssets.map((asset) => (
                  <TableCell
                    key={asset.ticker}
                    className="text-xs text-center font-mono text-foreground"
                  >
                    {asset.annualizedVolatilityPercent != null
                      ? formatPercent(asset.annualizedVolatilityPercent)
                      : '—'}
                  </TableCell>
                ))}
              </TableRow>
              <TableRow>
                <TableCell className="font-semibold text-xs text-muted-foreground">
                  Índice Sharpe
                </TableCell>
                {comparedAssets.map((asset) => (
                  <TableCell
                    key={asset.ticker}
                    className="text-xs text-center font-mono font-semibold"
                  >
                    {asset.sharpeRatio != null ? asset.sharpeRatio.toFixed(2) : '—'}
                  </TableCell>
                ))}
              </TableRow>
              <TableRow>
                <TableCell className="font-semibold text-xs text-muted-foreground">
                  Máximo Drawdown
                </TableCell>
                {comparedAssets.map((asset) => (
                  <TableCell
                    key={asset.ticker}
                    className="text-xs text-center font-mono text-negative"
                  >
                    {asset.maxDrawdownPercent != null
                      ? formatPercent(asset.maxDrawdownPercent)
                      : '—'}
                  </TableCell>
                ))}
              </TableRow>
            </TableBody>
          </Table>
        </div>
      ) : (
        <div className="flex h-48 items-center justify-center rounded-lg border border-dashed text-sm text-muted-foreground">
          {isLoading ? 'Carregando ativos...' : 'Nenhum ativo selecionado para comparação.'}
        </div>
      )}
    </div>
  );
}
