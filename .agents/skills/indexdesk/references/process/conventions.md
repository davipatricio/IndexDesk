# Convenções Transversais (Comandos, Qualidade, Idioma, Rotas)

Fontes: `CLAUDE.md` raiz + `apps/*/CLAUDE.md`.

## Comandos (da raiz, via Turborepo — ponto de entrada único)

```bash
bun run dev            # next dev --turbopack + dotnet watch
bun run build          # next build + dotnet build -c Release
bun run lint           # oxlint . + dotnet format analyzers
bun run typecheck      # tsc --noEmit + analyzers/build .NET
bun run test           # vitest run + dotnet test
bun run format         # oxfmt . + dotnet csharpier format
bun run format:check   # gate CI: oxfmt --check + csharpier check
bun run roadmap:validate | roadmap:generate | roadmap:check   # tracking do roadmap
```

Por workspace (`cd apps/web` ou `cd apps/backend`): comandos equivalentes via bun/dotnet.
Turbo cacheia `build` (.next/**; bin/**+obj/**) e `test` (coverage/**, TestResults/**).
`globalDependencies`: `.env*`, `tsconfig.json`, `Directory.Build.props`, `.editorconfig` — editar invalida caches.

## Quality gates

- JS/TS: Oxlint presets TS/React/Next (correctness/imports/typescript = error); Oxfmt ignora `.next/out/dist/coverage/node_modules`.
- tsconfig moderno: `module: preserve`, `moduleResolution: bundler`, `noEmit`, strict, `verbatimModuleSyntax`,
  `isolatedModules`, `noUncheckedIndexedAccess`.
- C#: CSharpier = whitespace; `dotnet format style/analyzers` = estilo/diagnósticos. **Nunca** `dotnet format whitespace`.
- Falha de quality gate retorna código ≠ 0.

## Idioma da UI (pt-BR) — regras rígidas

- Copy visível, SEO metadata, labels acessíveis, loading/error em pt-BR natural para investidores.
- **Proibido expor:** "API", "backend", "endpoint", "worker", "local-first", "dados persistidos", "ingestão",
  IDs de roadmap (ex.: MVP-011). Tais termos ficam só em comentários/DTOs/query keys/docs de arquitetura.
- **Nunca renderizar exceção crua.** Mapear para mensagens estáveis ex.: "Não foi possível carregar os ativos
  agora. Tente novamente em instantes." Detalhes técnicos só em diagnóstico interno.

## Visibilidade de rotas

- **Toda página Next.js é pública por default**, incluindo `/admin` e ferramentas. `(public)`/`(admin)` são apenas
  route groups organizacionais — sem controle de acesso.
- **Não adicionar** middleware nem gate de autenticação por página. Auth é opcional e nunca bloqueia páginas
  públicas/catálogo/calculadoras. `src/middleware.ts` é pass-through explícito; testes de middleware mantêm
  cobertura de acesso anônimo a `/admin` sem redirect.
- Mesmo público, mercado-dados usa **apenas** a camada local (sem fixtures/sintéticos/provedores diretos),
  com estados honestos de carregando/vazio/indisponível/retry.

## Contratos

- DTOs do frontend alinhados ao OpenAPI gerado; não criar campos paralelos mockados.
- Novo dado no frontend ⇒ novo endpoint no backend (nunca provider direto) + teste unitário (calculadora/endpoint)
  + teste de integração (grupo de endpoints).
- Commits: manter o bloco `BEGIN/END:nextjs-agent-rules` nos CLAUDE.md scoped (regenerado pelo next dev).

## Ambiente local (quirks conhecidos)

- SDK .NET local roda com `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` (libicu ausente sem sudo).
- Containers Docker podem estar parados por decisão do dono → migrations FND-013 pendentes; não assumir banco ativo.
