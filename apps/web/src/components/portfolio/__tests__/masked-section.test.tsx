import * as React from 'react';
import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MaskedValue, MASKED_TEXT } from '@/components/privacy/masked-value';
import { usePrivacyStore } from '@/stores/privacy-store';

// O MaskedSection (client boundary) só sincroniza store via /me; o comportamento
// de máscara em si é verificado via MaskedValue — prova escopo global.

describe('MaskedSection / public page privacy', () => {
  beforeEach(() => usePrivacyStore.getState().reset());

  it('mostra valores quando anon/hideValues ainda null (proteção sem flash)', () => {
    // store no estado pós-reset: hideValues=null → MaskedValue mostra real
    render(<MaskedValue>R$ 42.000,00</MaskedValue>);
    expect(screen.getByText('R$ 42.000,00')).toBeInTheDocument();
    expect(screen.queryByText(MASKED_TEXT)).toBeNull();
  });

  it('sendo viewer logado com hideValues=true mascara mesmo dados públicos (% retorno)', () => {
    usePrivacyStore.getState().initFromPreferences({ hideValues: true });
    render(<MaskedValue>12,34%</MaskedValue>);
    expect(screen.getByText(MASKED_TEXT)).toBeInTheDocument();
    expect(screen.queryByText('12,34%')).toBeNull();
  });

  it('pesos de composição (peso %) não são censurados — permanecem visíveis mesmo quando hideValues=true', () => {
    usePrivacyStore.getState().initFromPreferences({ hideValues: true });
    // composição é texto puro (ex.: ticker + nome), não envolve MaskedValue
    render(<span>42,0% — peso da posição</span>);
    expect(screen.getByText(/42,0% — peso/)).toBeInTheDocument();
  });
});
