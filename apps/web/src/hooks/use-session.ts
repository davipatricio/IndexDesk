'use client';

import * as React from 'react';
import { useSessionStore } from '@/stores/session-store';
import { useMounted } from '@/hooks/use-mounted';
import type { MockAccount } from '@/lib/mock-accounts';

export interface UseSessionResult {
  account: MockAccount | null;
  isAuthenticated: boolean;
  isAdmin: boolean;
  /** `true` once mounted and persisted state is loaded. */
  isReady: boolean;
  signIn: (account: MockAccount) => void;
  signOut: () => void;
}

/**
 * Hydration-safe accessor for the current user session.
 *
 * Exposes `isReady: false` until the component is mounted on the client AND
 * local storage has been merged. Consumers should render a neutral skeleton or
 * the logged-out shape while `isReady` is false.
 */
export function useSession(): UseSessionResult {
  const mounted = useMounted();
  const account = useSessionStore((s) => s.account);
  const signIn = useSessionStore((s) => s.signIn);
  const signOut = useSessionStore((s) => s.signOut);

  React.useEffect(() => {
    // Rehydrate once per client lifecycle.
    void useSessionStore.persist.rehydrate();
  }, []);

  const isReady = mounted && useSessionStore.persist.hasHydrated();

  return {
    account: isReady ? account : null,
    isAuthenticated: isReady && account !== null,
    isAdmin: isReady && account?.role === 'admin',
    isReady,
    signIn,
    signOut,
  };
}
