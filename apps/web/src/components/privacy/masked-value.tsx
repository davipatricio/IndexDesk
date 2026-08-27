'use client';

import { cn } from '@/lib/utils';
import { usePrivacyStore } from '@/stores/privacy-store';

/** Dots estáveis renderizados quando `hideValues` está ativo. */
export const MASKED_TEXT = '••••';

export function MaskedValue({
  children,
  className,
  maskedClassName,
}: {
  children: React.ReactNode;
  className?: string;
  /** Classes extras aplicadas ao •••• (ex.: largura reservada). */
  maskedClassName?: string;
}) {
  const hideValues = usePrivacyStore((s) => s.hideValues);

  if (hideValues !== true) {
    return <span className={cn('tabular-nums', className)}>{children}</span>;
  }

  return (
    <span
      className={cn('inline-block tabular-nums select-none', className, maskedClassName)}
      aria-label="Valor oculto"
      title="Valores ocultos — pressione Ctrl+. para mostrar"
      aria-hidden={false}
    >
      {MASKED_TEXT}
    </span>
  );
}

/**
 * Envolve um gráfico e o desfoca (sem interação) enquanto os valores estiverem
 * ocultos. Visual-only; não segura estado.
 */
export function BlurChart({
  children,
  className,
}: {
  children: React.ReactNode;
  className?: string;
}) {
  const hideValues = usePrivacyStore((s) => s.hideValues);
  const masked = hideValues === true;

  return (
    <div
      className={cn(
        'transition-[filter] duration-150',
        masked && 'blur-sm select-none pointer-events-none',
        className,
      )}
      aria-hidden={masked}
    >
      {children}
    </div>
  );
}

/**
 * `true` com os valores ocultos; `false` = visível; `null` = /me ainda não
 * carregou — renderize neutro (nem escondido nem mostrado) até resolver.
 */
export function useHideValues(): boolean | null {
  return usePrivacyStore((s) => s.hideValues);
}
