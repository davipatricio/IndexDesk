import { NextResponse } from 'next/server';
import type { NextRequest } from 'next/server';

/**
 * All Next.js pages are publicly reachable. Authentication is an optional
 * account capability and must not prevent a route from rendering.
 */
export function middleware(_request: NextRequest) {
  return NextResponse.next();
}

export const config = {
  matcher: ['/((?!api|_next/static|_next/image|favicon.ico).*)'],
};
