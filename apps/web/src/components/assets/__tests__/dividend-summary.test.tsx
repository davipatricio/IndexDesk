import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';

import { DividendSummary } from '@/components/assets/dividend-summary';
import type { AssetDividendsDto } from '@/lib/api-client';

const DATA: AssetDividendsDto = {
  ticker: 'MXRF11',
  totalCount: 54,
  last12mTotal: 0.62,
  dividendYield12mPercent: 10.4,
  events: Array.from({ length: 12 }, (_, index) => ({
    comDate: `2026-${String(index + 1).padStart(2, '0')}-15`,
    paymentDate: null,
    rate: 0.005 + index / 1000,
    type: 'Rendimento',
  })),
};

describe('DividendSummary', () => {
  it('shows the 12m cash total, DY and payment count', () => {
    render(<DividendSummary data={DATA} />);

    expect(screen.getByText('Proventos')).toBeInTheDocument();
    expect(screen.getByText('R$ 0,62')).toBeInTheDocument();
    expect(screen.getByText('10,40%')).toBeInTheDocument();
    expect(screen.getByText('54')).toBeInTheDocument();
  });

  it('lists only the most recent payments inside the collapsible table', () => {
    render(<DividendSummary data={DATA} />);

    expect(screen.getByText(/8 pagamentos mais recentes de 54/)).toBeInTheDocument();
    expect(screen.getAllByText('Rendimento')).toHaveLength(8);
  });
});
