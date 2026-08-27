'use client';

import { create } from 'zustand';
import { toast } from 'sonner';
import { updateMyPreferences } from '@/lib/api-client';

interface PrivacyState {
  /** `true` = valores ocultos (••••). `null` = ainda desconhecido (antes do /me chegar). */
  hideValues: boolean | null;
  /** `true` depois que as preferências da conta chegaram (initFromPreferences). */
  hydrated: boolean;
  /** Conta autenticada? Controla se o toggle faz PATCH ou apenas estado visual. */
  authenticated: boolean;
  setHideValues: (value: boolean) => void;
  initFromPreferences: (prefs: { hideValues?: boolean | null } | null | undefined) => void;
  setAuthenticated: (authenticated: boolean) => void;
  reset: () => void;
}

const DEFAULT_HIDE_VALUES = false;

/**
 * Estado global de privacidade ("esconder dados").
 *
 * Fonte de verdade é a conta (PATCH /api/v1/users/me) — nunca localStorage.
 * `setHideValues` grava otimisticamente e chama o backend; em erro faz rollback
 * e mostra toast (padrão sonner já usado no app).
 */
export const usePrivacyStore = create<PrivacyState>((set, get) => ({
  hideValues: null,
  hydrated: false,
  authenticated: false,

  setHideValues: (value) => {
    const { authenticated, hideValues } = get();
    if (authenticated === false) {
      // Sem conta: apenas estado visual da sessão (nada para gravar no servidor).
      set({ hideValues: value, hydrated: true });
      return;
    }

    const previous = hideValues;
    set({ hideValues: value });

    updateMyPreferences({ hideValues: value })
      .then(() => {
        set({ hydrated: true });
        return null;
      })
      .catch(() => {
        set({ hideValues: previous });
        toast.error('Não foi possível salvar sua preferência. Tente novamente em instantes.');
      });
  },

  initFromPreferences: (prefs) => {
    set({
      hideValues: typeof prefs?.hideValues === 'boolean' ? prefs.hideValues : DEFAULT_HIDE_VALUES,
      hydrated: true,
    });
  },

  setAuthenticated: (authenticated) => {
    if (!authenticated) {
      set({ hideValues: null, hydrated: false, authenticated: false });
      return;
    }
    set({ authenticated: true });
  },

  reset: () => set({ hideValues: null, hydrated: false, authenticated: false }),
}));

/**
 * Persistência de verdade = conta. `hideValues === null` = /me ainda não chegou;
 * consumidores devem renderizar neutro (nem escondido nem mostrado) até `hydrated`.
 */
export const PRIVACY_DEFAULTS = { hideValues: DEFAULT_HIDE_VALUES } as const;
