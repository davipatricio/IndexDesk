'use client';

import { create } from 'zustand';
import { createJSONStorage, persist, type StateStorage } from 'zustand/middleware';
import type { User } from '@/types/auth';

export interface UserAccount {
  id: string;
  name: string;
  email: string;
  role: 'admin' | 'investor';
  initials: string;
}

export function userToAccount(user: User): UserAccount {
  const isAdmin = user.roles.some(
    (r) => r.toLowerCase() === 'admin' || r.toLowerCase() === 'superadmin',
  );
  const nameParts = user.fullName.trim().split(/\s+/);
  const initials =
    nameParts.length > 1
      ? `${nameParts[0]?.[0] ?? ''}${nameParts[nameParts.length - 1]?.[0] ?? ''}`.toUpperCase()
      : (user.fullName.slice(0, 2) || 'US').toUpperCase();

  return {
    id: user.id,
    name: user.fullName,
    email: user.email,
    role: isAdmin ? 'admin' : 'investor',
    initials,
  };
}

function syncSessionCookie(sessionId: string | null) {
  if (typeof document === 'undefined') return;
  if (sessionId) {
    document.cookie = `session_hint=${encodeURIComponent(sessionId)}; path=/; max-age=604800; SameSite=Lax`;
    document.cookie = `indexdesk-session=${encodeURIComponent(sessionId)}; path=/; max-age=604800; SameSite=Lax`;
  } else {
    document.cookie = 'session_hint=; path=/; max-age=0; SameSite=Lax';
    document.cookie = 'indexdesk-session=; path=/; max-age=0; SameSite=Lax';
  }
}

interface SessionState {
  user: User | null;
  account: UserAccount | null;
  /** Flips to `true` once persisted storage has been rehydrated. */
  isReady: boolean;
  signIn: (user: User) => void;
  signOut: () => void;
  setUser: (user: User | null) => void;
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
      user: null,
      account: null,
      isReady: false,
      signIn: (user) => {
        const account = userToAccount(user);
        syncSessionCookie(user.id);
        set({ user, account });
      },
      signOut: () => {
        syncSessionCookie(null);
        set({ user: null, account: null });
      },
      setUser: (user) => {
        if (user) {
          const account = userToAccount(user);
          syncSessionCookie(user.id);
          set({ user, account });
        } else {
          syncSessionCookie(null);
          set({ user: null, account: null });
        }
      },
    }),
    {
      name: 'indexdesk-session',
      storage: createJSONStorage(() => safeStorage),
      skipHydration: true,
      partialize: (state) => ({ user: state.user, account: state.account }),
    },
  ),
);
