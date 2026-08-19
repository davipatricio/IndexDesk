import { clsx, type ClassValue } from 'clsx';
import { twMerge } from 'tailwind-merge';

const currencyFormatterBRL = new Intl.NumberFormat('pt-BR', {
  style: 'currency',
  currency: 'BRL',
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

const percentFormatters = new Map<number, Intl.NumberFormat>();

export function cn(...inputs: ClassValue[]): string {
  return twMerge(clsx(inputs));
}

export function formatCurrencyBRL(value: number): string {
  return currencyFormatterBRL.format(value);
}

export function formatPercent(value: number, minimumFractionDigits = 2): string {
  let formatter = percentFormatters.get(minimumFractionDigits);
  if (!formatter) {
    formatter = new Intl.NumberFormat('pt-BR', {
      minimumFractionDigits,
      maximumFractionDigits: minimumFractionDigits,
      signDisplay: 'auto',
    });
    percentFormatters.set(minimumFractionDigits, formatter);
  }
  return `${formatter.format(value)}%`;
}
