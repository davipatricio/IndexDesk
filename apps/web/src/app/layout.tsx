import type { Metadata, Viewport } from 'next';
import './globals.css';
import { Providers } from './providers';
import { Navbar } from '@/components/layout/navbar';
import { Footer } from '@/components/layout/footer';
import { ServiceWorkerRegistration } from '@/components/pwa/service-worker-registration';
import { Geist } from 'next/font/google';
import { cn } from '@/lib/utils';

const geist = Geist({ subsets: ['latin'], variable: '--font-sans' });

export const metadata: Metadata = {
  title: {
    template: '%s | IndexDesk — ETFs e BDRs B3',
    default: 'IndexDesk — Plataforma de Inteligência para ETFs e BDRs da B3',
  },
  description:
    'Análise completa de ETFs e BDRs brasileiros: catálogo de fundos de índice, lâmina CVM, tributação (come-cotas/DARF), simulador de backtest e rendimento real.',
  keywords: [
    'ETF B3',
    'BDR ETF',
    'IVVB11',
    'BOVA11',
    'B5P211',
    'Fundos de Índice',
    'Backtest ETF',
    'Tributação ETF',
  ],
  manifest: '/manifest.json',
};

export const viewport: Viewport = {
  themeColor: [
    { media: '(prefers-color-scheme: light)', color: '#ffffff' },
    { media: '(prefers-color-scheme: dark)', color: '#09090b' },
  ],
  width: 'device-width',
  initialScale: 1,
  maximumScale: 5,
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="pt-BR" suppressHydrationWarning className={cn('font-sans', geist.variable)}>
      <head />
      <body className="min-h-screen bg-background font-sans antialiased flex flex-col selection:bg-primary/25">
        <Providers>
          <ServiceWorkerRegistration />
          <Navbar />
          <main className="flex-1">{children}</main>
          <Footer />
        </Providers>
      </body>
    </html>
  );
}
