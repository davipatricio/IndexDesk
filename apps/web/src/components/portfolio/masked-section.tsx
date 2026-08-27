'use client';

import * as React from 'react';
import { usePrivacyStore } from '@/stores/privacy-store';
import { useSession } from '@/hooks/use-session';
import { useCurrentUserQuery } from '@/hooks/use-auth-queries';
import { useSessionStore } from '@/stores/session-store';
import { PrivacyHotkey } from '@/components/layout/privacy-hotkey';

/**
 * Limite client para págs. públicas (/c/[slug] — fora do DashboardShell).
 * Hidrate o privacy store a partir do /me do viewer logado, então um
 * observador conectado com hideValues=true mascara mesmo a carteira pública
 * (escopo global). Anônimo: hideValues lida com null → false → valores exibidos.
 * Also provides the Ctrl+. hotkey so MaskedValue's title hint remains truthful.
 */
export function MaskedSection({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, isReady } = useSession();
  const initFromPreferences = usePrivacyStore((s) => s.initFromPreferences);
  const setAuthenticated = usePrivacyStore((s) => s.setAuthenticated);
  const meQuery = useCurrentUserQuery({ enabled: isReady && isAuthenticated });
  const setUser = useSessionStore((s) => s.setUser);

  React.useEffect(() => {
    if (!isReady) return;
    setAuthenticated(isAuthenticated);
  }, [isReady, isAuthenticated, setAuthenticated]);

  React.useEffect(() => {
    const fresh = meQuery.data;
    if (!fresh) return;
    setUser(fresh);
    initFromPreferences(fresh.preferences);
  }, [meQuery.data, initFromPreferences, setUser]);

  return <PrivacyHotkey>{children}</PrivacyHotkey>;
}
