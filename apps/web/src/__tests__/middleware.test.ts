import { describe, it, expect } from 'vitest';
import { NextRequest } from 'next/server';
import { middleware } from '../middleware';

describe('Next.js Edge Middleware', () => {
  it('redirects unauthenticated user accessing /admin to /entrar with redirect param', () => {
    const req = new NextRequest('http://localhost:3000/admin/holdings');
    const res = middleware(req);

    expect(res.status).toBe(307);
    const location = res.headers.get('location');
    expect(location).toContain('/entrar?redirect=%2Fadmin%2Fholdings');
  });

  it('allows access to /admin when refresh_token cookie is present', () => {
    const req = new NextRequest('http://localhost:3000/admin', {
      headers: {
        cookie: 'refresh_token=valid-refresh-token-123',
      },
    });
    const res = middleware(req);

    expect(res.status).toBe(200);
    expect(res.headers.get('location')).toBeNull();
  });

  it('allows access to /admin when indexdesk-session cookie is present', () => {
    const req = new NextRequest('http://localhost:3000/admin', {
      headers: {
        cookie: 'indexdesk-session=session-id-456',
      },
    });
    const res = middleware(req);

    expect(res.status).toBe(200);
  });

  it('allows non-admin routes freely without cookies', () => {
    const req = new NextRequest('http://localhost:3000/etf/IVVB11');
    const res = middleware(req);

    expect(res.status).toBe(200);
  });
});
