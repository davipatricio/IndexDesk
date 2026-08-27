import * as React from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { PositionsTable } from '../positions-table';
import { usePrivacyStore } from '@/stores/privacy-store';
import { MASKED_TEXT } from '@/components/privacy/masked-value';

const positions = [
  {
    assetId: '1',
    ticker: 'BOVA11',
    name: 'IShares Ibov',
    broker: 'XP',
    quantity: 100,
    averagePrice: 98.5,
    investedAmount: 9850,
    currentPrice: 120.0,
    currentValue: 12000,
    hasMarketPrice: true,
    unrealizedPnl: 2150,
    realizedPnl: 0,
    incomeReceived: 0,
    contributionPercent: 30.5,
  },
];

describe('PositionsTable privacy', () => {
  let qc: QueryClient;

  beforeEach(() => {
    qc = new QueryClient();
    usePrivacyStore.getState().reset();
  });

  const wrap = (ui: React.ReactNode) => <QueryClientProvider client={qc}>{ui}</QueryClientProvider>;

  it('exibe valores financeiros quando hideValues=false', () => {
    usePrivacyStore.getState().initFromPreferences({ hideValues: false });
    render(wrap(<PositionsTable positions={positions} />));
    // algum valor real deve aparecer (ex.: 100 quantidade, R$ formatado)
    expect(screen.getByText('BOVA11')).toBeInTheDocument();
    expect(screen.getByText('100')).toBeInTheDocument();
    expect(screen.queryByText(MASKED_TEXT)).toBeNull();
  });

  it('mascara celulares monetários e rent quando hideValues=true, peso fica visível', () => {
    usePrivacyStore.getState().initFromPreferences({ hideValues: true });
    render(wrap(<PositionsTable positions={positions} />));
    // maskedValue repete por coluna — vários bullets
    const bullets = screen.getAllByText(MASKED_TEXT);
    expect(bullets.length).toBeGreaterThanOrEqual(4);
    // peso % (12% do total → composição) continua visível
    // peso = 12000/12000=100% para uma posição, mas format é *.0%
    expect(screen.getByText(/100\.0%/)).toBeInTheDocument();
    // ticker/broker permanecem
    expect(screen.getByText('BOVA11')).toBeInTheDocument();
  });
});
