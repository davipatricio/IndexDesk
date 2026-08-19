'use client';

import { create } from 'zustand';
import { createJSONStorage, persist, type StateStorage } from 'zustand/middleware';
import type { MockAccount } from '@/lib/mock-accounts';

interface SessionState {
  account: MockAccount | null;
  /** Flips to `true` once persisted storage has been rehydrated. */
  isReady: boolean;
  signIn: (account: MockAccount) => void;
  signOut: () => void;
}

const memoryStorage = new Map<string, string>();
const safeStorage: StateStorage = {
  getItem: (key) => {
    if (typeof window !== 'undefined' && window.localStorage) {
      return window.localStorage.getItem(key);
    }
    return memoryStorage.get(key) ?? null;
  },
  setItem: (key, value) => {
    if (typeof window !== 'undefined' && window.localStorage) {
      window.localStorage.setItem(key, value);
    } else {
      memoryStorage.set(key, value);
    }
  },
  removeItem: (key) => {
    if (typeof window !== 'undefined' && window.localStorage) {
      window.localStorage.removeItem(key);
    } else {
      memoryStorage.delete(key);
    }
  },
};

/**
 * Client-side session store.
 *
 * `skipHydration: true` means the store mounts with `account: null` on BOTH the
 * server and the client's first render, so React never sees a hydration
 * mismatch. `useSession` then calls `rehydrate()` in an effect and flips
 * `isReady`, which is what the header uses to avoid flashing the logged-out
 * buttons before the persisted account loads.
 */
export const useSessionStore = create<SessionState>()(
  persist(
    (set) => ({
      account: null,
      isReady: false,
      signIn: (account) => set({ account }),
      signOut: () => set({ account: null }),
    }),
    {
      name: 'indexdesk-session',
      storage: createJSONStorage(() => safeStorage),
      skipHydration: true,
      // Only the account identity survives a reload; readiness is runtime-only.
      partialize: (state) => ({ account: state.account }),
    },
  ),
);
