import type { ReactNode } from 'react';
import { DashboardShell } from '@/components/layout/dashboard-shell';

/**
 * Shell do painel do usuário (sidebar + header). Navbar/Footer públicos ficam
 * no root layout e continuam atendendo os grupos (public)/(admin).
 */
export default function DashboardGroupLayout({ children }: { children: ReactNode }) {
  return <DashboardShell>{children}</DashboardShell>;
}
