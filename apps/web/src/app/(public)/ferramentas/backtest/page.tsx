import type { Metadata } from 'next';
import { TerminalBacktest } from '@/components/backtest/terminal-backtest';

export const metadata: Metadata = {
  title: 'Simulador de Backtest — IndexDesk',
  description:
    'Monte uma carteira e acompanhe patrimônio, risco e desempenho com dados históricos.',
};

export default function BacktestPage() {
  return <TerminalBacktest />;
}
