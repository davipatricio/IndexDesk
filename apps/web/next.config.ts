import type { NextConfig } from 'next';
import { withSerwist } from '@serwist/turbopack';

const nextConfig: NextConfig = {
  reactStrictMode: true,
  poweredByHeader: false,
  turbopack: {},
  cacheComponents: true,
  partialPrefetching: true,
  reactCompiler: true,
  // Acesso via túneis efêmeros (Cloudflare quick tunnels): origem do navegador é
  // *.trycloudflare.com, diferente de localhost — sem isto o dev server responde
  // 403 nos chunks /_next/*.
  allowedDevOrigins: ['*.trycloudflare.com'],
  experimental: {
    optimizePackageImports: ['lucide-react', 'recharts'],
    turbopackMemoryEviction: 'full',
    turbopackRustReactCompiler: true,
  },
  async rewrites() {
    return [
      {
        source: '/api/v1/:path*',
        destination: 'http://127.0.0.1:5000/api/v1/:path*',
      },
    ];
  },
};

export default withSerwist({
  ...nextConfig,
});
