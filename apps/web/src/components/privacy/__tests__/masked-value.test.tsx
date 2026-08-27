import * as React from 'react';
import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MaskedValue, BlurChart, MASKED_TEXT } from '../masked-value';
import { usePrivacyStore } from '@/stores/privacy-store';

describe('MaskedValue & BlurChart', () => {
  beforeEach(() => {
    usePrivacyStore.getState().reset();
  });

  it('renderiza os filhos normalmente quando hideValues não é true', () => {
    usePrivacyStore.getState().initFromPreferences({ hideValues: false });
    render(<MaskedValue>R$ 150.000,00</MaskedValue>);
    expect(screen.getByText('R$ 150.000,00')).toBeInTheDocument();
  });

  it('renderiza bullets estáveis quando hideValues é true', () => {
    usePrivacyStore.getState().initFromPreferences({ hideValues: true });
    render(<MaskedValue>R$ 150.000,00</MaskedValue>);
    expect(screen.getByText(MASKED_TEXT)).toBeInTheDocument();
    expect(screen.queryByText('R$ 150.000,00')).toBeNull();
  });

  it('BlurChart aplica classes de blur e desativa ponteiro quando hideValues é true', () => {
    usePrivacyStore.getState().initFromPreferences({ hideValues: true });
    const { container } = render(
      <BlurChart>
        <div data-testid="chart">Chart content</div>
      </BlurChart>,
    );
    const wrapper = container.firstChild as HTMLElement;
    expect(wrapper).toHaveClass('blur-sm');
    expect(wrapper).toHaveClass('pointer-events-none');
    expect(wrapper).toHaveAttribute('aria-hidden', 'true');
  });

  it('BlurChart não desfoca quando hideValues é false', () => {
    usePrivacyStore.getState().initFromPreferences({ hideValues: false });
    const { container } = render(
      <BlurChart>
        <div data-testid="chart">Chart content</div>
      </BlurChart>,
    );
    const wrapper = container.firstChild as HTMLElement;
    expect(wrapper).not.toHaveClass('blur-sm');
    expect(wrapper).toHaveAttribute('aria-hidden', 'false');
  });
});
