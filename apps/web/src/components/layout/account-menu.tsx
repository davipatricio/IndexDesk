'use client';

import Link from 'next/link';
import { useSession } from '@/hooks/use-session';
import { Button } from '@/components/ui/button';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { LogOut, UserCheck } from 'lucide-react';

/**
 * Header account widget.
 *
 * - When not mounted / not ready: renders neutral placeholder buttons (no flash).
 * - When logged out: shows `Entrar` and `Criar conta` buttons.
 * - When logged in: shows an avatar + dropdown with identity, a role badge, and `Sair`.
 *   The `/admin` backoffice link returns together with the admin module (MVP-015/016).
 */
export function AccountMenu() {
  const { account, isAuthenticated, isReady, signOut } = useSession();

  if (!isReady) {
    return (
      <div className="flex items-center gap-2">
        <Button variant="ghost" size="sm" disabled className="opacity-60">
          Entrar
        </Button>
      </div>
    );
  }

  if (!isAuthenticated || !account) {
    return (
      <div className="flex items-center gap-2">
        <Link href="/entrar">
          <Button variant="ghost" size="sm">
            Entrar
          </Button>
        </Link>
        <Link href="/entrar">
          <Button size="sm" className="hidden sm:inline-flex">
            Criar conta
          </Button>
        </Link>
      </div>
    );
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={
          <Button
            variant="ghost"
            size="sm"
            className="gap-2 px-1.5 hover:bg-accent focus-visible:ring-ring"
            aria-label={`Menu de ${account.name}`}
          />
        }
      >
        <Avatar size="sm">
          <AvatarFallback className="bg-primary/10 text-primary font-semibold text-xs">
            {account.initials}
          </AvatarFallback>
        </Avatar>
        <span className="hidden md:inline text-xs font-medium max-w-[120px] truncate text-foreground">
          {account.name.split(' ')[0]}
        </span>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-56">
        <DropdownMenuLabel className="font-normal">
          <div className="flex flex-col gap-0.5">
            <p className="text-sm font-semibold leading-none text-foreground">{account.name}</p>
            <p className="text-xs leading-none text-muted-foreground">{account.email}</p>
            <div className="mt-1 flex items-center gap-1 text-[11px] text-muted-foreground">
              <UserCheck className="size-3 text-primary" />
              <span>{account.role === 'admin' ? 'Administrador' : 'Investidor'}</span>
            </div>
          </div>
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuItem render={<Link href="/dashboard" />}>Minhas carteiras</DropdownMenuItem>

        <DropdownMenuItem
          variant="destructive"
          onClick={() => signOut()}
          className="cursor-pointer"
        >
          <LogOut className="size-4 mr-1.5" />
          <span>Sair da conta</span>
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
