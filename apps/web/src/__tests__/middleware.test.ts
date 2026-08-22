import { describe, it, expect } from 'vitest';
import { NextRequest } from 'next/server';
import { middleware } from '../middleware';

describe('Next.js Edge Middleware', () => {
  it.each(['/admin', '/admin/holdings', '/ferramentas/backtest', '/entrar', '/etf/IVVB11'])(
    'allows %s without an authenticated session',
    (pathname) => {
      const req = new NextRequest(`http://localhost:3000${pathname}`);
      const res = middleware(req);

      expect(res.status).toBe(200);
      expect(res.headers.get('location')).toBeNull();
    },
  );

  it('allows public pages with query parameters without cookies', () => {
    const req = new NextRequest('http://localhost:3000/ferramentas/backtest?ticker=VWRA11');
    const res = middleware(req);

    expect(res.status).toBe(200);
    expect(res.headers.get('location')).toBeNull();
  });
});
