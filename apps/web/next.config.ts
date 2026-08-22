import type { NextConfig } from 'next';
import { withSerwist } from '@serwist/turbopack';

const nextConfig: NextConfig = {
  reactStrictMode: true,
  poweredByHeader: false,
  turbopack: {},
  cacheComponents: true,
  partialPrefetching: true,
  reactCompiler: true,
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
