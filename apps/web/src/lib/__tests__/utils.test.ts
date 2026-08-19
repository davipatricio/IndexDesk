import { describe, it, expect } from 'vitest';
import { cn, formatCurrencyBRL, formatPercent } from '../utils';

describe('Utils Library', () => {
  it('cn merges classes cleanly without conflict', () => {
    const result = cn('p-4 font-bold', 'p-6', { 'text-red-500': true });
    expect(result).toContain('p-6');
    expect(result).toContain('font-bold');
    expect(result).toContain('text-red-500');
    expect(result).not.toContain('p-4');
  });

  it('formatCurrencyBRL formats number into Brazilian Real format', () => {
    const formatted = formatCurrencyBRL(3500.5);
    expect(formatted).toContain('3.500,50');
  });

  it('formatPercent formats decimals into percentage string', () => {
    const formatted = formatPercent(0.23);
    expect(formatted).toContain('0,23%');
  });
});
