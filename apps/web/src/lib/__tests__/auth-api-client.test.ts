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
import { userToAccount } from '@/stores/session-store';
import type { User, AuthResponse } from '@/types/auth';

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

  it('signIn successfully saves token and returns AuthResponse on 200', async () => {
    const mockAuthResponse: AuthResponse = {
      accessToken: 'access-jwt-abc',
      expiresIn: 900,
      user: {
        id: 'usr-1',
        email: 'investor@example.com',
        fullName: 'Investidor Real',
        roles: ['User'],
        permissions: ['catalog:read'],
      },
    };

    const mockFetch = vi.fn().mockResolvedValue(
      new Response(JSON.stringify(mockAuthResponse), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    );
    vi.stubGlobal('fetch', mockFetch);

    const res = await signIn({
      email: 'investor@example.com',
      password: 'password123',
    });

    expect(res.accessToken).toBe('access-jwt-abc');
    expect(res.user.email).toBe('investor@example.com');
    expect(res.user.roles).toContain('User');
    expect(getAccessToken()).toBe('access-jwt-abc');
  });

  it('signIn throws error on non-200 responses', async () => {
    const mockFetch = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ message: 'Credenciais inválidas' }), {
        status: 401,
        headers: { 'Content-Type': 'application/json' },
      }),
    );
    vi.stubGlobal('fetch', mockFetch);

    await expect(
      signIn({
        email: 'wrong@example.com',
        password: 'wrongpassword',
      }),
    ).rejects.toThrow('Credenciais inválidas');
  });

  it('signUp successfully saves token and returns AuthResponse on 200', async () => {
    const mockAuthResponse: AuthResponse = {
      accessToken: 'access-jwt-xyz',
      expiresIn: 900,
      user: {
        id: 'usr-2',
        email: 'newuser@example.com',
        fullName: 'Novo Investidor',
        roles: ['User'],
        permissions: ['catalog:read'],
      },
    };

    const mockFetch = vi.fn().mockResolvedValue(
      new Response(JSON.stringify(mockAuthResponse), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    );
    vi.stubGlobal('fetch', mockFetch);

    const res = await signUp({
      email: 'newuser@example.com',
      password: 'password123',
      fullName: 'Novo Investidor',
    });

    expect(res.user.email).toBe('newuser@example.com');
    expect(res.user.fullName).toBe('Novo Investidor');
    expect(res.user.roles).toContain('User');
    expect(getAccessToken()).toBe('access-jwt-xyz');
  });

  it('signOut clears accessToken and calls signout endpoint', async () => {
    setAccessToken('active-token');
    const mockFetch = vi.fn().mockResolvedValue(new Response(null, { status: 200 }));
    vi.stubGlobal('fetch', mockFetch);

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
      fullName: 'Investidor Real',
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

  it('userToAccount maps User with Admin role to admin UserAccount', () => {
    const user: User = {
      id: 'usr-1',
      email: 'gestor@indexdesk.com',
      fullName: 'Carlos Drummond',
      roles: ['Admin'],
      permissions: ['assets:write'],
    };

    const account = userToAccount(user);
    expect(account.id).toBe('usr-1');
    expect(account.name).toBe('Carlos Drummond');
    expect(account.role).toBe('admin');
    expect(account.initials).toBe('CD');
  });
});
