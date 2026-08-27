import { describe, it, expect } from 'vitest';
import { Sparkline } from '../sparkline';
import { render } from '@testing-library/react';

describe('Sparkline', () => {
  it('renderiza linha tracejada cinza quando data está vazia', () => {
    const { container } = render(<Sparkline data={[]} width={120} height={28} />);
    const line = container.querySelector('line');
    expect(line).toBeInTheDocument();
    expect(line).toHaveAttribute('stroke-dasharray', '3 3');
  });

  it('renderiza linha tracejada quando há apenas 1 ponto', () => {
    const { container } = render(<Sparkline data={[100]} width={120} height={28} />);
    const line = container.querySelector('line');
    expect(line).toBeInTheDocument();
  });

  it('renderiza caminho com classe text-positive quando último ponto > primeiro ponto', () => {
    const { container } = render(<Sparkline data={[100, 105, 110]} width={120} height={28} />);
    const svg = container.querySelector('svg');
    expect(svg).toHaveClass('text-positive');
    const paths = container.querySelectorAll('path');
    expect(paths).toHaveLength(2); // area + line
  });

  it('renderiza caminho com classe text-negative quando último ponto < primeiro ponto', () => {
    const { container } = render(<Sparkline data={[110, 105, 95]} width={120} height={28} />);
    const svg = container.querySelector('svg');
    expect(svg).toHaveClass('text-negative');
  });

  it('renderiza classe text-muted-foreground quando a série é flat', () => {
    const { container } = render(<Sparkline data={[100, 100, 100]} width={120} height={28} />);
    const svg = container.querySelector('svg');
    expect(svg).toHaveClass('text-muted-foreground');
  });

  it('aceita prop values como retrocompatibilidade', () => {
    const { container } = render(<Sparkline values={[50, 75]} width={100} height={32} />);
    const svg = container.querySelector('svg');
    expect(svg).toHaveClass('text-positive');
  });
});
