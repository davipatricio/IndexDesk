import { describe, it, expect, beforeEach } from 'vitest';
import { useSessionStore, userToAccount } from '../session-store';
import type { User } from '@/types/auth';

describe('useSessionStore', () => {
  beforeEach(() => {
    useSessionStore.getState().signOut();
  });

  it('defaults to logged out (account is null)', () => {
    const state = useSessionStore.getState();
    expect(state.account).toBeNull();
    expect(state.user).toBeNull();
  });

  it('signIn sets the user and account in state', () => {
    const user: User = {
      id: 'usr-123',
      email: 'investor@indexdesk.com',
      fullName: 'Marina Alves',
      roles: ['User'],
      permissions: ['catalog:read'],
    };

    useSessionStore.getState().signIn(user);

    const state = useSessionStore.getState();
    expect(state.user).toEqual(user);
    expect(state.account?.name).toBe('Marina Alves');
    expect(state.account?.role).toBe('investor');
    expect(state.account?.initials).toBe('MA');
  });

  it('signOut clears the account in state', () => {
    const adminUser: User = {
      id: 'admin-123',
      email: 'admin@indexdesk.com',
      fullName: 'Carlos Drummond',
      roles: ['Admin'],
      permissions: ['assets:write'],
    };

    useSessionStore.getState().signIn(adminUser);
    expect(useSessionStore.getState().account?.role).toBe('admin');

    useSessionStore.getState().signOut();
    expect(useSessionStore.getState().account).toBeNull();
    expect(useSessionStore.getState().user).toBeNull();
  });

  it('userToAccount properly maps admin roles and single-word initials', () => {
    const singleNameUser: User = {
      id: 'usr-999',
      email: 'single@indexdesk.com',
      fullName: 'Aristoteles',
      roles: ['SuperAdmin'],
      permissions: [],
    };

    const account = userToAccount(singleNameUser);
    expect(account.role).toBe('admin');
    expect(account.initials).toBe('AR');
  });
});
