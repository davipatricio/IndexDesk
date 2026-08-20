import { NextResponse } from 'next/server';
import type { NextRequest } from 'next/server';

export function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl;

  if (pathname.startsWith('/admin')) {
    const refreshToken = request.cookies.get('refresh_token')?.value;
    const sessionHint = request.cookies.get('session_hint')?.value;
    const indexdeskSession = request.cookies.get('indexdesk-session')?.value;

    const hasSession = Boolean(refreshToken || sessionHint || indexdeskSession);

    if (!hasSession) {
      const signInUrl = new URL('/entrar', request.url);
      signInUrl.searchParams.set('redirect', pathname);
      return NextResponse.redirect(signInUrl);
    }
  }

  return NextResponse.next();
}

export const config = {
  matcher: ['/admin', '/admin/:path*'],
};
