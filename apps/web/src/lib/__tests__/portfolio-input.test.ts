import { describe, expect, it } from 'vitest';
import { parsePortfolioNumber, validPortfolioDate } from '@/lib/portfolio-input';

describe('portfolio input boundaries', () => {
  it('accepts pt-BR grouped decimals and ungrouped decimals', () => {
    expect(parsePortfolioNumber('1.000,50')).toBe(1000.5);
    expect(parsePortfolioNumber('10,25')).toBe(10.25);
    expect(parsePortfolioNumber('10.25')).toBe(10.25);
    expect(parsePortfolioNumber('0')).toBe(0);
  });
  it('rejects malformed or nonfinite values', () => {
    for (const input of ['', 'Infinity', 'NaN', '1e9', '1,2,3', '1.00,50', '9'.repeat(400)])
      expect(parsePortfolioNumber(input)).toBeNaN();
  });
  it('validates actual calendar dates', () => {
    expect(validPortfolioDate('2024-02-29')).toBe(true);
    for (const date of ['', '2025-02-29', '2026-13-01', '2026-1-1'])
      expect(validPortfolioDate(date)).toBe(false);
  });
});
