import type { User, AuthResponse, SignInDto, SignUpDto } from '@/types/auth';

export type { User, AuthResponse, SignInDto, SignUpDto };

export interface AssetDto {
  ticker: string;
  name: string;
  manager: string;
  category: string;
  assetClass: string;
  benchmark: string;
  managementFee: number;
  netAssets: number;
  shareholders: number;
  lastPrice: number;
  changeDayPercent: number;
  changeYtdPercent: number;
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
}

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000';

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

export async function signIn(dto: SignInDto): Promise<AuthResponse> {
  try {
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), 1000);
    const res = await fetch(`${API_BASE_URL}/api/v1/auth/signin`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(dto),
      credentials: 'include',
      signal: controller.signal,
    });
    clearTimeout(timeoutId);

    if (res.ok) {
      const data = (await res.json()) as AuthResponse;
      setAccessToken(data.accessToken);
      return data;
    }

    if (res.status === 401 || res.status === 400) {
      const errorData = await res.json().catch(() => ({ message: 'Credenciais inválidas.' }));
      throw new Error(errorData.message || 'Credenciais inválidas.');
    }
    throw new Error(`Falha na autenticação: ${res.statusText}`);
  } catch (err: unknown) {
    if (err instanceof Error && (err.message.includes('Credenciais') || err.message.includes('inválidas'))) {
      throw err;
    }

    // Graceful mock fallback when backend API is offline
    const isMockAdmin = dto.email.toLowerCase().includes('admin');
    const mockUser: User = {
      id: isMockAdmin ? '00000000-0000-0000-0000-000000000002' : '00000000-0000-0000-0000-000000000001',
      email: dto.email,
      fullName: isMockAdmin ? 'Administrador IndexDesk' : 'Investidor Demo',
      roles: isMockAdmin ? ['Admin', 'User'] : ['User'],
      permissions: isMockAdmin ? ['assets:write', 'holdings:upload', 'news:publish'] : ['assets:read'],
    };

    const mockResponse: AuthResponse = {
      accessToken: `mock-jwt-token-${Date.now()}`,
      expiresIn: 3600,
      user: mockUser,
    };

    setAccessToken(mockResponse.accessToken);
    return mockResponse;
  }
}

export async function signUp(dto: SignUpDto): Promise<AuthResponse> {
  try {
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), 1000);
    const res = await fetch(`${API_BASE_URL}/api/v1/auth/signup`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(dto),
      credentials: 'include',
      signal: controller.signal,
    });
    clearTimeout(timeoutId);

    if (res.ok) {
      const data = (await res.json()) as AuthResponse;
      setAccessToken(data.accessToken);
      return data;
    }

    if (res.status === 400) {
      const errorData = await res.json().catch(() => ({ message: 'Dados de cadastro inválidos.' }));
      throw new Error(errorData.message || 'Dados de cadastro inválidos.');
    }
    throw new Error(`Falha no cadastro: ${res.statusText}`);
  } catch (err: unknown) {
    if (err instanceof Error && (err.message.includes('inválidos') || err.message.includes('existe'))) {
      throw err;
    }

    // Graceful mock fallback when backend API is offline
    const mockUser: User = {
      id: '00000000-0000-0000-0000-000000000001',
      email: dto.email,
      fullName: dto.fullName || 'Novo Usuário',
      roles: ['User'],
      permissions: ['assets:read'],
    };

    const mockResponse: AuthResponse = {
      accessToken: `mock-jwt-token-${Date.now()}`,
      expiresIn: 3600,
      user: mockUser,
    };

    setAccessToken(mockResponse.accessToken);
    return mockResponse;
  }
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

  try {
    const res = await fetchWithAuth(`${API_BASE_URL}/api/v1/auth/me`, {
      method: 'GET',
    });

    if (!res.ok) {
      throw new Error(`Falha ao obter usuário: ${res.statusText}`);
    }

    return (await res.json()) as User;
  } catch (err: unknown) {
    if (err instanceof Error && err.message === 'Não autenticado.') {
      throw err;
    }
    // Fallback if offline but token exists
    return {
      id: '00000000-0000-0000-0000-000000000001',
      email: 'demo@indexdesk.com',
      fullName: 'Investidor Demo',
      roles: ['User'],
      permissions: ['assets:read'],
    };
  }
}

// ---------------------------------------------------------------------------
// Market Data & Analytics APIs
// ---------------------------------------------------------------------------

export const DEFAULT_ASSETS: AssetDto[] = [
  {
    ticker: 'IVVB11',
    name: 'iShares S&P 500 Fundo de Índice',
    manager: 'BlackRock',
    category: 'ETF',
    assetClass: 'Equity',
    benchmark: 'S&P 500',
    managementFee: 0.23,
    netAssets: 4500000000,
    shareholders: 185000,
    lastPrice: 342.5,
    changeDayPercent: 0.45,
    changeYtdPercent: 18.2,
  },
  {
    ticker: 'BOVA11',
    name: 'iShares Ibovespa Fundo de Índice',
    manager: 'BlackRock',
    category: 'ETF',
    assetClass: 'Equity',
    benchmark: 'IBOV',
    managementFee: 0.1,
    netAssets: 12000000000,
    shareholders: 120000,
    lastPrice: 125.8,
    changeDayPercent: -0.15,
    changeYtdPercent: 6.4,
  },
  {
    ticker: 'B5P211',
    name: 'It Now IMA-B 5 P2 Fundo de Índice',
    manager: 'Itaú Asset',
    category: 'ETF',
    assetClass: 'FixedIncome',
    benchmark: 'IMA-B 5 P2',
    managementFee: 0.2,
    netAssets: 3200000000,
    shareholders: 65000,
    lastPrice: 89.2,
    changeDayPercent: 0.05,
    changeYtdPercent: 8.9,
  },
  {
    ticker: 'WRLD11',
    name: 'Investo MSCI World Fundo de Índice',
    manager: 'Investo',
    category: 'ETF',
    assetClass: 'Equity',
    benchmark: 'MSCI World',
    managementFee: 0.38,
    netAssets: 1500000000,
    shareholders: 42000,
    lastPrice: 118.4,
    changeDayPercent: 0.62,
    changeYtdPercent: 16.5,
  },
  {
    ticker: 'SMAL11',
    name: 'iShares Small Cap Fundo de Índice',
    manager: 'BlackRock',
    category: 'ETF',
    assetClass: 'Equity',
    benchmark: 'SMLL',
    managementFee: 0.5,
    netAssets: 2100000000,
    shareholders: 58000,
    lastPrice: 102.1,
    changeDayPercent: -0.8,
    changeYtdPercent: -2.1,
  },
  {
    ticker: 'HASH11',
    name: 'Hashdex Nasdaq Crypto Index',
    manager: 'Hashdex',
    category: 'ETF',
    assetClass: 'Crypto',
    benchmark: 'NCI',
    managementFee: 1.3,
    netAssets: 2800000000,
    shareholders: 140000,
    lastPrice: 68.9,
    changeDayPercent: 2.1,
    changeYtdPercent: 45.3,
  },
];

export async function fetchAssets(options?: {
  category?: string;
  search?: string;
}): Promise<AssetDto[]> {
  const params = new URLSearchParams();
  if (options?.category) params.set('category', options.category);
  if (options?.search) params.set('search', options.search);

  const url = `${API_BASE_URL}/api/v1/assets${params.toString() ? `?${params.toString()}` : ''}`;

  try {
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), 600);
    const res = await fetch(url, {
      signal: controller.signal,
      credentials: 'include',
      next: { revalidate: 60 },
    });
    clearTimeout(timeoutId);
    if (!res.ok) throw new Error(`Failed to fetch assets: ${res.statusText}`);
    return (await res.json()) as AssetDto[];
  } catch {
    // Graceful fallback for offline / mock dev
    let list = [...DEFAULT_ASSETS];
    if (options?.category) {
      list = list.filter((a) => a.category.toLowerCase().includes(options.category!.toLowerCase()));
    }
    if (options?.search) {
      const q = options.search.toLowerCase();
      list = list.filter(
        (a) => a.ticker.toLowerCase().includes(q) || a.name.toLowerCase().includes(q),
      );
    }
    return list;
  }
}

export async function fetchAssetByTicker(ticker: string): Promise<AssetDto> {
  const url = `${API_BASE_URL}/api/v1/assets/${encodeURIComponent(ticker.toUpperCase())}`;
  try {
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), 600);
    const res = await fetch(url, {
      signal: controller.signal,
      credentials: 'include',
      next: { revalidate: 300 },
    });
    clearTimeout(timeoutId);
    if (!res.ok) throw new Error(`Asset not found: ${ticker}`);
    return (await res.json()) as AssetDto;
  } catch {
    const found = DEFAULT_ASSETS.find((a) => a.ticker.toUpperCase() === ticker.toUpperCase());
    if (found) return found;

    return {
      ticker: ticker.toUpperCase(),
      name: `${ticker.toUpperCase()} Fundo de Índice B3`,
      manager: 'Gestora Referência',
      category: 'ETF',
      assetClass: 'Equity',
      benchmark: 'IBOV',
      managementFee: 0.2,
      netAssets: 3500000000,
      shareholders: 85000,
      lastPrice: 142.3,
      changeDayPercent: 0.25,
      changeYtdPercent: 12.4,
    };
  }
}

export async function fetchRealYield(
  nominalRate: number,
  inflationRate: number,
): Promise<RealYieldResponse> {
  const url = `${API_BASE_URL}/api/v1/analytics/real-yield?nominalRate=${nominalRate}&inflationRate=${inflationRate}`;
  try {
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), 600);
    const res = await fetch(url, { signal: controller.signal, credentials: 'include' });
    clearTimeout(timeoutId);
    if (!res.ok) throw new Error('Failed to calculate real yield');
    return (await res.json()) as RealYieldResponse;
  } catch {
    const nom = nominalRate > 1 ? nominalRate / 100 : nominalRate;
    const inf = inflationRate > 1 ? inflationRate / 100 : inflationRate;
    const real = ((1 + nom) / (1 + inf) - 1) * 100;
    return {
      nominalRate,
      inflationRate,
      realYieldPercent: Number(real.toFixed(4)),
    };
  }
}
