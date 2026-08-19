import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { Button } from '../button';

describe('Button component', () => {
  it('renders with default text', () => {
    render(<Button>Simular Carteira</Button>);
    const btn = screen.getByRole('button', { name: /simular carteira/i });
    expect(btn).toBeInTheDocument();
  });

  it('applies variant classes correctly', () => {
    const { container } = render(<Button variant="outline">Comparar</Button>);
    expect(container.firstChild).toHaveClass('border');
  });

  it('is disabled when disabled prop is provided', () => {
    render(<Button disabled>Aguarde...</Button>);
    const btn = screen.getByRole('button', { name: /aguarde/i });
    expect(btn).toBeDisabled();
  });
});
