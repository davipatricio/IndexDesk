import { describe, expect, it } from 'vitest';

import { tokenToHex } from '@/lib/chart-colors';

describe('tokenToHex', () => {
  it('passes hex through', () => {
    expect(tokenToHex('#16a34a')).toBe('#16a34a');
    expect(tokenToHex('#16a34a80')).toBe('#16a34a80');
  });

  it('converts neutral oklch to white and black', () => {
    expect(tokenToHex('oklch(1 0 0)')).toBe('#ffffff');
    expect(tokenToHex('oklch(0 0 0)')).toBe('#000000');
  });

  it('converts a known green token within sRGB gamut', () => {
    // --positive light token: oklch(0.55 0.121 156)
    const hex = tokenToHex('oklch(0.55 0.121 156)');
    expect(hex).toMatch(/^#[\da-f]{6}$/);
    // Green channel must dominate (hue 156 ≈ spring green).
    if (!hex) return;
    const g = Number.parseInt(hex.slice(3, 5), 16);
    const r = Number.parseInt(hex.slice(1, 3), 16);
    const b = Number.parseInt(hex.slice(5, 7), 16);
    expect(g).toBeGreaterThan(r);
    expect(g).toBeGreaterThan(b);
  });

  it('appends the alpha byte when present', () => {
    const hex = tokenToHex('oklch(0.5 0.1 120 / 50%)');
    expect(hex?.length).toBe(9);
    expect(hex?.endsWith('80')).toBe(true);
  });

  it('returns null for unparseable input', () => {
    expect(tokenToHex('lab(50% 10 -5)')).toBeNull();
    expect(tokenToHex('not-a-color')).toBeNull();
  });
});
