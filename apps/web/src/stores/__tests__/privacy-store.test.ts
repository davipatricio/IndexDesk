import { describe, it, expect, beforeEach, vi } from 'vitest';
import { usePrivacyStore, PRIVACY_DEFAULTS } from '../privacy-store';

describe('usePrivacyStore', () => {
  beforeEach(() => {
    usePrivacyStore.getState().reset();
    vi.restoreAllMocks();
  });

  it('inicia em estado neutro/não-hidratado', () => {
    const s = usePrivacyStore.getState();
    expect(s.hideValues).toBeNull();
    expect(s.hydrated).toBe(false);
    expect(s.authenticated).toBe(false);
    expect(PRIVACY_DEFAULTS.hideValues).toBe(false);
  });

  it('initFromPreferences inicializa hideValues a partir das preferências do usuário', () => {
    usePrivacyStore.getState().initFromPreferences({ hideValues: true });
    expect(usePrivacyStore.getState().hideValues).toBe(true);
    expect(usePrivacyStore.getState().hydrated).toBe(true);

    usePrivacyStore.getState().initFromPreferences(null);
    expect(usePrivacyStore.getState().hideValues).toBe(false);
  });

  it('setHideValues atualiza o estado sem chamar backend se não autenticado', () => {
    usePrivacyStore.getState().setAuthenticated(false);
    usePrivacyStore.getState().setHideValues(true);

    expect(usePrivacyStore.getState().hideValues).toBe(true);
    expect(usePrivacyStore.getState().hydrated).toBe(true);
  });

  it('reset limpa o estado de volta para neutro', () => {
    usePrivacyStore.getState().initFromPreferences({ hideValues: true });
    usePrivacyStore.getState().reset();

    expect(usePrivacyStore.getState().hideValues).toBeNull();
    expect(usePrivacyStore.getState().hydrated).toBe(false);
    expect(usePrivacyStore.getState().authenticated).toBe(false);
  });
});
