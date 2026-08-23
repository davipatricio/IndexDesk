/**
 * Chart-color helpers.
 *
 * lightweight-charts paints on canvas and only accepts hex/rgb(a) strings —
 * it cannot parse the `oklch()`/`lab()` values Tailwind v4 themes use. These
 * helpers read the authored custom-property values (kept in their original
 * oklch form) and convert them to hex, following `var()` indirections.
 */

let cachedVarMaps: { root: Record<string, string>; dark: Record<string, string> } | null = null;

/** Reads authored custom properties from the :root (light) and .dark (dark) rules. */
function readVarMaps(): { root: Record<string, string>; dark: Record<string, string> } {
  if (cachedVarMaps) return cachedVarMaps;
  const root: Record<string, string> = {};
  const dark: Record<string, string> = {};

  for (const sheet of document.styleSheets) {
    let rules: CSSRuleList;
    try {
      rules = sheet.cssRules;
    } catch {
      continue; // cross-origin sheets throw on access
    }
    for (const rule of rules) {
      if (!(rule instanceof CSSStyleRule) || !rule.selectorText) continue;
      const target = rule.selectorText.includes('.dark')
        ? dark
        : rule.selectorText.includes(':root')
          ? root
          : null;
      if (!target) continue;

      const style = rule.style;
      for (let i = 0; i < style.length; i += 1) {
        const property = style.item(i);
        if (!property || !property.startsWith('--')) continue;
        const value = style.getPropertyValue(property).trim();
        if (value) target[property] = value;
      }
    }
  }

  cachedVarMaps = { root, dark };
  return cachedVarMaps;
}

function activeThemeIsDark(): boolean {
  return typeof document !== 'undefined' && document.documentElement.classList.contains('dark');
}

function lookupVariable(name: string, depth = 0): string | null {
  if (depth > 5) return null;
  const maps = readVarMaps();
  const raw = (activeThemeIsDark() ? maps.dark[name] : undefined) ?? maps.root[name] ?? null;
  if (raw === null) return null;

  // Follow simple var(--other) indirections used across token groups.
  const indirect = raw.match(/^var\((--[\w-]+)\)$/);
  if (indirect?.[1]) return lookupVariable(indirect[1], depth + 1);
  return raw;
}

interface Oklch {
  l: number; // 0..1
  c: number; // 0..~0.4
  h: number; // degrees
  alpha: number | null;
}

function parseOklch(value: string): Oklch | null {
  const match = value.match(
    /^oklch\(\s*([\d.]+)%?\s+([\d.]+)\s+(-?[\d.]+)(?:deg)?\s*(?:\/\s*([\d.%]+)\s*)?\)$/,
  );
  if (!match) return null;

  const [, lRaw, cRaw, hRaw, alphaRaw] = match;
  if (!lRaw || !cRaw || !hRaw) return null;
  const l = Number.parseFloat(lRaw);
  const c = Number.parseFloat(cRaw);
  const h = Number.parseFloat(hRaw);
  if (![l, c, h].every(Number.isFinite)) return null;

  let alpha: number | null = null;
  if (alphaRaw !== undefined) {
    alpha = alphaRaw.endsWith('%')
      ? Number.parseFloat(alphaRaw) / 100
      : Number.parseFloat(alphaRaw);
    if (!Number.isFinite(alpha)) alpha = null;
  }

  return { l: l <= 1 ? l : l / 100, c, h, alpha };
}

/**
 * OKLCH → OKLab → LMS → linear sRGB, then gamma-encoded hex.
 * Matrices from Björn Ottosson's reference OKLab implementation.
 */
function oklchToHexParts({ l, c, h }: Oklch): [number, number, number] {
  const hr = (h * Math.PI) / 180;
  const a = Math.cos(hr) * c;
  const b = Math.sin(hr) * c;

  const lcubed = (l + 0.3963377774 * a + 0.2158037573 * b) ** 3;
  const mcubed = (l - 0.1055613458 * a - 0.0638541728 * b) ** 3;
  const scubed = (l - 0.0894841775 * a - 1.291485548 * b) ** 3;

  const r = 4.0767416621 * lcubed - 3.3077115913 * mcubed + 0.2309699292 * scubed;
  const g = -1.2684380046 * lcubed + 2.6097574011 * mcubed - 0.3413193965 * scubed;
  const bl = -0.0041960863 * lcubed - 0.7034186147 * mcubed + 1.707614701 * scubed;
  return [r, g, bl];
}

function gammaEncode(channel: number): string {
  const v = channel <= 0.0031308 ? 12.92 * channel : 1.055 * Math.pow(channel, 1 / 2.4) - 0.055;
  const byte = Math.min(255, Math.max(0, Math.round(v * 255)));
  return byte.toString(16).padStart(2, '0');
}

function clamp01(value: number): number {
  return Math.min(1, Math.max(0, value));
}

/** Converts any supported token color (`#hex`, `oklch()`) to `#rrggbb[aa]`. */
export function tokenToHex(raw: string): string | null {
  const value = raw.trim();
  if (/^#[\da-fA-F]{6}([\da-fA-F]{2})?$/.test(value)) return value;

  const oklch = parseOklch(value);
  if (!oklch) return null;

  const [lr, lg, lb] = oklchToHexParts(oklch).map((channel) => clamp01(channel));
  if (lr === undefined || lg === undefined || lb === undefined) return null;
  const hex = `#${gammaEncode(lr)}${gammaEncode(lg)}${gammaEncode(lb)}`;
  if (oklch.alpha === null || oklch.alpha >= 1) return hex;
  const alphaByte = Math.round(clamp01(oklch.alpha) * 255)
    .toString(16)
    .padStart(2, '0');
  return `${hex}${alphaByte}`;
}

/**
 * Resolves a CSS custom property on :root to a canvas-safe hex color,
 * falling back when the token cannot be found or parsed.
 */
export function chartColor(tokenName: string, fallback: string): string {
  if (typeof window === 'undefined') return fallback;
  const raw = lookupVariable(tokenName);
  if (!raw) return fallback;
  return tokenToHex(raw) ?? fallback;
}
