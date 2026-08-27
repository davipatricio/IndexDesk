'use client';

import * as React from 'react';
import { usePrivacyStore } from '@/stores/privacy-store';

/**
 * Atalho global do dashboard: Ctrl/Cmd + . alterna "esconder dados".
 * Ignora quando o foco está em input/textarea/contenteditable.
 */
export function PrivacyHotkey({ children }: { children: React.ReactNode }) {
  const hideValues = usePrivacyStore((s) => s.hideValues);
  const setHideValues = usePrivacyStore((s) => s.setHideValues);
  const hydrated = usePrivacyStore((s) => s.hydrated);

  React.useEffect(() => {
    if (!hydrated) return;
    const handler = (event: KeyboardEvent) => {
      if ((event.ctrlKey || event.metaKey) && event.key === '.') {
        const target = event.target as HTMLElement | null;
        const tagName = target?.tagName?.toLowerCase();
        if (
          tagName === 'input' ||
          tagName === 'textarea' ||
          tagName === 'select' ||
          target?.isContentEditable
        ) {
          return;
        }
        event.preventDefault();
        setHideValues(hideValues !== true);
      }
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [hideValues, setHideValues, hydrated]);

  return <>{children}</>;
}
