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
};

export default withSerwist({
  ...nextConfig,
});
