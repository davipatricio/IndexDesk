import type { User, AuthResponse, SignInDto, SignUpDto } from '@/types/auth';

export type { User, AuthResponse, SignInDto, SignUpDto };

/** Compact asset row returned by GET /api/v1/assets. */
export interface AssetDto {
  ticker: string;
  name: string;
  assetType: string;
  currency: string;
  benchmarkSymbol: string | null;
  lastPrice: number | null;
  changeDayPercent: number | null;
  return1mPercent: number | null;
  return12mPercent: number | null;
  annualizedVolatilityPercent: number | null;
  sharpeRatio: number | null;
  maxDrawdownPercent: number | null;
  firstQuoteDate: string | null;
  lastQuoteDate: string | null;
}

export interface QuoteItem {
  date: string;
  open: number;
  high: number;
  low: number;
  close: number;
  adjustedClose: number;
  volume: number;
}

/** Rich single-asset response returned by GET /api/v1/assets/{ticker}. */
export interface AssetFiscalData {
  taxDomicile: 'BRAZIL' | 'USA' | 'IRELAND_UCITS' | 'OTHER';
  isEtf: boolean;
  isBdr: boolean;
  isFii: boolean;
  incomeTaxRatePercent: number;
  dayTradeTaxRatePercent: number;
  hasComeCotas: boolean;
  hasMonthlySalesTaxExemption: boolean;
  isTaxWithheldAtSource: boolean;
  foreignDividendWithholdingPercent: number;
  taxSummary: string;
}

export interface AssetDetailDto {
  ticker: string;
  name: string;
  assetType: string;
  cnpj: string | null;
  isin: string | null;
  currency: string;
  tradingViewSymbol: string | null;
  stats: AssetQuoteStatsDto;
  fiscal: AssetFiscalData;
}

export interface AssetQuoteStatsDto {
  lastPrice: number | null;
  changeDayPercent: number | null;
  return1mPercent: number | null;
  return6mPercent: number | null;
  return12mPercent: number | null;
  returnYtdPercent: number | null;
  annualizedVolatilityPercent: number | null;
  sharpeRatio: number | null;
  maxDrawdownPercent: number | null;
  avgVolume30D: number | null;
  firstQuoteDate: string | null;
  lastQuoteDate: string | null;
}

export interface PerformanceResponse {
  ticker: string;
  from: string;
  to: string;
  startPrice: number;
  endPrice: number;
  priceReturnPercent: number;
  totalReturnPercent: number;
  annualizedReturnPercent: number;
  dividendPayments: number;
  dividendsTotal: number;
  benchmarks: Array<{
    code: string;
    name: string;
    returnPercent: number;
    available: boolean;
  }>;
}

export interface QuoteQueryOptions {
  from?: string;
  to?: string;
  days?: number;
}

export interface PerformanceQueryOptions {
  from?: string;
  to?: string;
  returnType?: 'price' | 'total';
  includeBenchmarks?: boolean;
}

export interface RealYieldResponse {
  nominalRate: number;
  inflationRate: number;
  realYieldPercent: number;
}

export interface BacktestRequest {
  initialAmount: number;
  monthlyContribution: number;
  allocations: Array<{ ticker: string; weightPercent: number }>;
  benchmark?: string;
  rebalance?: 'none' | 'monthly' | 'quarterly' | 'semiannual' | 'annual';
  from?: string;
  to?: string;
}

export interface BacktestResponse {
  initialCapital: number;
  finalCapital: number;
  totalContributions: number;
  totalReturnPercent: number;
  annualizedReturnPercent: number;
  annualizedVolatilityPercent: number;
  sharpeRatio: number;
  maxDrawdownPercent: number;
  equityCurve: Array<{ date: string; value: number }>;
  benchmarkCurve?: Array<{ date: string; value: number }>;
  benchmarkFinalCapital?: number;
  benchmarkTotalReturnPercent?: number;
  benchmarkAnnualizedReturnPercent?: number;
}

/** Ranked asset row returned by GET /api/v1/assets/rankings. */
export interface AssetRankingDto {
  rank: number;
  ticker: string;
  name: string;
  assetType: string;
  currency: string;
  metricValue: number | null;
  lastPrice: number | null;
  changeDayPercent: number | null;
  return30dPercent: number | null;
  return6mPercent: number | null;
  return12mPercent: number | null;
  returnYtdPercent: number | null;
  annualizedVolatilityPercent: number | null;
  sharpeRatio: number | null;
  maxDrawdownPercent: number | null;
  avgVolume30D: number | null;
  firstQuoteDate: string | null;
  lastQuoteDate: string | null;
}

export const RANKING_METRICS = [
  'retorno12m',
  'retorno30d',
  'retorno6m',
  'retornoano',
  'variacaodia',
  'volatilidade',
  'sharpe',
  'drawdown',
  'volume',
] as const;

export type RankingsMetric = (typeof RANKING_METRICS)[number];

/** Macro indicator snapshot returned by GET /api/v1/assets/market-indicators. */
export interface MarketIndicatorDto {
  code: string;
  name: string;
  latestValue: number;
  latestDate: string;
  accum12mPercent: number | null;
}

/** Single closing point inside a batch sparkline window. */
export interface QuoteSparkPointDto {
  date: string;
  close: number;
}

/** Closing-price window for one ticker (GET /api/v1/assets/quotes/batch). */
export interface AssetQuotesBatchItemDto {
  ticker: string;
  quotes: QuoteSparkPointDto[];
}

const API_BASE_URL =
  process.env.NEXT_PUBLIC_API_URL || (typeof window !== 'undefined' ? '' : 'http://127.0.0.1:5000');

let inMemoryAccessToken: string | null = null;

export function getAccessToken(): string | null {
  return inMemoryAccessToken;
}

export function setAccessToken(token: string | null): void {
  inMemoryAccessToken = token;
}

let refreshPromise: Promise<string | null> | null = null;

export async function performRefresh(): Promise<string | null> {
  if (refreshPromise) {
    return refreshPromise;
  }

  refreshPromise = (async () => {
    try {
      const res = await fetch(`${API_BASE_URL}/api/v1/auth/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
      });

      if (!res.ok) {
        setAccessToken(null);
        return null;
      }

      const data = (await res.json()) as AuthResponse;
      setAccessToken(data.accessToken);
      return data.accessToken;
    } catch {
      setAccessToken(null);
      return null;
    } finally {
      refreshPromise = null;
    }
  })();

  return refreshPromise;
}

export async function fetchWithAuth(
  input: string | URL | Request,
  init?: RequestInit & { skipAuthRefresh?: boolean },
): Promise<Response> {
  const headers = new Headers(init?.headers);
  const token = getAccessToken();
  if (token && !headers.has('Authorization')) {
    headers.set('Authorization', `Bearer ${token}`);
  }

  const fetchInit: RequestInit = {
    ...init,
    headers,
    credentials: 'include',
  };

  let response = await fetch(input, fetchInit);

  if (response.status === 401 && !init?.skipAuthRefresh) {
    const newToken = await performRefresh();
    if (newToken) {
      const retryHeaders = new Headers(init?.headers);
      retryHeaders.set('Authorization', `Bearer ${newToken}`);
      response = await fetch(input, {
        ...init,
        headers: retryHeaders,
        credentials: 'include',
      });
    }
  }

  return response;
}

// ---------------------------------------------------------------------------
// Authentication API Endpoints
// ---------------------------------------------------------------------------

async function readApiError(response: Response, fallback: string): Promise<Error> {
  const payload = (await response.json().catch(() => null)) as {
    message?: unknown;
    title?: unknown;
  } | null;
  const message =
    typeof payload?.message === 'string'
      ? payload.message
      : typeof payload?.title === 'string'
        ? payload.title
        : fallback;
  return new Error(message);
}

export async function signIn(dto: SignInDto): Promise<AuthResponse> {
  const response = await fetch(`${API_BASE_URL}/api/v1/auth/signin`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(dto),
    credentials: 'include',
  });

  if (!response.ok) {
    throw await readApiError(response, `Falha na autenticação: ${response.statusText}`);
  }

  const data = (await response.json()) as AuthResponse;
  setAccessToken(data.accessToken);
  return data;
}

export async function signUp(dto: SignUpDto): Promise<AuthResponse> {
  const response = await fetch(`${API_BASE_URL}/api/v1/auth/signup`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(dto),
    credentials: 'include',
  });

  if (!response.ok) {
    throw await readApiError(response, `Falha no cadastro: ${response.statusText}`);
  }

  const data = (await response.json()) as AuthResponse;
  setAccessToken(data.accessToken);
  return data;
}

export async function signOut(): Promise<void> {
  try {
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), 600);
    await fetch(`${API_BASE_URL}/api/v1/auth/signout`, {
      method: 'POST',
      credentials: 'include',
      signal: controller.signal,
    });
    clearTimeout(timeoutId);
  } catch {
    // Offline or network error - continue local logout
  } finally {
    setAccessToken(null);
  }
}

export async function refreshSession(): Promise<AuthResponse> {
  const res = await fetch(`${API_BASE_URL}/api/v1/auth/refresh`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
  });

  if (!res.ok) {
    setAccessToken(null);
    throw new Error('Sessão expirada. Faça login novamente.');
  }

  const data = (await res.json()) as AuthResponse;
  setAccessToken(data.accessToken);
  return data;
}

export async function getCurrentUser(): Promise<User> {
  const token = getAccessToken();
  if (!token) {
    throw new Error('Não autenticado.');
  }

  const res = await fetchWithAuth(`${API_BASE_URL}/api/v1/auth/me`, { method: 'GET' });

  if (!res.ok) {
    setAccessToken(null);
    throw await readApiError(res, `Falha ao obter usuário: ${res.statusText}`);
  }

  return (await res.json()) as User;
}

// ---------------------------------------------------------------------------
// Market Data & Analytics APIs
// ---------------------------------------------------------------------------

export async function fetchAssets(options?: {
  category?: string;
  search?: string;
}): Promise<AssetDto[]> {
  const params = new URLSearchParams();
  if (options?.category) params.set('category', options.category);
  if (options?.search) params.set('search', options.search);

  const url = `${API_BASE_URL}/api/v1/assets${params.toString() ? `?${params.toString()}` : ''}`;

  const res = await fetch(url, { credentials: 'include', next: { revalidate: 60 } });
  if (!res.ok) throw await readApiError(res, `Falha ao obter ativos: ${res.statusText}`);

  const payload: unknown = await res.json();
  if (Array.isArray(payload)) return payload as AssetDto[];
  if (payload && typeof payload === 'object') {
    const items = (payload as { items?: unknown }).items;
    if (Array.isArray(items)) return items as AssetDto[];
  }
  throw new Error('Resposta inválida do catálogo de ativos.');
}

export async function fetchAssetByTicker(ticker: string): Promise<AssetDto> {
  const normalizedTicker = ticker.trim().toUpperCase();
  const url = `${API_BASE_URL}/api/v1/assets/${encodeURIComponent(normalizedTicker)}`;
  const res = await fetch(url, { credentials: 'include', next: { revalidate: 300 } });
  if (!res.ok) throw await readApiError(res, `Ativo não encontrado: ${normalizedTicker}`);
  return (await res.json()) as AssetDto;
}

/** Fetch the nested market and fiscal sheet from GET /api/v1/assets/{ticker}. */
export async function fetchAssetDetail(ticker: string): Promise<AssetDetailDto> {
  const upperTicker = ticker.toUpperCase();
  const url = `${API_BASE_URL}/api/v1/assets/${encodeURIComponent(upperTicker)}`;
  const res = await fetch(url, { credentials: 'include', next: { revalidate: 300 } });
  if (!res.ok) throw await readApiError(res, `Ativo não encontrado: ${upperTicker}`);
  return (await res.json()) as AssetDetailDto;
}

/** Fetch daily OHLCV data from GET /api/v1/assets/{ticker}/quotes. */
export async function fetchAssetQuotes(
  ticker: string,
  options?: QuoteQueryOptions,
): Promise<QuoteItem[]> {
  const upperTicker = ticker.toUpperCase();
  const params = new URLSearchParams();
  if (options?.from) params.set('from', options.from);
  if (options?.to) params.set('to', options.to);
  if (options?.days !== undefined) params.set('days', String(options.days));
  const query = params.toString();
  const url = `${API_BASE_URL}/api/v1/assets/${encodeURIComponent(upperTicker)}/quotes${query ? `?${query}` : ''}`;

  const res = await fetch(url, { credentials: 'include', next: { revalidate: 900 } });
  if (!res.ok) throw await readApiError(res, `Cotações não encontradas: ${upperTicker}`);
  return (await res.json()) as QuoteItem[];
}

/** Fetch return metrics from GET /api/v1/assets/{ticker}/performance. */
export async function fetchAssetPerformance(
  ticker: string,
  options?: PerformanceQueryOptions,
): Promise<PerformanceResponse> {
  const upperTicker = ticker.toUpperCase();
  const params = new URLSearchParams();
  if (options?.from) params.set('from', options.from);
  if (options?.to) params.set('to', options.to);
  if (options?.returnType) params.set('returnType', options.returnType);
  if (options?.includeBenchmarks !== undefined) {
    params.set('includeBenchmarks', String(options.includeBenchmarks));
  }
  const query = params.toString();
  const url = `${API_BASE_URL}/api/v1/assets/${encodeURIComponent(upperTicker)}/performance${query ? `?${query}` : ''}`;

  const res = await fetch(url, { credentials: 'include', next: { revalidate: 900 } });
  if (!res.ok) throw await readApiError(res, `Desempenho não encontrado: ${upperTicker}`);
  return (await res.json()) as PerformanceResponse;
}

export async function fetchBacktest(request: BacktestRequest): Promise<BacktestResponse> {
  const res = await fetch(`${API_BASE_URL}/api/v1/analytics/backtest`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
    credentials: 'include',
  });

  if (!res.ok) {
    throw await readApiError(res, `Falha ao executar backtest: ${res.statusText}`);
  }

  return (await res.json()) as BacktestResponse;
}

export async function fetchRealYield(
  nominalRate: number,
  inflationRate: number,
): Promise<RealYieldResponse> {
  const url = `${API_BASE_URL}/api/v1/analytics/real-yield?nominalRate=${nominalRate}&inflationRate=${inflationRate}`;
  const res = await fetch(url, { credentials: 'include' });
  if (!res.ok) throw await readApiError(res, `Falha ao calcular juro real: ${res.statusText}`);
  return (await res.json()) as RealYieldResponse;
}

/** Fetch the metric ranking from GET /api/v1/assets/rankings. */ export async function fetchAssetRankings(options?: {
  assetType?: string;
  metric?: string;
  orderDirection?: 'asc' | 'desc';
}): Promise<AssetRankingDto[]> {
  const params = new URLSearchParams();
  if (options?.assetType) params.set('assetType', options.assetType);
  if (options?.metric) params.set('metric', options.metric);
  if (options?.orderDirection) params.set('orderDirection', options.orderDirection);
  const query = params.toString();
  const url = `${API_BASE_URL}/api/v1/assets/rankings${query ? `?${query}` : ''}`;

  const res = await fetch(url, { credentials: 'include', next: { revalidate: 300 } });
  if (!res.ok) throw await readApiError(res, `Falha ao obter rankings: ${res.statusText}`);

  const payload: unknown = await res.json();
  if (Array.isArray(payload)) return payload as AssetRankingDto[];
  if (payload && typeof payload === 'object') {
    const items = (payload as { items?: unknown }).items;
    if (Array.isArray(items)) return items as AssetRankingDto[];
  }
  throw new Error('Resposta inválida do ranking de ativos.');
}

/** Fetch CDI/Selic/IPCA snapshot from GET /api/v1/assets/market-indicators. */
export async function fetchMarketIndicators(): Promise<MarketIndicatorDto[]> {
  const url = `${API_BASE_URL}/api/v1/assets/market-indicators`;
  const res = await fetch(url, { credentials: 'include', next: { revalidate: 3600 } });
  if (!res.ok) throw await readApiError(res, `Falha ao obter indicadores: ${res.statusText}`);
  return (await res.json()) as MarketIndicatorDto[];
}

/**
 * Fetch closing-price windows for several tickers at once
 * (GET /api/v1/assets/quotes/batch), returned as a ticker-keyed map.
 */
export async function fetchQuoteSparks(
  tickers: string[],
  days = 90,
): Promise<Record<string, QuoteSparkPointDto[]>> {
  const cleaned = tickers.map((ticker) => ticker.trim().toUpperCase()).filter(Boolean);
  if (cleaned.length === 0) return {};
  const url = `${API_BASE_URL}/api/v1/assets/quotes/batch?tickers=${encodeURIComponent(cleaned.join(','))}&days=${days}`;
  const res = await fetch(url, { credentials: 'include', next: { revalidate: 900 } });
  if (!res.ok) throw await readApiError(res, `Falha ao obter cotações: ${res.statusText}`);
  const items = (await res.json()) as AssetQuotesBatchItemDto[];
  return Object.fromEntries(items.map((item) => [item.ticker, item.quotes]));
}

/** Raw rate observation of a macro-economic series (percent per period). */
export interface MacroRatePointDto {
  date: string;
  value: number;
}

/** One dividend/income event of an asset (cash rate per quote). */
export interface AssetDividendEventDto {
  comDate: string;
  paymentDate: string | null;
  rate: number;
  type: string;
}

/** Dividend sheet for one asset, with trailing-12-months aggregates. */
export interface AssetDividendsDto {
  ticker: string;
  events: AssetDividendEventDto[];
  totalCount: number;
  last12mTotal: number;
  dividendYield12mPercent: number | null;
}

/** Raw rate window of one macro-economic series, newest last. */
export interface MacroRateSeriesDto {
  code: string;
  name: string;
  points: MacroRatePointDto[];
}

/**
 * Fetch raw CDI/Selic/IPCA rate windows from GET /api/v1/assets/macro-series,
 * used to build accumulated benchmark curves client-side. Returns a code-keyed map.
 */
export async function fetchMacroRateSeries(
  codes: string[],
  days = 3650,
): Promise<Record<string, MacroRatePointDto[]>> {
  const cleaned = codes.map((code) => code.trim().toUpperCase()).filter(Boolean);
  if (cleaned.length === 0) return {};
  const url = `${API_BASE_URL}/api/v1/assets/macro-series?codes=${encodeURIComponent(cleaned.join(','))}&days=${days}`;
  const res = await fetch(url, { credentials: 'include', next: { revalidate: 3600 } });
  if (!res.ok)
    throw await readApiError(res, `Falha ao obter séries de indicadores: ${res.statusText}`);
  const items = (await res.json()) as MacroRateSeriesDto[];
  return Object.fromEntries(items.map((item) => [item.code, item.points]));
}

/**
 * Fetch the dividend sheet from GET /api/v1/assets/{ticker}/dividends.
 * Returns null when the asset has no local dividend history — that is a valid
 * state (most ETF/BDR pilots), not an error.
 */
export async function fetchAssetDividends(ticker: string): Promise<AssetDividendsDto | null> {
  const upperTicker = ticker.trim().toUpperCase();
  if (!upperTicker) return null;
  const url = `${API_BASE_URL}/api/v1/assets/${encodeURIComponent(upperTicker)}/dividends`;
  const res = await fetch(url, { credentials: 'include', next: { revalidate: 1800 } });
  if (res.status === 404) return null;
  if (!res.ok) throw await readApiError(res, `Falha ao obter proventos: ${res.statusText}`);
  return (await res.json()) as AssetDividendsDto;
}

// ---------------------------------------------------------------------------
// Portfolios (dashboard do usuário) — GET/POST /api/v1/portfolios
// ---------------------------------------------------------------------------

/** Carteira retornada por GET /api/v1/portfolios. */
export interface PortfolioDto {
  id: string;
  title: string;
  description: string | null;
  riskProfile: string;
  visibility: 'private' | 'public' | 'link';
  publicValuesMode: 'percent_only' | 'full_values';
  createdAt: string;
}

/** Posição projetada de um par (ativo, corretora). */
export interface PositionDto {
  assetId: string | null;
  ticker: string;
  name: string;
  broker: string;
  quantity: number;
  averagePrice: number;
  investedAmount: number;
  currentPrice: number;
  currentValue: number;
  /** false = sem cotação local; valor atual usa o custo. */
  hasMarketPrice: boolean;
  unrealizedPnl: number;
  realizedPnl: number;
  incomeReceived: number;
  /** Fatia do lucro total (não realizado + realizado + renda) gerada pela posição. */
  contributionPercent: number | null;
}

/** Resumo completo de GET /api/v1/portfolios/{id}. */
export interface PortfolioSummaryDto {
  portfolio: PortfolioDto;
  totalValue: number;
  totalInvested: number;
  unrealizedPnl: number;
  realizedPnl: number;
  incomeReceived: number;
  positions: PositionDto[];
}

export interface TransactionDto {
  id: string;
  portfolioId: string;
  assetId: string | null;
  syntheticIndexCode: string | null;
  type: 'BUY' | 'SELL' | 'INCOME' | 'CORP_ACTION' | 'TRANSFER_IN' | 'TRANSFER_OUT';
  broker: string;
  quantity: number | null;
  unitPrice: number | null;
  grossAmount: number;
  fees: number;
  fxRate: number | null;
  currency: string;
  tradeDate: string;
  maturityDate: string | null;
  corpActionJson: string | null;
  notes: string | null;
  isAmendment: boolean;
}

export interface PagedTransactionsDto {
  items: TransactionDto[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface CreatePortfolioInput {
  title: string;
  description?: string;
  riskProfile?: 'conservador' | 'moderado' | 'arrojado';
}

export interface CreateTransactionInput {
  type: TransactionDto['type'];
  assetId?: string | null;
  syntheticIndexCode?: string | null;
  broker: string;
  grossAmount?: number;
  fees?: number;
  quantity?: number;
  unitPrice?: number;
  currency?: string;
  tradeDate?: string;
  maturityDate?: string;
  corpActionJson?: string;
  notes?: string;
}

function portfolioApiError(res: Response, fallback: string): Promise<Error> {
  return readApiError(res, fallback);
}

export async function fetchPortfolios(): Promise<PortfolioDto[]> {
  const res = await fetchWithAuth(`${API_BASE_URL}/api/v1/portfolios`);
  if (!res.ok) throw await portfolioApiError(res, `Falha ao obter carteiras: ${res.statusText}`);
  return (await res.json()) as PortfolioDto[];
}

export async function fetchPortfolioSummary(id: string): Promise<PortfolioSummaryDto> {
  const res = await fetchWithAuth(`${API_BASE_URL}/api/v1/portfolios/${id}`);
  if (!res.ok) throw await portfolioApiError(res, `Falha ao obter carteira: ${res.statusText}`);
  return (await res.json()) as PortfolioSummaryDto;
}

export async function createPortfolio(input: CreatePortfolioInput): Promise<PortfolioDto> {
  const res = await fetchWithAuth(`${API_BASE_URL}/api/v1/portfolios`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input),
  });
  if (!res.ok) throw await portfolioApiError(res, 'Falha ao criar carteira.');
  return (await res.json()) as PortfolioDto;
}

export async function deletePortfolio(id: string): Promise<void> {
  const res = await fetchWithAuth(`${API_BASE_URL}/api/v1/portfolios/${id}`, { method: 'DELETE' });
  if (!res.ok && res.status !== 204)
    throw await portfolioApiError(res, 'Falha ao excluir carteira.');
}

export async function createTransaction(
  portfolioId: string,
  input: CreateTransactionInput,
): Promise<TransactionDto> {
  const res = await fetchWithAuth(`${API_BASE_URL}/api/v1/portfolios/${portfolioId}/transactions`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input),
  });
  if (!res.ok) throw await portfolioApiError(res, 'Falha ao lançar transação.');
  return (await res.json()) as TransactionDto;
}

/** Resultado compacto de GET /api/v1/portfolios/lookup/{ticker}. */
export interface AssetLookupDto {
  id: string;
  ticker: string;
  name: string;
  assetType: string;
  currency: string;
}

export async function fetchAssetLookup(ticker: string): Promise<AssetLookupDto | null> {
  const normalized = ticker.trim().toUpperCase();
  if (!normalized) return null;
  const res = await fetchWithAuth(
    `${API_BASE_URL}/api/v1/portfolios/lookup/${encodeURIComponent(normalized)}`,
  );
  if (res.status === 404) return null;
  if (!res.ok) throw await portfolioApiError(res, 'Falha ao buscar ativo no catálogo.');
  return (await res.json()) as AssetLookupDto;
}

/** Ponto da série diária do patrimônio (GET /portfolios/{id}/performance). */
export interface PerformancePointDto {
  date: string;
  value: number;
  externalFlow: number;
}

export interface BenchmarkSeriesDto {
  code: 'CDI' | 'SELIC' | 'IPCA' | 'IBOV' | string;
  normalizedValues: number[];
}

export interface PerformanceResultDto {
  from: string;
  to: string;
  series: PerformancePointDto[];
  totalReturnPercent: number;
  twrPercentPeriod: number;
  mwrPercentAnnualized: number | null;
  volatilityPercentAnnualized: number;
  sharpeRatio: number;
  maxDrawdownPercent: number;
  benchmarks: BenchmarkSeriesDto[];
}

export async function fetchPortfolioPerformance(
  portfolioId: string,
  options?: { from?: string; to?: string; benchmarks?: string },
): Promise<PerformanceResultDto> {
  const params = new URLSearchParams();
  if (options?.from) params.set('from', options.from);
  if (options?.to) params.set('to', options.to);
  if (options?.benchmarks) params.set('benchmarks', options.benchmarks);

  const query = params.toString() ? `?${params.toString()}` : '';
  const res = await fetchWithAuth(
    `${API_BASE_URL}/api/v1/portfolios/${portfolioId}/performance${query}`,
  );
  if (!res.ok) throw await portfolioApiError(res, 'Falha ao calcular a rentabilidade da carteira.');
  return (await res.json()) as PerformanceResultDto;
}

// ---------------------------------------------------------------------------
// Renda fixa (M-P3) — parâmetros por posição + timeline de vencimentos
// ---------------------------------------------------------------------------

export interface FixedIncomeParamDto {
  id: string;
  assetId: string | null;
  syntheticIndexCode: string | null;
  indexer: 'CDI_PERCENT' | 'CDI_PLUS' | 'SELIC' | 'IPCA_PLUS' | 'PREFIXED';
  indexerRate: number;
  principal: number;
  startDate: string;
  maturityDate: string;
  liquidity: string;
  taxRegime: string;
  accruedValue: number;
  lastAccrualDate: string | null;
}

export interface AttachFixedIncomeInput {
  assetId?: string | null;
  syntheticIndexCode?: string | null;
  indexer: FixedIncomeParamDto['indexer'];
  indexerRate: number;
  principal: number;
  startDate: string;
  maturityDate: string;
  liquidity?: string;
  taxRegime?: string;
}

export interface TimelineItemDto {
  date: string;
  label: string;
  kind: 'maturity' | 'liquidity';
  amount: number;
}

export async function attachFixedIncome(
  portfolioId: string,
  input: AttachFixedIncomeInput,
): Promise<FixedIncomeParamDto> {
  const res = await fetchWithAuth(`${API_BASE_URL}/api/v1/portfolios/${portfolioId}/fixed-income`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input),
  });
  if (!res.ok) throw await portfolioApiError(res, 'Falha ao salvar parâmetros de renda fixa.');
  return (await res.json()) as FixedIncomeParamDto;
}

export async function fetchPortfolioTimeline(portfolioId: string): Promise<TimelineItemDto[]> {
  const res = await fetchWithAuth(`${API_BASE_URL}/api/v1/portfolios/${portfolioId}/timeline`);
  if (!res.ok) throw await portfolioApiError(res, 'Falha ao obter vencimentos.');
  return (await res.json()) as TimelineItemDto[];
}

// ---------------------------------------------------------------------------
// Fiscal (M-P4) — simulador de resgate e projeção DARF (educacional)
// ---------------------------------------------------------------------------

export interface RedemptionResultDto {
  grossAmount: number;
  costBasis: number;
  profit: number;
  irPercent: number;
  irAmount: number;
  iofAmount: number;
  netAmount: number;
  exemptApplied: boolean;
  exemptReason: string | null;
  comeCotasAlreadyPaid: number;
  quantityAvailable: number;
  priceDate: string;
  premises: string[];
  disclaimer: string;
}

export async function simulateRedemption(
  portfolioId: string,
  input: { assetId: string; broker?: string; quantity?: number },
): Promise<RedemptionResultDto> {
  const res = await fetchWithAuth(
    `${API_BASE_URL}/api/v1/portfolios/${portfolioId}/tax/redemption-simulation`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(input),
    },
  );
  if (!res.ok) throw await portfolioApiError(res, 'Falha ao simular o resgate.');
  return (await res.json()) as RedemptionResultDto;
}

export interface DarfProjectionItemDto {
  assetClass: string;
  realizedPnl: number;
  taxDue: number;
  darfCode: string;
  dueDate: string;
}

export interface TaxProjectionDto {
  year: number;
  month: number;
  items: DarfProjectionItemDto[];
  premises: string[];
  disclaimer: string;
}

export async function fetchTaxProjection(
  portfolioId: string,
  year?: number,
  month?: number,
): Promise<TaxProjectionDto> {
  const params = new URLSearchParams();
  if (year) params.set('year', String(year));
  if (month) params.set('month', String(month));
  const query = params.toString() ? `?${params.toString()}` : '';
  const res = await fetchWithAuth(
    `${API_BASE_URL}/api/v1/portfolios/${portfolioId}/tax-projection${query}`,
  );
  if (!res.ok) throw await portfolioApiError(res, 'Falha ao projetar o DARF.');
  return (await res.json()) as TaxProjectionDto;
}

// ---------------------------------------------------------------------------
// Carteira pública / compartilhamento (M-P5)
// ---------------------------------------------------------------------------

export interface PublicAllocationClassDto {
  assetClass: string;
  percent: number;
}

export interface PublicPositionDto {
  ticker: string;
  name: string;
  weightPercent: number;
  returnPercent: number;
  currentValue: number | null;
  investedAmount: number | null;
}

export interface PublicPortfolioDto {
  title: string;
  description: string | null;
  riskProfile: string;
  identityLabel: string;
  valuesMode: 'percent_only' | 'full_values';
  allocationPercent: PublicAllocationClassDto[];
  totalReturnPercent: number;
  periodReturnPercent: number | null;
  positions: PublicPositionDto[];
  createdAt: string;
}

export async function fetchPublicPortfolio(
  slug: string,
  shareToken?: string,
): Promise<PublicPortfolioDto | null> {
  const params = new URLSearchParams();
  if (shareToken) params.set('shareToken', shareToken);
  const query = params.toString() ? `?${params.toString()}` : '';
  const url = `${API_BASE_URL}/api/v1/portfolios/public/${encodeURIComponent(slug)}${query}`;
  const res = await fetch(url, { next: { revalidate: 300 } });
  if (res.status === 404) return null;
  if (!res.ok) throw new Error('Falha ao carregar a carteira pública.');
  return (await res.json()) as PublicPortfolioDto;
}

export async function clonePublicPortfolio(slug: string): Promise<string> {
  const res = await fetchWithAuth(
    `${API_BASE_URL}/api/v1/portfolios/public/${encodeURIComponent(slug)}/clone`,
    { method: 'POST' },
  );
  if (!res.ok) throw await portfolioApiError(res, 'Falha ao importar a carteira.');
  const body = (await res.json()) as { id: string };
  return body.id;
}

export interface VisibilityResultDto {
  portfolioId: string;
  visibility: 'private' | 'public' | 'link';
  slug: string | null;
  hasShareToken: boolean;
  shareExpiresAt: string | null;
}

export async function updateVisibility(
  portfolioId: string,
  visibility: 'private' | 'public' | 'link',
  expiresInDays?: number,
): Promise<VisibilityResultDto> {
  const res = await fetchWithAuth(`${API_BASE_URL}/api/v1/portfolios/${portfolioId}/visibility`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ visibility, expiresInDays }),
  });
  if (!res.ok) throw await portfolioApiError(res, 'Falha ao alterar visibilidade.');
  return (await res.json()) as VisibilityResultDto;
}

/** Token claro retornado UMA única vez pelo backend. */
export interface ShareLinkResultDto extends VisibilityResultDto {
  shareToken: string;
  shareUrl: string;
}

export async function regenerateShareLink(
  portfolioId: string,
  expiresInDays = 30,
): Promise<ShareLinkResultDto> {
  const res = await fetchWithAuth(
    `${API_BASE_URL}/api/v1/portfolios/${portfolioId}/share-link/regenerate`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ expiresInDays }),
    },
  );
  if (!res.ok) throw await portfolioApiError(res, 'Falha ao gerar o link.');
  return (await res.json()) as ShareLinkResultDto;
}

export async function revokeShareLink(portfolioId: string): Promise<void> {
  const res = await fetchWithAuth(`${API_BASE_URL}/api/v1/portfolios/${portfolioId}/share-link`, {
    method: 'DELETE',
  });
  if (!res.ok && res.status !== 204) throw await portfolioApiError(res, 'Falha ao revogar o link.');
}

// ---------------------------------------------------------------------------
// Metas (M-P5) — multi-metas com progresso e projeção
// ---------------------------------------------------------------------------

export interface GoalDto {
  id: string;
  portfolioId: string;
  kind: 'TARGET_AMOUNT' | 'TARGET_RETURN_PCT' | 'TARGET_DATE';
  targetValue: number | null;
  targetPct: number | null;
  targetDate: string | null;
  monthlyContribution: number | null;
  assumedAnnualRate: number | null;
  status: 'active' | 'achieved' | 'cancelled';
  createdAt: string;
}

export interface GoalProgressDto {
  goal: GoalDto;
  currentValue: number;
  progressPercent: number | null;
}

export interface CreateGoalInput {
  kind: GoalDto['kind'];
  targetValue?: number;
  targetPct?: number;
  targetDate?: string;
  monthlyContribution?: number;
  assumedAnnualRate?: number;
}

export async function fetchGoals(
  portfolioId: string,
  currentValue: number,
): Promise<GoalProgressDto[]> {
  const res = await fetchWithAuth(
    `${API_BASE_URL}/api/v1/portfolios/${portfolioId}/goals?currentValue=${currentValue}`,
  );
  if (!res.ok) throw await portfolioApiError(res, 'Falha ao obter as metas.');
  return (await res.json()) as GoalProgressDto[];
}

export async function createGoal(portfolioId: string, input: CreateGoalInput): Promise<GoalDto> {
  const res = await fetchWithAuth(`${API_BASE_URL}/api/v1/portfolios/${portfolioId}/goals`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input),
  });
  if (!res.ok) throw await portfolioApiError(res, 'Falha ao criar a meta.');
  return (await res.json()) as GoalDto;
}

export async function deleteGoal(portfolioId: string, goalId: string): Promise<void> {
  const res = await fetchWithAuth(
    `${API_BASE_URL}/api/v1/portfolios/${portfolioId}/goals/${goalId}`,
    { method: 'DELETE' },
  );
  if (!res.ok && res.status !== 204) throw await portfolioApiError(res, 'Falha ao remover a meta.');
}
