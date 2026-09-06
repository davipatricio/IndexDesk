/** Decimal input accepts pt-BR grouping and ungrouped dot decimals. */
export function parsePortfolioNumber(input: string): number {
  const text = input.trim();
  if (!/^-?(?:\d+|\d{1,3}(?:\.\d{3})+)(?:,\d+)?$/.test(text) && !/^-?\d+\.\d+$/.test(text))
    return NaN;
  const normalized = text.includes(',') ? text.replaceAll('.', '').replace(',', '.') : text;
  const value = Number(normalized);
  return Number.isFinite(value) ? value : NaN;
}

export function validPortfolioDate(value: string): boolean {
  return (
    /^\d{4}-\d{2}-\d{2}$/.test(value) &&
    Number.isFinite(Date.parse(value)) &&
    new Date(value).toISOString().slice(0, 10) === value
  );
}
