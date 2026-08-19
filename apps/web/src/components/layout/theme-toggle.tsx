'use client';

import { useTheme } from 'next-themes';
import { Button } from '@/components/ui/button';
import { useMounted } from '@/hooks/use-mounted';
import { Sun, Moon, Laptop } from 'lucide-react';

/**
 * Three-state theme toggle (light -> dark -> system -> light).
 *
 * Renders a blank placeholder until mounted on the client, which avoids a
 * hydration mismatch when the server render does not know the user's
 * preferred mode.
 */
export function ThemeToggle() {
  const mounted = useMounted();
  const { theme, setTheme } = useTheme();

  if (!mounted) {
    return (
      <Button
        variant="ghost"
        size="icon-sm"
        aria-label="Alternar tema"
        disabled
        className="text-muted-foreground opacity-50"
      >
        <Sun className="size-4" />
      </Button>
    );
  }

  const cycleTheme = () => {
    if (theme === 'light') setTheme('dark');
    else if (theme === 'dark') setTheme('system');
    else setTheme('light');
  };

  const label =
    theme === 'light'
      ? 'Tema claro (clique para modo escuro)'
      : theme === 'dark'
        ? 'Tema escuro (clique para seguir o sistema)'
        : 'Seguindo o sistema (clique para modo claro)';

  return (
    <Button
      variant="ghost"
      size="icon-sm"
      onClick={cycleTheme}
      aria-label={label}
      title={label}
      className="text-muted-foreground hover:text-foreground"
    >
      {theme === 'light' ? (
        <Sun className="size-4" />
      ) : theme === 'dark' ? (
        <Moon className="size-4" />
      ) : (
        <Laptop className="size-4" />
      )}
    </Button>
  );
}
