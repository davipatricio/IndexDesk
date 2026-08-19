export default function Loading() {
  return (
    <div className="container mx-auto px-4 py-24 flex flex-col items-center justify-center gap-3">
      <div className="size-8 rounded-full border-2 border-emerald-500 border-t-transparent animate-spin" />
      <span className="text-xs text-muted-foreground font-medium">
        Carregando inteligência de mercado...
      </span>
    </div>
  );
}
