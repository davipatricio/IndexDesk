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
