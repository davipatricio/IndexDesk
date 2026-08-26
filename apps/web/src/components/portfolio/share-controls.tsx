'use client';

import * as React from 'react';
import { useRouter } from 'next/navigation';
import {
  regenerateShareLink,
  revokeShareLink,
  updateVisibility,
  type VisibilityResultDto,
} from '@/lib/api-client';
import { Button } from '@/components/ui/button';
import { toast } from 'sonner';

const options = [
  { value: 'private', label: 'Privada' },
  { value: 'public', label: 'Pública' },
  { value: 'link', label: 'Só com link' },
] as const;

/**
 * Controles de visibilidade/link restrito (M-P5). Token claro aparece uma única vez.
 */
export function ShareControls({
  portfolioId,
  initial,
}: {
  portfolioId: string;
  initial: { visibility: VisibilityResultDto['visibility'] };
}) {
  const router = useRouter();
  const [visibility, setVisibility] = React.useState(initial.visibility);
  const [slug, setSlug] = React.useState<string | null>(null);
  const [shareUrl, setShareUrl] = React.useState<string | null>(null);
  // Quem chega num modo "só com link" normalmente já tem token ativo; as ações
  // abaixo corrigem o estado com a resposta real do backend.
  const [hasActiveLink, setHasActiveLink] = React.useState(initial.visibility === 'link');
  const [busy, setBusy] = React.useState(false);

  const change = async (value: (typeof options)[number]['value']) => {
    setBusy(true);
    try {
      const result = await updateVisibility(portfolioId, value);
      setVisibility(result.visibility);
      setSlug(result.slug);
      setHasActiveLink(result.hasShareToken);
      toast.success(
        result.visibility === 'private'
          ? 'Carteira privada.'
          : result.visibility === 'public'
            ? 'Carteira pública — indexável em buscadores.'
            : 'Visível só pelo link restrito.',
      );
      router.refresh();
    } catch (error) {
      toast.error((error as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const generateLink = async () => {
    setBusy(true);
    try {
      const result = await regenerateShareLink(portfolioId, 30);
      setVisibility(result.visibility);
      setSlug(result.slug);
      const origin = typeof window !== 'undefined' ? window.location.origin : '';
      setShareUrl(`${origin}/c/${result.slug}?t=${result.shareToken}`);
      setHasActiveLink(true);
      toast.success('Link gerado — copie agora, ele não será exibido de novo.');
    } catch (error) {
      toast.error((error as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const revoke = async () => {
    setBusy(true);
    try {
      await revokeShareLink(portfolioId);
      setShareUrl(null);
      setHasActiveLink(false);
      toast.success('Link revogado.');
      router.refresh();
    } catch (error) {
      toast.error((error as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const copyLink = async () => {
    if (!shareUrl) return;
    try {
      await navigator.clipboard.writeText(shareUrl);
      toast.success('Copiado.');
    } catch {
      // clipboard indisponível fora de contexto seguro — não deixa o clique morrer.
      toast.error('Não foi possível copiar automaticamente. Selecione o link e copie manualmente.');
    }
  };

  return (
    <div className="space-y-2 rounded-lg border p-3">
      <p className="text-xs font-medium">Compartilhamento</p>
      <div className="flex flex-wrap gap-1">
        {options.map((o) => (
          <button
            key={o.value}
            type="button"
            disabled={busy}
            onClick={() => void change(o.value)}
            className={`rounded-md px-2 py-1 text-xs transition-colors ${
              visibility === o.value
                ? 'bg-primary text-primary-foreground'
                : 'text-muted-foreground hover:bg-muted'
            }`}
          >
            {o.label}
          </button>
        ))}
      </div>

      {visibility === 'public' && slug ? (
        <p className="text-[11px] text-muted-foreground">
          Público em{' '}
          <a
            href={`/c/${slug}`}
            target="_blank"
            rel="noreferrer"
            className="text-primary hover:underline"
          >
            /c/{slug}
          </a>
        </p>
      ) : null}

      {visibility === 'link' ? (
        <div className="space-y-2">
          <Button variant="outline" size="sm" disabled={busy} onClick={() => void generateLink()}>
            Gerar novo link
          </Button>
          <p className="text-[11px] text-muted-foreground">
            Se já existir um link ativo, gerar um novo invalida o anterior.
          </p>
          {shareUrl ? (
            <div className="space-y-1">
              <input
                readOnly
                aria-label="Link restrito da carteira"
                value={shareUrl}
                className="w-full rounded-md border bg-muted/40 px-2 py-1 text-[11px]"
                onFocus={(e) => e.currentTarget.select()}
              />
              <Button size="xs" onClick={() => void copyLink()}>
                Copiar
              </Button>
              <p className="text-[10px] text-red-600">Este token não será mostrado novamente.</p>
            </div>
          ) : null}
          {hasActiveLink || shareUrl ? (
            <Button variant="ghost" size="xs" disabled={busy} onClick={() => void revoke()}>
              Revogar link
            </Button>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}
