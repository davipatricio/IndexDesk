import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Shield, RefreshCw, Upload, FileText, CheckCircle2 } from 'lucide-react';

export default function AdminDashboardPage() {
  return (
    <div className="container mx-auto px-4 py-8 flex flex-col gap-6">
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b pb-6">
        <div className="flex flex-col gap-1.5">
          <div className="flex items-center gap-2">
            <h1 className="text-3xl font-extrabold tracking-tight flex items-center gap-2">
              <Shield className="size-7 text-emerald-500" />
              IndexDesk Backoffice & Curadoria
            </h1>
            <Badge variant="destructive" className="text-xs">
              Área Restrita (RBAC)
            </Badge>
          </div>
          <p className="text-muted-foreground text-sm">
            Gestão de ativos, uploads manuais de holdings CSV, sincronização Quartz.NET e auditoria.
          </p>
        </div>

        <Button size="sm" className="gap-1.5 text-xs">
          <RefreshCw className="size-3.5" />
          Disparar Sync Manual
        </Button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        <Card>
          <CardHeader>
            <CardTitle className="text-sm font-semibold flex items-center gap-1.5">
              <Upload className="size-4 text-emerald-500" />
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
            <Button variant="outline" size="sm" className="text-xs w-full">
              Selecionar Arquivo CSV
            </Button>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-sm font-semibold flex items-center gap-1.5">
              <FileText className="size-4 text-emerald-500" />
              Publicação de Notícias & Fatos Relevantes
            </CardTitle>
            <CardDescription className="text-xs">
              Research letters de gestoras e comunicados CVM
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-3">
            <p className="text-xs text-muted-foreground">
              Crie notas de análise, avisos aos cotistas e cartas mensais vinculadas aos tickers.
            </p>
            <Button variant="outline" size="sm" className="text-xs w-full">
              Nova Publicação
            </Button>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-sm font-semibold flex items-center gap-1.5">
              <CheckCircle2 className="size-4 text-emerald-500" />
              Status dos Jobs Quartz.NET
            </CardTitle>
            <CardDescription className="text-xs">
              Monitoramento de ingestão em background
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-2 text-xs">
            <div className="flex justify-between items-center p-2 rounded bg-muted/40 font-mono">
              <span>BCB Ingest (23:00 UTC)</span>
              <Badge className="text-[10px] bg-emerald-500/15 text-emerald-500">Ativo</Badge>
            </div>
            <div className="flex justify-between items-center p-2 rounded bg-muted/40 font-mono">
              <span>CVM Streaming (04:00 AM)</span>
              <Badge className="text-[10px] bg-emerald-500/15 text-emerald-500">Ativo</Badge>
            </div>
            <div className="flex justify-between items-center p-2 rounded bg-muted/40 font-mono">
              <span>Brapi Fechamento B3</span>
              <Badge className="text-[10px] bg-emerald-500/15 text-emerald-500">Ativo</Badge>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
