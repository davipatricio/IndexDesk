'use client';

import { useSyncExternalStore } from 'react';

const emptySubscribe = () => () => {};

/**
 * Returns `true` once the component has mounted on the client.
 *
 * Hydration-safe by construction: `useSyncExternalStore` serves `false` on the
 * server and `true` after hydration, so the first client render matches the
 * server output and React never warns about a mismatch. Prefer this over a
 * `useEffect` + `setState` flag — that pattern trips the `set-state-in-effect`
 * lint rule and starts an extra render.
 */
export function useMounted(): boolean {
  return useSyncExternalStore(
    emptySubscribe,
    () => true,
    () => false,
  );
}
