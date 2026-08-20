import { describe, it, expect, beforeEach, vi } from 'vitest';
import {
  signIn,
  signUp,
  signOut,
  getCurrentUser,
  getAccessToken,
  setAccessToken,
  fetchWithAuth,
} from '../api-client';
import { userToMockAccount, mockAccountToUser } from '@/stores/session-store';
import type { User } from '@/types/auth';
import type { MockAccount } from '../mock-accounts';

describe('Auth API Client & Mapping', () => {
  beforeEach(() => {
    setAccessToken(null);
    vi.restoreAllMocks();
  });

  it('manages in-memory accessToken correctly', () => {
    expect(getAccessToken()).toBeNull();
    setAccessToken('test-token-123');
    expect(getAccessToken()).toBe('test-token-123');
    setAccessToken(null);
    expect(getAccessToken()).toBeNull();
  });

  it('signIn falls back gracefully when API is offline', async () => {
    const res = await signIn({
      email: 'investor@example.com',
      password: 'password123',
    });

    expect(res.accessToken).toBeDefined();
    expect(res.user.email).toBe('investor@example.com');
    expect(res.user.roles).toContain('User');
    expect(getAccessToken()).toBe(res.accessToken);
  });

  it('signIn returns admin claims when admin email is used in fallback', async () => {
    const res = await signIn({
      email: 'admin@indexdesk.com',
      password: 'password123',
    });

    expect(res.user.roles).toContain('Admin');
    expect(res.user.permissions).toContain('assets:write');
  });

  it('signUp falls back gracefully with user role', async () => {
    const res = await signUp({
      email: 'newuser@example.com',
      password: 'password123',
      fullName: 'Novo Investidor',
    });

    expect(res.user.email).toBe('newuser@example.com');
    expect(res.user.fullName).toBe('Novo Investidor');
    expect(res.user.roles).toContain('User');
    expect(getAccessToken()).toBe(res.accessToken);
  });

  it('signOut clears accessToken', async () => {
    setAccessToken('active-token');
    await signOut();
    expect(getAccessToken()).toBeNull();
  });

  it('getCurrentUser throws when no token exists', async () => {
    setAccessToken(null);
    await expect(getCurrentUser()).rejects.toThrow('Não autenticado.');
  });

  it('getCurrentUser returns user data when authenticated', async () => {
    setAccessToken('mock-token');
    const mockUser: User = {
      id: 'usr-1',
      email: 'investor@example.com',
      fullName: 'Investidor Demo',
      roles: ['User'],
      permissions: ['assets:read'],
    };
    const mockFetch = vi.fn().mockResolvedValue(
      new Response(JSON.stringify(mockUser), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    );
    vi.stubGlobal('fetch', mockFetch);

    const user = await getCurrentUser();
    expect(user.id).toBe('usr-1');
    expect(user.roles.length).toBeGreaterThan(0);
  });

  it('fetchWithAuth injects Authorization header when token is present', async () => {
    setAccessToken('jwt-token-xyz');
    const mockFetch = vi.fn().mockResolvedValue(new Response('{}', { status: 200 }));
    vi.stubGlobal('fetch', mockFetch);

    await fetchWithAuth('http://localhost:5000/api/v1/auth/me');

    expect(mockFetch).toHaveBeenCalled();
    const callArgs = mockFetch.mock.calls[0]!;
    const headers = callArgs[1]?.headers as Headers;
    expect(headers.get('Authorization')).toBe('Bearer jwt-token-xyz');
  });

  it('userToMockAccount maps User with Admin role to admin MockAccount', () => {
    const user: User = {
      id: 'usr-1',
      email: 'gestor@indexdesk.com',
      fullName: 'Carlos Drummond',
      roles: ['Admin'],
      permissions: ['assets:write'],
    };

    const account = userToMockAccount(user);
    expect(account.id).toBe('usr-1');
    expect(account.name).toBe('Carlos Drummond');
    expect(account.role).toBe('admin');
    expect(account.initials).toBe('CD');
  });

  it('mockAccountToUser maps MockAccount correctly to User', () => {
    const account: MockAccount = {
      id: 'persona-1',
      name: 'Marina Alves',
      email: 'marina@exemplo.com',
      role: 'investor',
      initials: 'MA',
    };

    const user = mockAccountToUser(account);
    expect(user.id).toBe('persona-1');
    expect(user.fullName).toBe('Marina Alves');
    expect(user.roles).toEqual(['User']);
  });
});
