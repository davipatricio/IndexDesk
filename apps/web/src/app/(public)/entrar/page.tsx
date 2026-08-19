'use client';

import * as React from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { MOCK_ACCOUNTS, type MockAccount } from '@/lib/mock-accounts';
import { useSession } from '@/hooks/use-session';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Avatar, AvatarFallback } from '@/components/ui/avatar';
import { Badge } from '@/components/ui/badge';
import { ArrowLeft, Check, Shield, User } from 'lucide-react';

export default function EntrarPage() {
  const router = useRouter();
  const { account: currentAccount, signIn, signOut } = useSession();
  const [selectedId, setSelectedId] = React.useState<string>(
    () => currentAccount?.id ?? MOCK_ACCOUNTS[0]?.id ?? '',
  );

  const handleSelect = (account: MockAccount) => {
    setSelectedId(account.id);
    signIn(account);
  };

  const handleContinue = () => {
    router.push('/');
  };

  return (
    <div className="container mx-auto px-4 py-12 flex flex-col items-center justify-center min-h-[70vh]">
      <div className="w-full max-w-md flex flex-col gap-6">
        <Link href="/">
          <Button variant="ghost" size="sm" className="gap-1.5 text-muted-foreground -ml-2">
            <ArrowLeft className="size-3.5" />
            Voltar ao início
          </Button>
        </Link>

        <Card>
          <CardHeader>
            <CardTitle className="text-xl">Entrar no IndexDesk</CardTitle>
            <CardDescription>
              Ambiente de demonstração local. Escolha uma persona para testar a experiência com ou
              sem permissões administrativas:
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-4">
            <div className="flex flex-col gap-2" role="radiogroup" aria-label="Personas de teste">
              {MOCK_ACCOUNTS.map((account) => {
                const isSelected = selectedId === account.id;
                return (
                  <button
                    type="button"
                    key={account.id}
                    onClick={() => handleSelect(account)}
                    role="radio"
                    aria-checked={isSelected}
                    className={`flex items-center justify-between p-3 rounded-lg border text-left transition-colors cursor-pointer ${
                      isSelected
                        ? 'border-primary bg-primary/5 ring-1 ring-primary'
                        : 'border-border hover:bg-muted/50'
                    }`}
                  >
                    <div className="flex items-center gap-3">
                      <Avatar>
                        <AvatarFallback className="bg-primary/10 text-primary font-semibold text-xs">
                          {account.initials}
                        </AvatarFallback>
                      </Avatar>
                      <div className="flex flex-col">
                        <span className="font-semibold text-sm text-foreground">
                          {account.name}
                        </span>
                        <span className="text-xs text-muted-foreground">{account.email}</span>
                      </div>
                    </div>

                    <div className="flex items-center gap-2">
                      <Badge variant={account.role === 'admin' ? 'default' : 'secondary'}>
                        {account.role === 'admin' ? (
                          <span className="inline-flex items-center gap-1">
                            <Shield className="size-3" />
                            Admin
                          </span>
                        ) : (
                          <span className="inline-flex items-center gap-1">
                            <User className="size-3" />
                            Investidor
                          </span>
                        )}
                      </Badge>
                      {isSelected && <Check className="size-4 text-primary" />}
                    </div>
                  </button>
                );
              })}
            </div>

            <div className="flex flex-col gap-2 pt-2">
              <Button onClick={handleContinue} className="w-full">
                Continuar como {MOCK_ACCOUNTS.find((a) => a.id === selectedId)?.name ?? 'usuário'}
              </Button>

              {currentAccount && (
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => signOut()}
                  className="text-muted-foreground"
                >
                  Sair da sessão atual
                </Button>
              )}
            </div>

            <p className="text-[11px] text-muted-foreground text-center border-t pt-3">
              Autenticação simulada no navegador. Nenhuma credencial real é transmitida.
            </p>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
