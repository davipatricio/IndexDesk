import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import * as React from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { PortfoliosListTable } from '../portfolios-list-table';
import { usePrivacyStore } from '@/stores/privacy-store';

// Mock nuqs
const mockNuqsState: Record<string, string> = { q: '', sort: 'value', dir: 'desc' };
vi.mock('nuqs', () => ({
  useQueryState: (key: string) => [
    mockNuqsState[key] ?? '',
    (val: string | ((prev: string) => string)) => {
      if (typeof val === 'function') {
        mockNuqsState[key] = val(mockNuqsState[key] ?? '');
      } else {
        mockNuqsState[key] = val;
      }
      return Promise.resolve(new URLSearchParams());
    },
  ],
  parseAsString: {
    withDefault: (def: string) => ({
      parse: (v: string | null) => v ?? def,
    }),
  },
  parseAsStringLiteral: (values: readonly string[]) => ({
    withDefault: (def: string) => ({
      parse: (v: string | null) => (values.includes(v as string) ? v : def),
    }),
  }),
}));

// Mock next/navigation
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: vi.fn(),
    replace: vi.fn(),
    back: vi.fn(),
  }),
  useSearchParams: () => new URLSearchParams(),
}));

// Mock session hook
vi.mock('@/hooks/use-session', () => ({
  useSession: () => ({
    isAuthenticated: true,
    isReady: true,
    user: { id: 'u1', email: 'test@example.com' },
  }),
}));

// Mock usePortfoliosList
const mockPortfoliosData = [
  {
    id: 'p-1',
    title: 'Carteira Alpha',
    description: 'Foco em dividendos',
    riskProfile: 'moderado',
    visibility: 'private' as const,
    publicValuesMode: 'percent_only' as const,
    createdAt: '2026-01-01',
    series30d: [1000, 1050, 1100],
    returnPercentMonth: 4.76,
    returnPercentTotal: 10.0,
  },
  {
    id: 'p-2',
    title: 'Carteira Beta',
    description: 'Reserva de oportunidade',
    riskProfile: 'conservador',
    visibility: 'public' as const,
    publicValuesMode: 'full_values' as const,
    createdAt: '2026-02-01',
    series30d: [2000, 1950, 1900],
    returnPercentMonth: -5.0,
    returnPercentTotal: -5.0,
  },
];

let mockListState = {
  portfolios: mockPortfoliosData as typeof mockPortfoliosData | undefined,
  isLoading: false,
  isError: false,
  refetch: vi.fn(),
};

vi.mock('@/hooks/use-portfolios-list', () => ({
  usePortfoliosList: () => mockListState,
  portfolioHue: () => 180,
  portfolioAvatarStyle: () => ({
    backgroundColor: 'hsl(180 65% 45% / 0.15)',
    color: 'hsl(180 65% 45%)',
  }),
  portfolioInitial: (title: string) => (title[0] ?? 'C').toUpperCase(),
}));

function renderWithClient(ui: React.ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
}

describe('PortfoliosListTable', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    usePrivacyStore.getState().reset();
    usePrivacyStore.getState().initFromPreferences({ hideValues: false });
    mockNuqsState.q = '';
    mockNuqsState.sort = 'value';
    mockNuqsState.dir = 'desc';
    mockListState = {
      portfolios: mockPortfoliosData,
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    };
  });

  it('renderiza as carteiras na tabela desktop e nos cards mobile', () => {
    renderWithClient(<PortfoliosListTable />);
    expect(screen.getAllByText('Carteira Alpha').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Carteira Beta').length).toBeGreaterThan(0);
  });

  it('aplica mascaramento quando hideValues é true no privacy store', () => {
    usePrivacyStore.getState().setHideValues(true);
    renderWithClient(<PortfoliosListTable />);
    const maskedElements = screen.getAllByText('••••');
    expect(maskedElements.length).toBeGreaterThan(0);
  });

  it('renderiza skeleton quando está carregando', () => {
    mockListState = {
      portfolios: undefined,
      isLoading: true,
      isError: false,
      refetch: vi.fn(),
    };
    const { container } = renderWithClient(<PortfoliosListTable />);
    expect(container.querySelector('[aria-busy="true"]')).toBeInTheDocument();
  });

  it('renderiza empty ghost state quando não há carteiras', () => {
    mockListState = {
      portfolios: [],
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    };
    renderWithClient(<PortfoliosListTable />);
    expect(screen.getByText('Crie sua primeira carteira')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /criar primeira carteira/i })).toBeInTheDocument();
  });

  it('renderiza erro com botão retry quando isError é true', () => {
    const refetchMock = vi.fn();
    mockListState = {
      portfolios: undefined,
      isLoading: false,
      isError: true,
      refetch: refetchMock,
    };
    renderWithClient(<PortfoliosListTable />);
    expect(screen.getByText('Erro ao carregar carteiras')).toBeInTheDocument();
    const retryBtn = screen.getByRole('button', { name: /tentar novamente/i });
    fireEvent.click(retryBtn);
    expect(refetchMock).toHaveBeenCalled();
  });
});
