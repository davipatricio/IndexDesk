import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Shield, RefreshCw, Upload, FileText, Activity } from 'lucide-react';

export default function AdminDashboardPage() {
  return (
    <div className="container mx-auto px-4 py-8 flex flex-col gap-6">
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b pb-6">
        <div className="flex flex-col gap-1.5">
          <div className="flex items-center gap-2">
            <h1 className="text-2xl sm:text-3xl font-bold tracking-tight flex items-center gap-2 text-foreground">
              <Shield className="size-6 text-primary" />
              Backoffice & Curadoria
            </h1>
            <Badge variant="secondary" className="text-xs">
              Acesso Administrativo
            </Badge>
          </div>
          <p className="text-muted-foreground text-sm">
            Gestão do catálogo de ativos, upload de carteiras teóricas e monitoramento de ingestão.
          </p>
        </div>

        <Button size="sm" className="gap-1.5">
          <RefreshCw className="size-3.5" />
          Disparar Sincronização
        </Button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        <Card>
          <CardHeader>
            <CardTitle className="text-sm font-semibold flex items-center gap-1.5 text-foreground">
              <Upload className="size-4 text-primary" />
              Upload Manual de Holdings (CSV)
            </CardTitle>
            <CardDescription className="text-xs">
              Ingestão de composição de carteira (CDA CVM ou Gestoras)
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-3">
            <p className="text-xs text-muted-foreground">
              Envie arquivos CSV diários das gestoras (BlackRock, Vanguard, Investo) com trava de
              sobreposição manual.
            </p>
            <Button variant="outline" size="sm" className="w-full">
              Selecionar Arquivo CSV
            </Button>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-sm font-semibold flex items-center gap-1.5 text-foreground">
              <FileText className="size-4 text-primary" />
              Publicação de Notícias & Relatórios
            </CardTitle>
            <CardDescription className="text-xs">
              Research letters de gestoras e comunicados CVM
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-3">
            <p className="text-xs text-muted-foreground">
              Crie notas de análise, avisos aos cotistas e cartas mensais vinculadas aos tickers.
            </p>
            <Button variant="outline" size="sm" className="w-full">
              Nova Publicação
            </Button>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-sm font-semibold flex items-center gap-1.5 text-foreground">
              <Activity className="size-4 text-primary" />
              Rotinas de Sincronização
            </CardTitle>
            <CardDescription className="text-xs">
              Monitoramento de ingestão em background
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-3 text-xs text-muted-foreground">
            <p>
              As tarefas agendadas são executadas pelo worker de dados (BCB, CVM e cotações B3).
            </p>
            <div className="rounded-lg border border-dashed p-3 text-center text-xs text-muted-foreground bg-muted/20">
              Status das rotinas em tempo real disponível quando conectado à API.
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
