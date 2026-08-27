import { describe, it, expect } from 'vitest';
import { portfolioHue, portfolioAvatarStyle, portfolioInitial } from '../use-portfolios-list';

describe('use-portfolios-list helpers', () => {
  it('portfolioHue retorna número consistente e determinístico', () => {
    const id = '550e8400-e29b-41d4-a716-446655440000';
    const hue1 = portfolioHue(id);
    const hue2 = portfolioHue(id);
    expect(hue1).toBe(hue2);
    expect(hue1).toBeGreaterThanOrEqual(0);
    expect(hue1).toBeLessThan(360);
  });

  it('portfolioAvatarStyle gera objeto CSS com HSL válido', () => {
    const style = portfolioAvatarStyle('port-123');
    expect(style.backgroundColor).toMatch(/hsl\(\d+(\.\d+)? 65% 45% \/ 0\.15\)/);
    expect(style.color).toMatch(/hsl\(\d+(\.\d+)? 65% 45%\)/);
  });

  it('portfolioInitial extrai primeira letra em maiúsculo', () => {
    expect(portfolioInitial('Carteira Principal')).toBe('C');
    expect(portfolioInitial('  fii ')).toBe('F');
    expect(portfolioInitial('')).toBe('C');
  });
});
