/**
 * Rich mock catalog domain models and fixture datasets for B3 assets:
 * ETFs, Fundos Imobiliários (FIIs), and BDRs (Global ETF BDRs and Equity BDRs).
 */

export type AssetCategory = 'ETF' | 'FII' | 'BDR';

export interface BaseAsset {
  ticker: string;
  name: string;
  manager: string;
  category: AssetCategory;
  subCategory: string;
  lastPrice: number;
  changeDayPercent: number;
  changeYtdPercent: number;
  netAssets: number;
  shareholders: number;
}

export interface EtfAsset extends BaseAsset {
  category: 'ETF';
  benchmark: string;
  managementFee: number;
  assetClass: 'Equity' | 'FixedIncome' | 'Crypto' | 'MultiAsset';
}

export interface FiiAsset extends BaseAsset {
  category: 'FII';
  dividendYield12m: number;
  pvp: number;
  lastDividend: number;
  segment: string;
}

export interface BdrAsset extends BaseAsset {
  category: 'BDR';
  underlyingAsset: string;
  country: string;
  managementFee: number;
}

export type CatalogAsset = EtfAsset | FiiAsset | BdrAsset;

export const MOCK_ETFS: EtfAsset[] = [
  {
    ticker: 'IVVB11',
    name: 'iShares S&P 500 Fundo de Índice',
    manager: 'BlackRock',
    category: 'ETF',
    subCategory: 'Ações Global',
    benchmark: 'S&P 500',
    managementFee: 0.23,
    assetClass: 'Equity',
    netAssets: 4500000000,
    shareholders: 185000,
    lastPrice: 342.5,
    changeDayPercent: 0.45,
    changeYtdPercent: 18.2,
  },
  {
    ticker: 'BOVA11',
    name: 'iShares Ibovespa Fundo de Índice',
    manager: 'BlackRock',
    category: 'ETF',
    subCategory: 'Ações Brasil',
    benchmark: 'IBOV',
    managementFee: 0.1,
    assetClass: 'Equity',
    netAssets: 12000000000,
    shareholders: 120000,
    lastPrice: 125.8,
    changeDayPercent: -0.15,
    changeYtdPercent: 6.4,
  },
  {
    ticker: 'B5P211',
    name: 'It Now IMA-B 5 P2 Fundo de Índice',
    manager: 'Itaú Asset',
    category: 'ETF',
    subCategory: 'Renda Fixa',
    benchmark: 'IMA-B 5 P2',
    managementFee: 0.2,
    assetClass: 'FixedIncome',
    netAssets: 3200000000,
    shareholders: 65000,
    lastPrice: 89.2,
    changeDayPercent: 0.05,
    changeYtdPercent: 8.9,
  },
  {
    ticker: 'WRLD11',
    name: 'Investo MSCI World Fundo de Índice',
    manager: 'Investo',
    category: 'ETF',
    subCategory: 'Ações Global',
    benchmark: 'MSCI World',
    managementFee: 0.38,
    assetClass: 'Equity',
    netAssets: 1500000000,
    shareholders: 42000,
    lastPrice: 118.4,
    changeDayPercent: 0.62,
    changeYtdPercent: 16.5,
  },
  {
    ticker: 'SMAL11',
    name: 'iShares Small Cap Fundo de Índice',
    manager: 'BlackRock',
    category: 'ETF',
    subCategory: 'Ações Brasil',
    benchmark: 'SMLL',
    managementFee: 0.5,
    assetClass: 'Equity',
    netAssets: 2100000000,
    shareholders: 58000,
    lastPrice: 102.1,
    changeDayPercent: -0.8,
    changeYtdPercent: -2.1,
  },
  {
    ticker: 'HASH11',
    name: 'Hashdex Nasdaq Crypto Index',
    manager: 'Hashdex',
    category: 'ETF',
    subCategory: 'Criptoativos',
    benchmark: 'NCI',
    managementFee: 1.3,
    assetClass: 'Crypto',
    netAssets: 2800000000,
    shareholders: 140000,
    lastPrice: 68.9,
    changeDayPercent: 2.1,
    changeYtdPercent: 45.3,
  },
  {
    ticker: 'NASD11',
    name: 'Trend Nasdaq 100 Fundo de Índice',
    manager: 'XP Asset',
    category: 'ETF',
    subCategory: 'Ações Global',
    benchmark: 'Nasdaq-100',
    managementFee: 0.3,
    assetClass: 'Equity',
    netAssets: 1800000000,
    shareholders: 52000,
    lastPrice: 22.4,
    changeDayPercent: 1.15,
    changeYtdPercent: 24.8,
  },
  {
    ticker: 'SPXI11',
    name: 'It Now S&P 500 Fundo de Índice',
    manager: 'Itaú Asset',
    category: 'ETF',
    subCategory: 'Ações Global',
    benchmark: 'S&P 500',
    managementFee: 0.18,
    assetClass: 'Equity',
    netAssets: 2900000000,
    shareholders: 78000,
    lastPrice: 384.2,
    changeDayPercent: 0.42,
    changeYtdPercent: 18.0,
  },
  {
    ticker: 'DIVO11',
    name: 'It Now IDIV Fundo de Índice',
    manager: 'Itaú Asset',
    category: 'ETF',
    subCategory: 'Dividendos',
    benchmark: 'IDIV',
    managementFee: 0.5,
    assetClass: 'Equity',
    netAssets: 1100000000,
    shareholders: 39000,
    lastPrice: 94.7,
    changeDayPercent: 0.18,
    changeYtdPercent: 11.2,
  },
  {
    ticker: 'XFIX11',
    name: 'Trend IFIX Fundo de Índice',
    manager: 'XP Asset',
    category: 'ETF',
    subCategory: 'Fundos Imobiliários',
    benchmark: 'IFIX',
    managementFee: 0.3,
    assetClass: 'MultiAsset',
    netAssets: 680000000,
    shareholders: 28000,
    lastPrice: 11.8,
    changeDayPercent: -0.08,
    changeYtdPercent: 5.6,
  },
  {
    ticker: 'GOLD11',
    name: 'Trend Ouro Fundo de Índice',
    manager: 'XP Asset',
    category: 'ETF',
    subCategory: 'Commodities',
    benchmark: 'LBMA Gold',
    managementFee: 0.3,
    assetClass: 'MultiAsset',
    netAssets: 850000000,
    shareholders: 34000,
    lastPrice: 14.9,
    changeDayPercent: -0.35,
    changeYtdPercent: 14.1,
  },
  {
    ticker: 'LFTS11',
    name: 'Investo Teva Tesouro Selic Fundo de Índice',
    manager: 'Investo',
    category: 'ETF',
    subCategory: 'Renda Fixa',
    benchmark: 'Teva Selic',
    managementFee: 0.19,
    assetClass: 'FixedIncome',
    netAssets: 2400000000,
    shareholders: 48000,
    lastPrice: 112.6,
    changeDayPercent: 0.04,
    changeYtdPercent: 10.4,
  },
];

export const MOCK_FIIS: FiiAsset[] = [
  {
    ticker: 'HGLG11',
    name: 'CSHG Logística Fundo de Investimento Imobiliário',
    manager: 'Pátria Investimentos',
    category: 'FII',
    subCategory: 'Tijolo - Logística',
    segment: 'Logística',
    dividendYield12m: 8.9,
    pvp: 1.02,
    lastDividend: 1.1,
    netAssets: 5300000000,
    shareholders: 380000,
    lastPrice: 164.5,
    changeDayPercent: 0.12,
    changeYtdPercent: 4.8,
  },
  {
    ticker: 'KNIP11',
    name: 'Kinea Rendimentos Imobiliários',
    manager: 'Kinea Investimentos',
    category: 'FII',
    subCategory: 'Papel - CRI',
    segment: 'Recebíveis / CRI',
    dividendYield12m: 11.8,
    pvp: 0.98,
    lastDividend: 0.88,
    netAssets: 7800000000,
    shareholders: 260000,
    lastPrice: 91.2,
    changeDayPercent: -0.05,
    changeYtdPercent: 7.2,
  },
  {
    ticker: 'MXRF11',
    name: 'Maxi Renda Fundo de Investimento Imobiliário',
    manager: 'XP Asset',
    category: 'FII',
    subCategory: 'Papel - CRI',
    segment: 'Recebíveis / CRI',
    dividendYield12m: 12.4,
    pvp: 1.03,
    lastDividend: 0.1,
    netAssets: 3400000000,
    shareholders: 1150000,
    lastPrice: 10.4,
    changeDayPercent: 0.1,
    changeYtdPercent: 5.1,
  },
  {
    ticker: 'XPLG11',
    name: 'XP Log Fundo de Investimento Imobiliário',
    manager: 'XP Asset',
    category: 'FII',
    subCategory: 'Tijolo - Logística',
    segment: 'Logística',
    dividendYield12m: 9.1,
    pvp: 0.95,
    lastDividend: 0.78,
    netAssets: 3100000000,
    shareholders: 320000,
    lastPrice: 104.3,
    changeDayPercent: 0.25,
    changeYtdPercent: 3.9,
  },
  {
    ticker: 'BTLG11',
    name: 'BTG Pactual Logística FII',
    manager: 'BTG Pactual Asset',
    category: 'FII',
    subCategory: 'Tijolo - Logística',
    segment: 'Logística',
    dividendYield12m: 9.6,
    pvp: 0.99,
    lastDividend: 0.76,
    netAssets: 4200000000,
    shareholders: 290000,
    lastPrice: 101.8,
    changeDayPercent: -0.1,
    changeYtdPercent: 6.2,
  },
  {
    ticker: 'XPML11',
    name: 'XP Malls Fundo de Investimento Imobiliário',
    manager: 'XP Asset',
    category: 'FII',
    subCategory: 'Tijolo - Shoppings',
    segment: 'Shoppings',
    dividendYield12m: 9.4,
    pvp: 0.98,
    lastDividend: 0.92,
    netAssets: 6200000000,
    shareholders: 450000,
    lastPrice: 114.7,
    changeDayPercent: 0.35,
    changeYtdPercent: 8.4,
  },
  {
    ticker: 'VISC11',
    name: 'Vinci Shopping Centers FII',
    manager: 'Vinci Partners',
    category: 'FII',
    subCategory: 'Tijolo - Shoppings',
    segment: 'Shoppings',
    dividendYield12m: 9.2,
    pvp: 0.94,
    lastDividend: 1.0,
    netAssets: 3800000000,
    shareholders: 310000,
    lastPrice: 118.9,
    changeDayPercent: -0.2,
    changeYtdPercent: 5.8,
  },
  {
    ticker: 'KNCR11',
    name: 'Kinea Rendimentos Imobiliários CDI',
    manager: 'Kinea Investimentos',
    category: 'FII',
    subCategory: 'Papel - CRI',
    segment: 'Recebíveis / CRI',
    dividendYield12m: 12.8,
    pvp: 1.01,
    lastDividend: 1.05,
    netAssets: 5900000000,
    shareholders: 240000,
    lastPrice: 102.5,
    changeDayPercent: 0.08,
    changeYtdPercent: 8.9,
  },
  {
    ticker: 'HGRU11',
    name: 'CSHG Renda Urbana FII',
    manager: 'Pátria Investimentos',
    category: 'FII',
    subCategory: 'Híbrido - Varejo',
    segment: 'Híbrido / Varejo',
    dividendYield12m: 8.7,
    pvp: 0.99,
    lastDividend: 0.9,
    netAssets: 2300000000,
    shareholders: 210000,
    lastPrice: 127.4,
    changeDayPercent: 0.15,
    changeYtdPercent: 4.2,
  },
  {
    ticker: 'CPTS11',
    name: 'Capitânia Securities II FII',
    manager: 'Capitânia Asset',
    category: 'FII',
    subCategory: 'Papel - CRI',
    segment: 'Recebíveis / CRI',
    dividendYield12m: 11.9,
    pvp: 0.91,
    lastDividend: 0.08,
    netAssets: 2900000000,
    shareholders: 230000,
    lastPrice: 8.45,
    changeDayPercent: -0.35,
    changeYtdPercent: 2.8,
  },
  {
    ticker: 'TGAR11',
    name: 'TG Ativo Real FII',
    manager: 'TG Core Asset',
    category: 'FII',
    subCategory: 'Desenvolvimento',
    segment: 'Desenvolvimento / Loteamentos',
    dividendYield12m: 13.5,
    pvp: 0.96,
    lastDividend: 1.35,
    netAssets: 2100000000,
    shareholders: 160000,
    lastPrice: 119.2,
    changeDayPercent: 0.45,
    changeYtdPercent: 7.9,
  },
];

export const MOCK_BDRS: BdrAsset[] = [
  {
    ticker: 'BIVW39',
    name: 'iShares S&P 500 Growth ETF BDR',
    manager: 'BlackRock',
    category: 'BDR',
    subCategory: 'ETF BDR Global',
    underlyingAsset: 'IVW (NYSE)',
    country: 'EUA',
    managementFee: 0.18,
    netAssets: 1400000000,
    shareholders: 45000,
    lastPrice: 88.4,
    changeDayPercent: 0.85,
    changeYtdPercent: 26.4,
  },
  {
    ticker: 'BIEM39',
    name: 'iShares Core MSCI Emerging Markets ETF BDR',
    manager: 'BlackRock',
    category: 'BDR',
    subCategory: 'ETF BDR Global',
    underlyingAsset: 'IEMG (NYSE)',
    country: 'Global',
    managementFee: 0.09,
    netAssets: 950000000,
    shareholders: 28000,
    lastPrice: 52.6,
    changeDayPercent: -0.4,
    changeYtdPercent: 9.1,
  },
  {
    ticker: 'BIEF39',
    name: 'iShares Core MSCI EAFE ETF BDR',
    manager: 'BlackRock',
    category: 'BDR',
    subCategory: 'ETF BDR Global',
    underlyingAsset: 'IEFA (NYSE)',
    country: 'Europa & Ásia',
    managementFee: 0.07,
    netAssets: 780000000,
    shareholders: 22000,
    lastPrice: 74.2,
    changeDayPercent: 0.15,
    changeYtdPercent: 11.8,
  },
  {
    ticker: 'BACW39',
    name: 'iShares MSCI ACWI ETF BDR',
    manager: 'BlackRock',
    category: 'BDR',
    subCategory: 'ETF BDR Global',
    underlyingAsset: 'ACWI (Nasdaq)',
    country: 'Global',
    managementFee: 0.32,
    netAssets: 620000000,
    shareholders: 19000,
    lastPrice: 114.5,
    changeDayPercent: 0.55,
    changeYtdPercent: 17.3,
  },
  {
    ticker: 'BNDX39',
    name: 'Vanguard Total International Bond ETF BDR',
    manager: 'Vanguard / B3',
    category: 'BDR',
    subCategory: 'Renda Fixa Global',
    underlyingAsset: 'BNDX (Nasdaq)',
    country: 'Global',
    managementFee: 0.07,
    netAssets: 480000000,
    shareholders: 14000,
    lastPrice: 51.8,
    changeDayPercent: -0.05,
    changeYtdPercent: 4.8,
  },
  {
    ticker: 'NVDC34',
    name: 'NVIDIA Corporation BDR',
    manager: 'Banco B3 / Depositário',
    category: 'BDR',
    subCategory: 'Ações Tecnologia',
    underlyingAsset: 'NVDA (Nasdaq)',
    country: 'EUA',
    managementFee: 0.0,
    netAssets: 4200000000,
    shareholders: 210000,
    lastPrice: 18.25,
    changeDayPercent: 2.45,
    changeYtdPercent: 48.6,
  },
  {
    ticker: 'AAPL34',
    name: 'Apple Inc. BDR',
    manager: 'Banco B3 / Depositário',
    category: 'BDR',
    subCategory: 'Ações Tecnologia',
    underlyingAsset: 'AAPL (Nasdaq)',
    country: 'EUA',
    managementFee: 0.0,
    netAssets: 3800000000,
    shareholders: 190000,
    lastPrice: 62.4,
    changeDayPercent: 0.3,
    changeYtdPercent: 15.2,
  },
  {
    ticker: 'MSFT34',
    name: 'Microsoft Corporation BDR',
    manager: 'Banco B3 / Depositário',
    category: 'BDR',
    subCategory: 'Ações Tecnologia',
    underlyingAsset: 'MSFT (Nasdaq)',
    country: 'EUA',
    managementFee: 0.0,
    netAssets: 3100000000,
    shareholders: 155000,
    lastPrice: 84.1,
    changeDayPercent: 0.65,
    changeYtdPercent: 19.4,
  },
  {
    ticker: 'AMZO34',
    name: 'Amazon.com Inc. BDR',
    manager: 'Banco B3 / Depositário',
    category: 'BDR',
    subCategory: 'Ações Varejo & Cloud',
    underlyingAsset: 'AMZN (Nasdaq)',
    country: 'EUA',
    managementFee: 0.0,
    netAssets: 2400000000,
    shareholders: 125000,
    lastPrice: 58.7,
    changeDayPercent: 1.1,
    changeYtdPercent: 22.8,
  },
  {
    ticker: 'DISB34',
    name: 'The Walt Disney Company BDR',
    manager: 'Banco B3 / Depositário',
    category: 'BDR',
    subCategory: 'Ações Entretenimento',
    underlyingAsset: 'DIS (NYSE)',
    country: 'EUA',
    managementFee: 0.0,
    netAssets: 890000000,
    shareholders: 42000,
    lastPrice: 38.2,
    changeDayPercent: -0.75,
    changeYtdPercent: 8.5,
  },
];

export const ALL_CATALOG_ASSETS: CatalogAsset[] = [...MOCK_ETFS, ...MOCK_FIIS, ...MOCK_BDRS];

export function getCatalogAssets(category?: AssetCategory): CatalogAsset[] {
  if (!category) return ALL_CATALOG_ASSETS;
  switch (category) {
    case 'ETF':
      return MOCK_ETFS;
    case 'FII':
      return MOCK_FIIS;
    case 'BDR':
      return MOCK_BDRS;
  }
}

export function findCatalogAsset(ticker: string): CatalogAsset | undefined {
  const upper = ticker.toUpperCase();
  return ALL_CATALOG_ASSETS.find((a) => a.ticker.toUpperCase() === upper);
}

export function getFilterOptions(category: AssetCategory): {
  managers: string[];
  subCategories: string[];
} {
  const assets = getCatalogAssets(category);
  const managers = Array.from(new Set(assets.map((a) => a.manager))).sort();
  const subCategories = Array.from(new Set(assets.map((a) => a.subCategory))).sort();
  return { managers, subCategories };
}

export interface CategoryStatsSummary {
  assetCount: number;
  totalNetAssets: number;
  totalShareholders: number;
  primaryMetric: {
    label: string;
    value: string;
  };
  secondaryMetric: {
    label: string;
    value: string;
  };
}

export function computeCategoryStats(
  assets: CatalogAsset[],
  category: AssetCategory,
): CategoryStatsSummary {
  const totalNetAssets = assets.reduce((sum, a) => sum + a.netAssets, 0);
  const totalShareholders = assets.reduce((sum, a) => sum + a.shareholders, 0);

  if (category === 'FII') {
    const fiiAssets = assets.filter((a): a is FiiAsset => a.category === 'FII');
    const avgDy =
      fiiAssets.length > 0
        ? fiiAssets.reduce((sum, a) => sum + a.dividendYield12m, 0) / fiiAssets.length
        : 0;
    const avgPvp =
      fiiAssets.length > 0 ? fiiAssets.reduce((sum, a) => sum + a.pvp, 0) / fiiAssets.length : 0;

    return {
      assetCount: assets.length,
      totalNetAssets,
      totalShareholders,
      primaryMetric: {
        label: 'DY Médio 12M',
        value: `${avgDy.toFixed(2).replace('.', ',')}% a.a.`,
      },
      secondaryMetric: {
        label: 'P/VP Médio',
        value: `${avgPvp.toFixed(2).replace('.', ',')}x`,
      },
    };
  }

  if (category === 'ETF') {
    const etfAssets = assets.filter((a): a is EtfAsset => a.category === 'ETF');
    const avgFee =
      etfAssets.length > 0
        ? etfAssets.reduce((sum, a) => sum + a.managementFee, 0) / etfAssets.length
        : 0;
    const lowestFee =
      etfAssets.length > 0
        ? etfAssets.reduce((min, a) => (a.managementFee < min.managementFee ? a : min))
        : null;

    return {
      assetCount: assets.length,
      totalNetAssets,
      totalShareholders,
      primaryMetric: {
        label: 'Taxa Adm. Média',
        value: `${avgFee.toFixed(2).replace('.', ',')}% a.a.`,
      },
      secondaryMetric: {
        label: 'Menor Taxa',
        value: lowestFee
          ? `${lowestFee.managementFee.toFixed(2).replace('.', ',')}% (${lowestFee.ticker})`
          : '—',
      },
    };
  }

  // BDRs
  const bdrAssets = assets.filter((a): a is BdrAsset => a.category === 'BDR');
  const distinctUnderlying = new Set(bdrAssets.map((a) => a.underlyingAsset)).size;
  const avgFee =
    bdrAssets.length > 0
      ? bdrAssets.reduce((sum, a) => sum + a.managementFee, 0) / bdrAssets.length
      : 0;

  return {
    assetCount: assets.length,
    totalNetAssets,
    totalShareholders,
    primaryMetric: {
      label: 'Ativos Subjacentes',
      value: `${distinctUnderlying} mapeados`,
    },
    secondaryMetric: {
      label: 'Taxa ETF Base Média',
      value: `${avgFee.toFixed(2).replace('.', ',')}% a.a.`,
    },
  };
}

export function filterCatalogAssets(
  assets: CatalogAsset[],
  filters: {
    search?: string;
    manager?: string;
    subCategory?: string;
  },
): CatalogAsset[] {
  let result = assets;

  if (filters.manager && filters.manager !== 'all') {
    result = result.filter((a) => a.manager === filters.manager);
  }

  if (filters.subCategory && filters.subCategory !== 'all') {
    result = result.filter((a) => a.subCategory === filters.subCategory);
  }

  if (filters.search) {
    const q = filters.search.trim().toLowerCase();
    result = result.filter(
      (a) =>
        a.ticker.toLowerCase().includes(q) ||
        a.name.toLowerCase().includes(q) ||
        a.manager.toLowerCase().includes(q) ||
        ('benchmark' in a && a.benchmark.toLowerCase().includes(q)) ||
        ('underlyingAsset' in a && a.underlyingAsset.toLowerCase().includes(q)),
    );
  }

  return result;
}
