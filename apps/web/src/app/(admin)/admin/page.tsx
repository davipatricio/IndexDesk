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
              Área de gestão
            </Badge>
          </div>
          <p className="text-muted-foreground text-sm">
            Gestão do catálogo de ativos, composição de carteiras e acompanhamento das atualizações.
          </p>
        </div>

        <Button size="sm" className="gap-1.5">
          <RefreshCw className="size-3.5" />
          Atualizar catálogo
        </Button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        <Card>
          <CardHeader>
            <CardTitle className="text-sm font-semibold flex items-center gap-1.5 text-foreground">
              <Upload className="size-4 text-primary" />
              Importar composição de carteira
            </CardTitle>
            <CardDescription className="text-xs">
              Atualize os ativos e suas principais posições
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-3">
            <p className="text-xs text-muted-foreground">
              Envie a composição da carteira para revisar posições e manter as informações
              organizadas.
            </p>
            <Button variant="outline" size="sm" className="w-full">
              Selecionar arquivo
            </Button>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="text-sm font-semibold flex items-center gap-1.5 text-foreground">
              <FileText className="size-4 text-primary" />
              Publicar notícias e relatórios
            </CardTitle>
            <CardDescription className="text-xs">
              Análises e comunicados para investidores
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
              Atualizações do catálogo
            </CardTitle>
            <CardDescription className="text-xs">
              Acompanhe quando as informações foram atualizadas
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-3 text-xs text-muted-foreground">
            <p>Consulte o andamento das atualizações de mercado e dos materiais publicados.</p>
            <div className="rounded-lg border border-dashed p-3 text-center text-xs text-muted-foreground bg-muted/20">
              O status das atualizações aparecerá aqui quando houver novidades.
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
