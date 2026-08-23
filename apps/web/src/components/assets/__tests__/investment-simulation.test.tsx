import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { InvestmentSimulation } from '@/components/assets/investment-simulation';
import type { SeriesPoint } from '@/lib/simulation';

vi.mock('@/lib/api-client', async (importOriginal) => {
  const original = await importOriginal<typeof import('@/lib/api-client')>();
  return {
    ...original,
    fetchQuoteSparks: vi.fn(async () => ({
      IBOV: [
        { date: '2025-01-02', close: 120 },
        { date: '2026-07-01', close: 140 },
      ],
      IFIX: [
        { date: '2025-06-01', close: 3600 },
        { date: '2026-08-20', close: 3682 },
      ],
    })),
    fetchMacroRateSeries: vi.fn(async () => ({
      CDI: [
        { date: '2025-01-02', value: 0.05 },
        { date: '2026-07-01', value: 0.05 },
      ],
    })),
  };
});

const ASSET_CLOSES: SeriesPoint[] = [
  { date: '2025-01-01', value: 10 },
  { date: '2025-07-01', value: 11 },
  { date: '2026-07-01', value: 14 },
];

function renderSection(assetClass = 'FII') {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <InvestmentSimulation ticker="TEST11" closes={ASSET_CLOSES} assetClass={assetClass} />
    </QueryClientProvider>,
  );
}

describe('InvestmentSimulation', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders the asset summary column with the normalized final value', async () => {
    renderSection();

    // Default window is 1A: anchor close = 11, final = 14 → R$1.272,73 (+27,27%).
    expect(await screen.findByText('R$ 1.272,73')).toBeInTheDocument();
    expect(screen.getByText(/27,27%/)).toBeInTheDocument();
    expect(screen.getByText(/educativa/i)).toBeInTheDocument();
  });

  it('hides IFIX by default for ETFs and shows it for FIIs', async () => {
    const fii = renderSection('FII');
    await fii.findByText('R$ 1.272,73');
    const ifixColumnFii = await fii.findByText('IFIX');
    expect(ifixColumnFii.closest('button')).toHaveAttribute('aria-pressed', 'true');

    fii.unmount();
    const etf = renderSection('ETF');
    await etf.findByText('R$ 1.272,73');
    // Column exists in both (all benchmarks are computed), but the ETF one
    // starts toggled off (dimmed).
    const ifixColumnEtf = await etf.findByText('IFIX');
    expect(ifixColumnEtf.closest('button')).toHaveAttribute('aria-pressed', 'false');
  });

  it('toggles a benchmark line when its column is clicked', async () => {
    const user = userEvent.setup();
    const view = renderSection('FII');
    await view.findByText('R$ 1.272,73');

    // IBOV starts toggled off on FIIs; clicking the column turns the line on.
    const ibovToggle = await view.findByTitle('Mostrar Ibovespa');
    expect(ibovToggle).toHaveAttribute('aria-pressed', 'false');
    await user.click(ibovToggle);

    expect(await view.findByTitle('Ocultar Ibovespa')).toHaveAttribute('aria-pressed', 'true');
  });

  it('notes the inception fallback for young assets', async () => {
    const youngCloses: SeriesPoint[] = [
      { date: '2026-06-01', value: 10 },
      { date: '2026-08-01', value: 12 },
    ];
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={queryClient}>
        <InvestmentSimulation ticker="NOVO11" closes={youngCloses} assetClass="BDR_ETF" />
      </QueryClientProvider>,
    );

    expect(await screen.findByText(/início do histórico local/)).toBeInTheDocument();
  });
});
