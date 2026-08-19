export function Footer() {
  return (
    <footer className="border-t py-6 md:py-8 bg-muted/30 text-xs text-muted-foreground">
      <div className="container mx-auto px-4 flex flex-col md:flex-row items-center justify-between gap-4">
        <p>© 2026 IndexDesk. Plataforma de Inteligência de ETFs e BDRs da B3.</p>
        <p className="text-center md:text-right">
          Aviso legal: Rentabilidade passada não é garantia de rentabilidade futura. Este site não
          constitui recomendação de investimento.
        </p>
      </div>
    </footer>
  );
}
