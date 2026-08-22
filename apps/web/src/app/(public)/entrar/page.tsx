'use client';

import * as React from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useSession } from '@/hooks/use-session';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { ArrowLeft, Loader2, LogIn, UserPlus } from 'lucide-react';

export default function EntrarPage() {
  const router = useRouter();
  const { user, signIn, signUp, signOut } = useSession();

  const [activeTab, setActiveTab] = React.useState<'signin' | 'signup'>('signin');
  const [email, setEmail] = React.useState('');
  const [password, setPassword] = React.useState('');
  const [fullName, setFullName] = React.useState('');
  const [error, setError] = React.useState<string | null>(null);
  const [isLoading, setIsLoading] = React.useState(false);

  const handleSignIn = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setIsLoading(true);

    try {
      await signIn({ email, password });
      router.push('/');
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Falha na autenticação';
      setError(message);
    } finally {
      setIsLoading(false);
    }
  };

  const handleSignUp = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setIsLoading(true);

    try {
      await signUp({ fullName, email, password });
      router.push('/');
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Falha no cadastro';
      setError(message);
    } finally {
      setIsLoading(false);
    }
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
            <CardTitle className="text-xl">Acesso ao IndexDesk</CardTitle>
            <CardDescription>
              Acesse sua conta ou cadastre-se para sincronizar carteiras e gerenciar ativos.
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-4">
            {user ? (
              <div className="flex flex-col gap-4 py-2">
                <div className="p-3 bg-muted rounded-lg text-sm">
                  <p className="font-semibold text-foreground">{user.fullName}</p>
                  <p className="text-xs text-muted-foreground">{user.email}</p>
                </div>
                <Button onClick={() => router.push('/')} className="w-full">
                  Continuar navegando
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => signOut()}
                  className="w-full text-muted-foreground"
                >
                  Sair da conta
                </Button>
              </div>
            ) : (
              <Tabs
                value={activeTab}
                onValueChange={(v) => {
                  setActiveTab(v as 'signin' | 'signup');
                  setError(null);
                }}
              >
                <TabsList className="grid w-full grid-cols-2 mb-4">
                  <TabsTrigger value="signin" className="gap-1.5">
                    <LogIn className="size-3.5" />
                    Entrar
                  </TabsTrigger>
                  <TabsTrigger value="signup" className="gap-1.5">
                    <UserPlus className="size-3.5" />
                    Cadastrar
                  </TabsTrigger>
                </TabsList>

                {error && (
                  <div className="p-3 mb-4 rounded-md bg-destructive/10 border border-destructive/20 text-destructive text-sm">
                    {error}
                  </div>
                )}

                <TabsContent value="signin">
                  <form onSubmit={handleSignIn} className="flex flex-col gap-4">
                    <div className="flex flex-col gap-1.5">
                      <label className="text-xs font-medium text-foreground" htmlFor="signin-email">
                        Email
                      </label>
                      <Input
                        id="signin-email"
                        type="email"
                        placeholder="seu@email.com"
                        value={email}
                        onChange={(e) => setEmail(e.target.value)}
                        required
                        disabled={isLoading}
                      />
                    </div>
                    <div className="flex flex-col gap-1.5">
                      <label
                        className="text-xs font-medium text-foreground"
                        htmlFor="signin-password"
                      >
                        Senha
                      </label>
                      <Input
                        id="signin-password"
                        type="password"
                        placeholder="••••••••"
                        value={password}
                        onChange={(e) => setPassword(e.target.value)}
                        required
                        disabled={isLoading}
                      />
                    </div>
                    <Button type="submit" className="w-full mt-2" disabled={isLoading}>
                      {isLoading ? (
                        <>
                          <Loader2 className="size-4 animate-spin mr-2" />
                          Entrando...
                        </>
                      ) : (
                        'Entrar'
                      )}
                    </Button>
                  </form>
                </TabsContent>

                <TabsContent value="signup">
                  <form onSubmit={handleSignUp} className="flex flex-col gap-4">
                    <div className="flex flex-col gap-1.5">
                      <label
                        className="text-xs font-medium text-foreground"
                        htmlFor="signup-fullname"
                      >
                        Nome Completo
                      </label>
                      <Input
                        id="signup-fullname"
                        type="text"
                        placeholder="João da Silva"
                        value={fullName}
                        onChange={(e) => setFullName(e.target.value)}
                        required
                        disabled={isLoading}
                      />
                    </div>
                    <div className="flex flex-col gap-1.5">
                      <label className="text-xs font-medium text-foreground" htmlFor="signup-email">
                        Email
                      </label>
                      <Input
                        id="signup-email"
                        type="email"
                        placeholder="seu@email.com"
                        value={email}
                        onChange={(e) => setEmail(e.target.value)}
                        required
                        disabled={isLoading}
                      />
                    </div>
                    <div className="flex flex-col gap-1.5">
                      <label
                        className="text-xs font-medium text-foreground"
                        htmlFor="signup-password"
                      >
                        Senha
                      </label>
                      <Input
                        id="signup-password"
                        type="password"
                        placeholder="Mínimo 8 caracteres"
                        value={password}
                        onChange={(e) => setPassword(e.target.value)}
                        required
                        minLength={8}
                        disabled={isLoading}
                      />
                    </div>
                    <Button type="submit" className="w-full mt-2" disabled={isLoading}>
                      {isLoading ? (
                        <>
                          <Loader2 className="size-4 animate-spin mr-2" />
                          Criando conta...
                        </>
                      ) : (
                        'Criar Conta'
                      )}
                    </Button>
                  </form>
                </TabsContent>
              </Tabs>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
