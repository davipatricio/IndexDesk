# Erros Comuns — Checklist antes de commit/PR

Formato: ❌ errado → ✅ certo. Consolidado dos docs do repo e do código real.
Regras completas por tema nos arquivos de [`../..`](../../SKILL.md) (mapa no SKILL.md).

## Arquitetura / dados

1. ❌ Chamar provider externo (BCB, CVM, Brapi, Yahoo) de request path ou frontend
   → ✅ ingestão **só** em jobs/serviços do `IndexDesk.Worker`; frontend só fala com a API local.
2. ❌ Hardcodar CDI/Selic/IPCA, feriado ou alíquota fiscal no código/componente
   → ✅ ler `macro_economic_series` (SGS 11/12/433/189) e `etf_metadata.*`.
3. ❌ Calcular retorno com `close`
   → ✅ sempre `adj_close` (splits/inplits distorcem a série).
4. ❌ Ingestão com insert simples
   → ✅ upsert/COPY idempotente; reexecutar o mesmo dia atualiza, nunca duplica.
5. ❌ Módulo referenciando outro módulo (`Modules.Auth` → `Modules.MarketData`)
   → ✅ módulos só referenciam `BuildingBlocks/*`; hosts compõem.
6. ❌ Assumir que circuit breaker/rate limiter por provider já estão ativos
   → ✅ só existe `CreateDefaultHttpPipeline` (retry+timeout); wiring é pendente.

## Regras de negócio (mercado/fisco)

7. ❌ Assumir isenção de R$20k/mês para ETF ou BDR
   → ✅ isenção vale **só para ações**; ETF/BDR geram DARF 6015 sobre qualquer lucro.
8. ❌ Inferir classe do ativo pelo sufixo do ticker (ex.: termina em 11 = ETF)
   → ✅ usar `assets.asset_type` (`ETF|BDR_ETF|STOCK|INDEX`).
9. ❌ Usar tabela regressiva p/ ETF de renda fixa
   → ✅ Lei 13.043/14: 15% fixo retido na fonte, sem come-cotas.
10. ❌ Apresentar resultado fiscal sem premissas/data-fonte/disclaimer
    → ✅ toda saída fiscal é educacional e mostra premissas (RISK-003).

## Frontend (`apps/web`)

11. ❌ Mockar/fixturar/sintetizar resposta quando a API falha ("fallback amigável" no catálogo)
    → ✅ array vazio da API é estado vazio válido; erro vira `Error` → UI honesta de erro/retry.
12. ❌ Adicionar middleware/gate de autenticação em páginas
    → ✅ todas as páginas são públicas por design (inclusive `/admin`); `middleware.ts` é pass-through.
13. ❌ Jargão técnico no copy pt-BR: "API", "backend", "endpoint", "worker", "ingestão", "MVP-011"
    → ✅ linguagem natural de investidor; erros mapeados p/ mensagens estáveis.
14. ❌ npm/yarn/pnpm · imports relativos entre pastas · hand-roll de primitivo
    → ✅ Bun · aliases `@/...` · `bunx shadcn@latest add <component>`.
15. ❌ Deletar o bloco `BEGIN/END:nextjs-agent-rules` dos CLAUDE.md scoped
    → ✅ é regenerado pelo `next dev`; mantê-lo evita diff sujo.
16. ❌ Ler `node_modules/next/dist/docs` como se fosse Next.js antigo
    → ✅ esta versão tem breaking changes — consultar os guias antes de escrever código.

## Backend (.NET)

17. ❌ Rodar `dotnet format whitespace`
    → ✅ CSharpier é dono do whitespace; `dotnet format` fica com style/analyzers.
18. ❌ Remover `public partial class Program {}` do Api
    → ✅ exigido pelo `WebApplicationFactory` nos testes de integração.
19. ❌ Trocar fallback in-memory de cache (`InMemoryCacheFallback` / `WorkerInMemoryCacheFallback`)
    → ✅ Redis pode estar fora localmente; o fallback é intencional.
20. ❌ Logar senha/token/hash ou colocar segredo em appsettings commitado
    → ✅ segredos via `.env`/env vars; logs nunca contêm credenciais.

## Processo

21. ❌ Editar `ROADMAP.md` direto
    → ✅ editar `.roadmap/**/*.json` → `roadmap:validate` → `roadmap:generate`.
22. ❌ Assumir banco/Docker ativos ao rodar testes/migrations
    → ✅ FND-013 pendente: containers podem estar parados por decisão do dono; SDK usa
    `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`.
23. ❌ Travar campo manualmente editando linha no banco
    → ✅ curadoria via `locked_fields`/`is_manually_overridden` — o sync ignora campos travados.
24. ❌ Encerrar tarefa sem atualizar roadmap + skill interna
    → ✅ ver seção "Skills internas" do CLAUDE.md raiz (definição de pronto).
