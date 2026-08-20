'use client';

import * as React from 'react';
import { useSessionStore } from '@/stores/session-store';
import { useMounted } from '@/hooks/use-mounted';
import type { MockAccount } from '@/lib/mock-accounts';
import type { User, SignInDto, SignUpDto } from '@/types/auth';
import {
  signIn as apiSignIn,
  signOut as apiSignOut,
  signUp as apiSignUp,
} from '@/lib/api-client';

export interface UseSessionResult {
  user: User | null;
  account: MockAccount | null;
  isAuthenticated: boolean;
  isAdmin: boolean;
  isPro: boolean;
  /** `true` once mounted and persisted state is loaded. */
  isReady: boolean;
  signIn: (identityOrDto: MockAccount | User | SignInDto) => Promise<void> | void;
  signOut: () => Promise<void> | void;
  signUp: (dto: SignUpDto) => Promise<void>;
}

function isCredentialsDto(
  val: MockAccount | User | SignInDto,
): val is SignInDto {
  return 'password' in val && !('id' in val);
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
  const user = useSessionStore((s) => s.user);
  const account = useSessionStore((s) => s.account);
  const storeSignIn = useSessionStore((s) => s.signIn);
  const storeSignOut = useSessionStore((s) => s.signOut);

  React.useEffect(() => {
    // Rehydrate once per client lifecycle.
    void useSessionStore.persist.rehydrate();
  }, []);

  const isReady = mounted && useSessionStore.persist.hasHydrated();

  const activeUser = isReady ? user : null;
  const activeAccount = isReady ? account : null;
  const isAuthenticated = Boolean(isReady && (user !== null || account !== null));

  const isAdmin = Boolean(
    isReady &&
      (account?.role === 'admin' ||
        user?.roles.some(
          (r) => r.toLowerCase() === 'admin' || r.toLowerCase() === 'superadmin',
        )),
  );

  const isPro = Boolean(
    isReady &&
      (isAdmin ||
        user?.roles.some((r) => r.toLowerCase() === 'pro') ||
        user?.permissions.includes('pro:access')),
  );

  const handleSignIn = React.useCallback(
    async (identityOrDto: MockAccount | User | SignInDto) => {
      if (isCredentialsDto(identityOrDto)) {
        const res = await apiSignIn(identityOrDto);
        storeSignIn(res.user);
        return;
      }
      storeSignIn(identityOrDto);
    },
    [storeSignIn],
  );

  const handleSignOut = React.useCallback(async () => {
    await apiSignOut();
    storeSignOut();
  }, [storeSignOut]);

  const handleSignUp = React.useCallback(
    async (dto: SignUpDto) => {
      const res = await apiSignUp(dto);
      storeSignIn(res.user);
    },
    [storeSignIn],
  );

  return {
    user: activeUser,
    account: activeAccount,
    isAuthenticated,
    isAdmin,
    isPro,
    isReady,
    signIn: handleSignIn,
    signOut: handleSignOut,
    signUp: handleSignUp,
  };
}
