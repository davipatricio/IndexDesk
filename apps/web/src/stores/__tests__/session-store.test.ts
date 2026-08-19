import { describe, it, expect, beforeEach } from 'vitest';
import { useSessionStore } from '../session-store';
import { MOCK_ACCOUNTS } from '@/lib/mock-accounts';

describe('useSessionStore', () => {
  beforeEach(() => {
    useSessionStore.getState().signOut();
  });

  it('defaults to logged out (account is null)', () => {
    const state = useSessionStore.getState();
    expect(state.account).toBeNull();
  });

  it('signIn sets the account in state', () => {
    const persona = MOCK_ACCOUNTS[0]!;
    useSessionStore.getState().signIn(persona);

    const state = useSessionStore.getState();
    expect(state.account).toEqual(persona);
    expect(state.account?.name).toBe('Marina Alves');
  });

  it('signOut clears the account in state', () => {
    const persona = MOCK_ACCOUNTS[2]!; // Admin persona
    useSessionStore.getState().signIn(persona);
    expect(useSessionStore.getState().account?.role).toBe('admin');

    useSessionStore.getState().signOut();
    expect(useSessionStore.getState().account).toBeNull();
  });
});
