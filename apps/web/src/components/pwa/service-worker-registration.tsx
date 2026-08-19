'use client';

import * as React from 'react';

export function ServiceWorkerRegistration() {
  React.useEffect(() => {
    if (process.env.NODE_ENV === 'production' && 'serviceWorker' in navigator) {
      navigator.serviceWorker.register('/serwist/sw.js', { scope: '/' }).catch(() => {
        // Offline support is optional; the app remains usable without registration.
      });
    }
  }, []);

  return null;
}
