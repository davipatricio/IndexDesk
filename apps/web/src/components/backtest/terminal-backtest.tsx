'use client';

import * as React from 'react';
import { useQuery } from '@tanstack/react-query';
import { useQueryState, parseAsString } from 'nuqs';
import {
  Area,
  Bar,
  CartesianGrid,
  Cell,
  ComposedChart,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import {
  BarChart3,
  CalendarDays,
  CircleHelp,
  DollarSign,
  Info,
  Play,
  Plus,
  RotateCcw,
  Share2,
  Trash2,
  WalletCards,
} from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  fetchAssets,
  fetchBacktest,
  type AssetDto,
  type BacktestRequest,
  type BacktestResponse,
} from '@/lib/api-client';
import { formatCurrencyBRL, formatPercent } from '@/lib/utils';

const COLORS = [
  'var(--chart-1)',
  'var(--chart-2)',
  'var(--chart-3)',
  'var(--chart-4)',
  'var(--chart-5)',
];
const REBALANCE_OPTIONS = [
  { value: 'none', label: 'Nenhum · Buy & Hold' },
  { value: 'monthly', label: 'Mensal' },
  { value: 'quarterly', label: 'Trimestral' },
  { value: 'semiannual', label: 'Semestral' },
  { value: 'annual', label: 'Anual' },
] as const;

type Rebalance = (typeof REBALANCE_OPTIONS)[number]['value'];
type Allocation = { ticker: string; weightPercent: number };

type CurvePoint = BacktestResponse['equityCurve'][number] & {
  invested: number;
  drawdown: number;
  year: string;
  benchmarkValue?: number;
};

function dateInputValue(date: Date): string {
  return date.toISOString().slice(0, 10);
}

function defaultFromDate(): string {
  const date = new Date();
  date.setFullYear(date.getFullYear() - 5);
  return dateInputValue(date);
}

function defaultToDate(): string {
  return dateInputValue(new Date());
}

function toCurveData(response: BacktestResponse, monthlyContribution: number): CurvePoint[] {
  let peak = 0;
  const benchmarkMap = new Map<string, number>(
    response.benchmarkCurve?.map((point) => [point.date, point.value]) ?? [],
  );
  return response.equityCurve.map((point, index) => {
    const invested = response.initialCapital + monthlyContribution * index;
    peak = Math.max(peak, point.value);
    return {
      ...point,
      invested,
      benchmarkValue: benchmarkMap.get(point.date),
      drawdown: peak > 0 ? (point.value / peak - 1) * 100 : 0,
      year: point.date.slice(0, 4),
    };
  });
}

function annualReturns(curve: CurvePoint[]): Array<{ year: string; returnPercent: number }> {
  const years = new Map<string, CurvePoint[]>();
  curve.forEach((point) => years.set(point.year, [...(years.get(point.year) ?? []), point]));
  return [...years.entries()].map(([year, points]) => {
    const first = points[0]?.value ?? 0;
    const last = points.at(-1)?.value ?? first;
    return { year, returnPercent: first > 0 ? (last / first - 1) * 100 : 0 };
  });
}

function BacktestTooltip({
  active,
  payload,
  label,
  benchmarkLabel,
}: {
  active?: boolean;
  payload?: Array<{ dataKey?: string; value?: number; name?: string }>;
  label?: string;
  benchmarkLabel?: string;
}) {
  if (!active || !payload?.length) return null;
  return (
    <div className="rounded-lg border bg-popover px-3 py-2 text-xs text-popover-foreground shadow-lg">
      <p className="mb-1 text-muted-foreground">{label}</p>
      {payload.map((entry) => {
        let title = 'Patrimônio';
        if (entry.dataKey === 'invested') title = 'Aportado';
        else if (entry.dataKey === 'benchmarkValue') title = benchmarkLabel ?? 'Benchmark';

        return (
          <p key={entry.dataKey} className="font-mono font-semibold">
            {title}: {formatCurrencyBRL(entry.value ?? 0)}
          </p>
        );
      })}
    </div>
  );
}

function AllocationRow({
  allocation,
  assets,
  onChange,
  onRemove,
}: {
  allocation: Allocation;
  assets: AssetDto[];
  onChange: (allocation: Allocation) => void;
  onRemove: () => void;
}) {
  const selectedAsset = assets.find((asset) => asset.ticker === allocation.ticker);
  return (
    <div className="grid grid-cols-[minmax(0,1fr)_82px_32px] items-end gap-2">
      <label className="min-w-0">
        <span className="mb-1 block text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">
          Ativo
        </span>
        <Select
          value={allocation.ticker}
          onValueChange={(ticker) =>
            onChange({ ...allocation, ticker: ticker ?? allocation.ticker })
          }
        >
          <SelectTrigger className="w-full font-mono text-xs">
            <SelectValue placeholder="Selecione" />
          </SelectTrigger>
          <SelectContent>
            {assets.map((asset) => (
              <SelectItem key={asset.ticker} value={asset.ticker}>
                <span className="font-mono">{asset.ticker}</span>
                <span className="ml-2 text-muted-foreground">{asset.name}</span>
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        {selectedAsset?.firstQuoteDate && selectedAsset.lastQuoteDate ? (
          <span className="mt-1 block truncate text-[10px] text-muted-foreground">
            {selectedAsset.firstQuoteDate} → {selectedAsset.lastQuoteDate}
          </span>
        ) : null}
      </label>
      <label>
        <span className="mb-1 block text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">
          Peso %
        </span>
        <Input
          type="number"
          min={0}
          max={100}
          step="0.01"
          value={allocation.weightPercent}
          onChange={(event) =>
            onChange({ ...allocation, weightPercent: Number(event.target.value) || 0 })
          }
          className="h-8 font-mono text-xs"
          aria-label={`Peso de ${allocation.ticker}`}
        />
      </label>
      <Button
        type="button"
        variant="ghost"
        size="icon-sm"
        aria-label={`Remover ${allocation.ticker}`}
        onClick={onRemove}
      >
        <Trash2 className="size-3.5 text-muted-foreground" />
      </Button>
    </div>
  );
}

function EmptyResults() {
  return (
    <div className="flex min-h-72 flex-col items-center justify-center rounded-xl border border-dashed p-8 text-center">
      <BarChart3 className="mb-3 size-8 text-muted-foreground/60" />
      <h2 className="text-sm font-semibold">Execute uma simulação para visualizar os resultados</h2>
      <p className="mt-1 max-w-md text-xs leading-relaxed text-muted-foreground">
        Escolha os ativos, defina o período e simule sua carteira para acompanhar patrimônio e
        risco.
      </p>
    </div>
  );
}

export function TerminalBacktest() {
  const [queryTicker] = useQueryState('ticker', parseAsString.withDefault(''));
  const {
    data: assets = [],
    isLoading: assetsLoading,
    isError: assetsError,
  } = useQuery({
    queryKey: ['assets', 'backtest-selector'],
    queryFn: () => fetchAssets(),
    staleTime: 60_000,
  });
  const [allocations, setAllocations] = React.useState<Allocation[]>(() =>
    queryTicker ? [{ ticker: queryTicker.toUpperCase(), weightPercent: 100 }] : [],
  );
  const [initialAmount, setInitialAmount] = React.useState(50_000);
  const [monthlyContribution, setMonthlyContribution] = React.useState(1_000);
  const [from, setFrom] = React.useState(defaultFromDate);
  const [to, setTo] = React.useState(defaultToDate);
  const [rebalance, setRebalance] = React.useState<Rebalance>('annual');
  const [benchmark, setBenchmark] = React.useState('CDI');
  const [request, setRequest] = React.useState<BacktestRequest | null>(null);

  const totalWeight = allocations.reduce(
    (total, allocation) => total + allocation.weightPercent,
    0,
  );
  const canSimulate =
    allocations.length > 0 && Math.abs(totalWeight - 100) < 0.01 && initialAmount > 0 && from < to;
  const resultQuery = useQuery({
    queryKey: ['analytics', 'backtest', request],
    queryFn: () => fetchBacktest(request!),
    enabled: request !== null,
    staleTime: 60_000,
  });
  const result = resultQuery.data;
  const curve = React.useMemo(
    () => (result ? toCurveData(result, monthlyContribution) : []),
    [monthlyContribution, result],
  );
  const annual = React.useMemo(() => annualReturns(curve), [curve]);

  const addAllocation = () => {
    const available = assets.find(
      (asset) => !allocations.some((item) => item.ticker === asset.ticker),
    );
    if (available)
      setAllocations((current) => [...current, { ticker: available.ticker, weightPercent: 0 }]);
  };

  const simulate = () => {
    if (!canSimulate) return;
    setRequest({
      initialAmount,
      monthlyContribution,
      allocations,
      benchmark,
      rebalance,
      from,
      to,
    });
  };

  const reset = () => {
    setRequest(null);
    setAllocations(queryTicker ? [{ ticker: queryTicker.toUpperCase(), weightPercent: 100 }] : []);
  };

  const share = async () => {
    const url = new URL(window.location.href);
    if (allocations[0]) url.searchParams.set('ticker', allocations[0].ticker);
    url.searchParams.set('initial', String(initialAmount));
    url.searchParams.set('monthly', String(monthlyContribution));
    url.searchParams.set('from', from);
    url.searchParams.set('to', to);
    await navigator.clipboard?.writeText(url.toString());
  };

  return (
    <div className="container mx-auto flex max-w-[1440px] flex-col gap-6 px-4 py-8">
      <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-end">
        <div>
          <h1 className="flex items-center gap-2.5 text-2xl font-bold tracking-tight sm:text-3xl">
            <BarChart3 className="size-6 text-primary" />
            Simulador de backtest
          </h1>
          <p className="mt-2 max-w-2xl text-sm leading-relaxed text-muted-foreground">
            Monte uma carteira e acompanhe patrimônio, risco e drawdown ao longo do tempo.
          </p>
        </div>
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={() => void share()}
          disabled={allocations.length === 0}
        >
          <Share2 className="size-3.5" /> Compartilhar configuração
        </Button>
      </div>

      <div className="grid items-start gap-6 xl:grid-cols-[300px_minmax(0,1fr)]">
        <Card className="xl:sticky xl:top-20">
          <CardHeader className="border-b pb-4">
            <CardTitle className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
              Configuração
            </CardTitle>
            <CardDescription className="text-xs">
              Defina os parâmetros que deseja comparar.
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-5 pt-5">
            <section className="flex flex-col gap-3">
              <div className="flex items-center justify-between">
                <h2 className="text-xs font-semibold">Carteira</h2>
                <span
                  className={`font-mono text-[11px] ${Math.abs(totalWeight - 100) < 0.01 ? 'text-positive' : 'text-negative'}`}
                >
                  {totalWeight.toFixed(2)}%
                </span>
              </div>
              {allocations.length > 0 ? (
                allocations.map((allocation, index) => (
                  <AllocationRow
                    key={allocation.ticker}
                    allocation={allocation}
                    assets={assets}
                    onChange={(next) =>
                      setAllocations((current) =>
                        current.map((item, itemIndex) => (itemIndex === index ? next : item)),
                      )
                    }
                    onRemove={() =>
                      setAllocations((current) =>
                        current.filter((_, itemIndex) => itemIndex !== index),
                      )
                    }
                  />
                ))
              ) : (
                <p className="rounded-lg border border-dashed p-3 text-xs leading-relaxed text-muted-foreground">
                  Escolha um ativo para começar a montar sua carteira.
                </p>
              )}
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={addAllocation}
                disabled={assetsLoading || assets.length <= allocations.length}
              >
                <Plus className="size-3.5" /> Adicionar ativo
              </Button>
              {assetsError ? (
                <p role="alert" className="text-xs text-negative">
                  Não foi possível carregar os ativos agora. Tente novamente em instantes.
                </p>
              ) : null}
            </section>

            <section className="flex flex-col gap-3 border-t pt-5">
              <h2 className="text-xs font-semibold">Capital e período</h2>
              <label>
                <span className="mb-1 flex items-center gap-1 text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">
                  <DollarSign className="size-3" /> Capital inicial
                </span>
                <Input
                  type="number"
                  min={0}
                  step="100"
                  value={initialAmount}
                  onChange={(event) => setInitialAmount(Number(event.target.value) || 0)}
                  className="font-mono text-sm"
                />
              </label>
              <label>
                <span className="mb-1 flex items-center gap-1 text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">
                  <WalletCards className="size-3" /> Aporte mensal
                </span>
                <Input
                  type="number"
                  min={0}
                  step="100"
                  value={monthlyContribution}
                  onChange={(event) => setMonthlyContribution(Number(event.target.value) || 0)}
                  className="font-mono text-sm"
                />
              </label>
              <div className="grid grid-cols-2 gap-2">
                <label>
                  <span className="mb-1 flex items-center gap-1 text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">
                    <CalendarDays className="size-3" /> De
                  </span>
                  <Input
                    type="date"
                    value={from}
                    onChange={(event) => setFrom(event.target.value)}
                    className="px-2 font-mono text-[11px]"
                  />
                </label>
                <label>
                  <span className="mb-1 block text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">
                    Até
                  </span>
                  <Input
                    type="date"
                    value={to}
                    onChange={(event) => setTo(event.target.value)}
                    className="px-2 font-mono text-[11px]"
                  />
                </label>
              </div>
            </section>

            <section className="flex flex-col gap-3 border-t pt-5">
              <h2 className="text-xs font-semibold">Estratégia</h2>
              <label>
                <span className="mb-1 block text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">
                  Rebalanceamento
                </span>
                <Select
                  value={rebalance}
                  onValueChange={(value) => setRebalance(value as Rebalance)}
                >
                  <SelectTrigger className="w-full text-xs">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {REBALANCE_OPTIONS.map((option) => (
                      <SelectItem key={option.value} value={option.value}>
                        {option.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </label>
              <label>
                <span className="mb-1 block text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">
                  Taxa livre / benchmark
                </span>
                <Select value={benchmark} onValueChange={(value) => setBenchmark(value ?? 'CDI')}>
                  <SelectTrigger className="w-full text-xs">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="CDI">CDI (BCB Série 12)</SelectItem>
                    <SelectItem value="SELIC">Selic (BCB Série 11)</SelectItem>
                    <SelectItem value="NONE">Sem benchmark</SelectItem>
                  </SelectContent>
                </Select>
              </label>
            </section>

            {!canSimulate && allocations.length > 0 ? (
              <p className="flex items-start gap-1.5 rounded-lg bg-negative/10 p-3 text-[11px] leading-relaxed text-negative">
                <Info className="mt-0.5 size-3.5 shrink-0" />A soma dos pesos deve ser 100%, o
                capital deve ser positivo e o início anterior ao fim.
              </p>
            ) : null}
            <div className="flex gap-2">
              <Button
                type="button"
                className="flex-1"
                onClick={simulate}
                disabled={!canSimulate || resultQuery.isFetching}
              >
                <Play className="size-3.5" />
                {resultQuery.isFetching ? 'Calculando…' : 'Simular carteira'}
              </Button>
              <Button
                type="button"
                variant="outline"
                size="icon"
                onClick={reset}
                aria-label="Limpar configuração"
              >
                <RotateCcw className="size-3.5" />
              </Button>
            </div>
          </CardContent>
        </Card>

        <div className="flex min-w-0 flex-col gap-4">
          {resultQuery.isError ? (
            <div
              role="alert"
              className="flex items-start gap-2 rounded-lg border border-negative/30 bg-negative/5 p-4 text-sm text-negative"
            >
              <CircleHelp className="mt-0.5 size-4 shrink-0" />
              <div>
                <strong>Não foi possível concluir a simulação</strong>
                <p className="mt-1 text-xs leading-relaxed">
                  Não encontramos informações suficientes para os ativos e o período escolhidos.
                  Tente ajustar os parâmetros e tente novamente.
                </p>
              </div>
            </div>
          ) : null}
          {result ? (
            <>
              <div className="grid grid-cols-2 gap-px overflow-hidden rounded-xl border bg-border sm:grid-cols-3 lg:grid-cols-5">
                {[
                  [
                    'CAGR',
                    formatPercent(result.annualizedReturnPercent),
                    'retorno anual composto',
                    'text-positive',
                  ],
                  [
                    'Volatilidade',
                    formatPercent(result.annualizedVolatilityPercent),
                    'anualizada',
                    'text-foreground',
                  ],
                  ['Sharpe', result.sharpeRatio.toFixed(2), `vs. ${benchmark}`, 'text-foreground'],
                  [
                    'Max drawdown',
                    formatPercent(result.maxDrawdownPercent),
                    'pico a vale',
                    'text-negative',
                  ],
                  [
                    'Saldo final',
                    formatCurrencyBRL(result.finalCapital),
                    `${formatCurrencyBRL(result.finalCapital - result.totalContributions)} de ganho`,
                    'text-primary',
                  ],
                ].map(([label, value, hint, color]) => (
                  <div key={label} className="bg-card p-4">
                    <span className="block text-[10px] font-semibold uppercase tracking-wide text-muted-foreground">
                      {label}
                    </span>
                    <strong className={`mt-2 block font-mono text-lg tracking-tight ${color}`}>
                      {value}
                    </strong>
                    <span className="mt-1 block text-[10px] text-muted-foreground">{hint}</span>
                  </div>
                ))}
              </div>
              <div className="grid gap-4 2xl:grid-cols-[minmax(0,1.6fr)_minmax(240px,0.7fr)]">
                <Card>
                  <CardHeader className="pb-2">
                    <CardTitle className="text-sm">Evolução patrimonial</CardTitle>
                    <CardDescription className="text-xs">
                      Curva calculada sobre {curve.length} sessões comuns
                    </CardDescription>
                  </CardHeader>
                  <CardContent>
                    <div className="h-[310px] w-full">
                      <ResponsiveContainer width="100%" height="100%">
                        <ComposedChart
                          data={curve}
                          margin={{ top: 8, right: 8, bottom: 0, left: 4 }}
                        >
                          <CartesianGrid
                            vertical={false}
                            stroke="var(--border)"
                            strokeDasharray="3 3"
                          />
                          <XAxis
                            dataKey="date"
                            axisLine={false}
                            tickLine={false}
                            minTickGap={32}
                            tick={{ fill: 'var(--muted-foreground)', fontSize: 10 }}
                          />
                          <YAxis
                            axisLine={false}
                            tickLine={false}
                            width={70}
                            tick={{ fill: 'var(--muted-foreground)', fontSize: 10 }}
                            tickFormatter={(value: number) => formatCurrencyBRL(value)}
                          />
                          <Tooltip
                            content={<BacktestTooltip benchmarkLabel={benchmark} />}
                            cursor={{ stroke: 'var(--muted-foreground)', strokeDasharray: '4 4' }}
                          />
                          <Area
                            type="monotone"
                            dataKey="value"
                            name="Patrimônio"
                            stroke="var(--positive)"
                            strokeWidth={2}
                            fill="var(--positive)"
                            fillOpacity={0.12}
                            dot={false}
                          />
                          {result.benchmarkCurve &&
                          result.benchmarkCurve.length > 0 &&
                          benchmark !== 'NONE' ? (
                            <Area
                              type="monotone"
                              dataKey="benchmarkValue"
                              name={benchmark}
                              stroke="var(--chart-4)"
                              strokeDasharray="4 4"
                              strokeWidth={1.5}
                              fill="none"
                              dot={false}
                            />
                          ) : null}
                          <Area
                            type="monotone"
                            dataKey="invested"
                            name="Aportado"
                            stroke="var(--chart-3)"
                            strokeWidth={1.5}
                            fill="none"
                            dot={false}
                          />
                        </ComposedChart>
                      </ResponsiveContainer>
                    </div>
                    <div className="mt-3 flex flex-wrap gap-4 text-[10px] text-muted-foreground">
                      <span>
                        <i className="mr-1 inline-block h-2 w-2 rounded-sm bg-positive" />
                        Patrimônio
                      </span>
                      {result.benchmarkCurve &&
                      result.benchmarkCurve.length > 0 &&
                      benchmark !== 'NONE' ? (
                        <span>
                          <i className="mr-1 inline-block h-2 w-2 rounded-sm bg-chart-4" />
                          {benchmark} acumulado
                          {result.benchmarkFinalCapital
                            ? ` (${formatCurrencyBRL(result.benchmarkFinalCapital)})`
                            : ''}
                        </span>
                      ) : null}
                      <span>
                        <i className="mr-1 inline-block h-2 w-2 rounded-sm bg-chart-3" />
                        Total aportado
                      </span>
                    </div>
                  </CardContent>
                </Card>
                <Card>
                  <CardHeader className="pb-2">
                    <CardTitle className="text-sm">Alocação inicial</CardTitle>
                    <CardDescription className="text-xs">
                      Pesos enviados à simulação
                    </CardDescription>
                  </CardHeader>
                  <CardContent>
                    <div className="h-[220px]">
                      <ResponsiveContainer width="100%" height="100%">
                        <PieChart>
                          <Pie
                            data={allocations}
                            dataKey="weightPercent"
                            nameKey="ticker"
                            innerRadius={58}
                            outerRadius={82}
                            paddingAngle={2}
                          >
                            {allocations.map((allocation, index) => (
                              <Cell key={allocation.ticker} fill={COLORS[index % COLORS.length]} />
                            ))}
                          </Pie>
                          <Tooltip formatter={(value) => `${Number(value).toFixed(2)}%`} />
                        </PieChart>
                      </ResponsiveContainer>
                    </div>
                    <div className="flex flex-col gap-2">
                      {allocations.map((allocation, index) => (
                        <div
                          key={allocation.ticker}
                          className="flex items-center justify-between text-xs"
                        >
                          <span className="flex items-center gap-1.5 font-mono">
                            <i
                              className="size-2 rounded-sm"
                              style={{ background: COLORS[index % COLORS.length] }}
                            />
                            {allocation.ticker}
                          </span>
                          <strong className="font-mono">
                            {formatPercent(allocation.weightPercent)}
                          </strong>
                        </div>
                      ))}
                    </div>
                  </CardContent>
                </Card>
              </div>
              <div className="grid gap-4 lg:grid-cols-2">
                <Card>
                  <CardHeader className="pb-2">
                    <CardTitle className="text-sm">Underwater drawdown</CardTitle>
                    <CardDescription className="text-xs">
                      Distância do patrimônio ao pico anterior
                    </CardDescription>
                  </CardHeader>
                  <CardContent>
                    <div className="h-[220px]">
                      <ResponsiveContainer width="100%" height="100%">
                        <ComposedChart data={curve}>
                          <CartesianGrid
                            vertical={false}
                            stroke="var(--border)"
                            strokeDasharray="3 3"
                          />
                          <XAxis dataKey="date" hide />
                          <YAxis
                            domain={['auto', 0]}
                            tickFormatter={(value: number) => `${value.toFixed(0)}%`}
                            axisLine={false}
                            tickLine={false}
                            width={42}
                            tick={{ fill: 'var(--muted-foreground)', fontSize: 10 }}
                          />
                          <Tooltip formatter={(value) => `${Number(value).toFixed(2)}%`} />
                          <Area
                            type="monotone"
                            dataKey="drawdown"
                            stroke="var(--negative)"
                            fill="var(--negative)"
                            fillOpacity={0.16}
                            dot={false}
                          />
                        </ComposedChart>
                      </ResponsiveContainer>
                    </div>
                  </CardContent>
                </Card>
                <Card>
                  <CardHeader className="pb-2">
                    <CardTitle className="text-sm">Retornos anuais</CardTitle>
                    <CardDescription className="text-xs">
                      Variação entre a primeira e a última sessão de cada ano
                    </CardDescription>
                  </CardHeader>
                  <CardContent>
                    <div className="h-[220px]">
                      <ResponsiveContainer width="100%" height="100%">
                        <ComposedChart data={annual}>
                          <CartesianGrid
                            vertical={false}
                            stroke="var(--border)"
                            strokeDasharray="3 3"
                          />
                          <XAxis
                            dataKey="year"
                            axisLine={false}
                            tickLine={false}
                            tick={{ fill: 'var(--muted-foreground)', fontSize: 10 }}
                          />
                          <YAxis
                            tickFormatter={(value: number) => `${value.toFixed(0)}%`}
                            axisLine={false}
                            tickLine={false}
                            width={42}
                            tick={{ fill: 'var(--muted-foreground)', fontSize: 10 }}
                          />
                          <Tooltip formatter={(value) => `${Number(value).toFixed(2)}%`} />
                          <Bar dataKey="returnPercent" name="Retorno" radius={[3, 3, 0, 0]}>
                            {annual.map((point) => (
                              <Cell
                                key={point.year}
                                fill={
                                  point.returnPercent >= 0 ? 'var(--positive)' : 'var(--negative)'
                                }
                              />
                            ))}
                          </Bar>
                        </ComposedChart>
                      </ResponsiveContainer>
                    </div>
                  </CardContent>
                </Card>
              </div>
              <Card variant="warning">
                <CardContent className="flex items-start gap-2 p-4 text-xs leading-relaxed text-muted-foreground">
                  <Info className="mt-0.5 size-4 shrink-0 text-warning" />
                  <span>
                    <strong className="text-foreground">Como funciona.</strong> A simulação
                    considera os preços disponíveis, usa as mesmas datas para todos os ativos e
                    aplica os aportes no primeiro pregão de cada novo mês. Resultados passados não
                    garantem resultados futuros. Quando não houver referência para comparação, o
                    indicador de Sharpe é calculado sem taxa livre de risco.
                  </span>
                </CardContent>
              </Card>
            </>
          ) : (
            <EmptyResults />
          )}
        </div>
      </div>
    </div>
  );
}
