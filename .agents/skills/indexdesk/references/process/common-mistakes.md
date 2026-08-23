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
6. ❌ Assumir taxonomia de resiliência "ainda não wired"
   → ✅ desde a Fase 4 provider-sync existe breaker por provider (`ProviderResilience`), pool de
   chaves (`IApiKeyPool`) e códigos soft `Provider.CircuitOpen`/`Provider.PoolExhausted` que viram
   PARTIAL_WARNING (não erro duro); `CreateDefaultHttpPipeline` continua para HTTP genérico.

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
21. ❌ Logar os argumentos do processo sidecar (`SidecarProcessRunner`)
    → ✅ o cookie TV vai no argv do filho e a key InfoMoney no env do filho — logue só o
    executável/timeout; tail de stderr citado em erros já é truncado (4000 chars).
22. ❌ Assumir que `bash -c <script>` lê um arquivo
    → ✅ `-c` executa a string como comando: arquivo sem +x dá exit 126; nos testes use
    `exec bash '<path>' "$@"`. Kill em timeout precisa `Kill(entireProcessTree: true)`
    (filhos como `sleep &` sobrevivem ao kill simples).

## Ingestão / providers (provider-sync)

31. ❌ Chamar Brapi por ticker no fluxo diário ("1 req por ativo")
    → ✅ UMA chamada batch `/quote/list` por dia útil (`GetDailyBatchQuotesAsync`); resposta
    anônima ignora o filtro `tickers` — filtrar client-side. Proventos: fila espaçada ≥7 s
    (`Providers:Brapi:DividendSpacingMs`). Bulk history nunca via Brapi.
32. ❌ Assumir que o CSV do Investo/It Now traz coluna de ticker
    → ✅ layout real do Investo (08/2026): "Ativo | Peso" com **nomes de empresas**, sem
    ticker, mais uma tabela "País" (exposição) que não é holding. Parser classifica pelo header;
    holdings sem ticker dedupe por nome (`etf_holdings.holding_ticker` é nullable e NULLs são
    distintos em índice unique — dedupe por ticker NÃO cobre essas linhas).
33. ❌ Hardcodar o id/hash do produto iShares na URL do CSV
    → ✅ o link `.ajax?fileType=csv` rotaciona: extrair por regex da página do produto
    (`ISharesHoldingsParser.ExtractAjaxCsvUrl`); mapa ticker→página em config.
34. ❌ Tratar `itnow.com.br` como garantidamente acessível
    → ✅ DNS já falhou (NXDOMAIN) nesta máquina em 08/2026 — feeds de gestoras podem falhar
    por rede/geo/WAF; cada fonte = PARTIAL_WARNING isolado, job segue com as demais.
35. ❌ Colocar FX em `macro_economic_series`
    → ✅ contrato FX tem bid/ask (sem OHLCV/volume) — usar `fx_rates` (PK pair+date,
    bid como proxy de close).
36. ❌ Chamar TV com ticker bare ou login email/senha (`TV_EMAIL`/`TV_PASSWORD`)
    → ✅ símbolo exige prefixo `BMFBOVESPA:` e a autenticação é **COOKIE de sessão autenticado**
    (`Providers__TradingView__Cookie`; tv-scraper 1.5.x não tem fluxo de senha). Export de cookies
    SEM `sessionid`/JWT não autentica — falha com `TradingView.AuthFailed`.
37. ❌ Escrever logs/erros do sidecar no stdout ("só um print a mais")
    → ✅ stdout = NDJSON **puro** (contrato v1); todo erro vai como envelope JSON no stderr
    (`{"error":{"code":...}}`, exit 2/3/4) — qualquer linha extra no stdout quebra o parser C#
    com `Sidecar.ParseError`.
38. ❌ Assumir que Brapi anônimo cobre o catálogo
    → ✅ anônimo é **whitelist-only** (observado: só PETR4/VALE3; resto = 401 `MISSING_TOKEN`)
    e o batch `/quote/list` ignora o filtro `tickers` (vem o mercado inteiro). Token/chave do
    pool obrigatórios para o fluxo real.
39. ❌ Montar URL SPDR com ticker maiúsculo (`holdings-daily-us-en-SPY.xlsx`)
    → ✅ SSGA é case-sensitive: ticker **minúsculo** (`...us-en-spy.xlsx` = 200; `SPY.xlsx` = 404).
40. ❌ Interpretar peso pt-BR com vírgula decimal como fração ("1,32" → ×100)
    → ✅ vírgula decimal já são pontos percentuais (RENT3 "1,32" = 1,32%); fração só dot-only
    começando com "0." (regra em `TryParseWeight`; caso Alphabet/132% corrigido com regressão).
41. ❌ Esperar que `EnsureCreated` adicione tabelas novas num banco existente
    → ✅ EF `EnsureCreated` **não migra** DB já criado: `etf_holdings` e `fx_rates` precisaram de
    DDL aditivo manual no Postgres dev (ou migration) — rodar sync contra banco velho falha se
    a tabela não existir.
42. ❌ Contornar WAF Akamai spoofando User-Agent/headers no HttpClient/curl nativo
    → ✅ Akamai valida **fingerprint TLS (JA3)**, não headers — 403 "Access Denied" mesmo com
    headers completos de browser. Usar o transporte sidecar (`sidecar fetch` / `ISidecarHttp`,
    curl_cffi impersonate=chrome); It Now default `Transport=sidecar`.
43. ❌ Usar o apex `itnow.com.br`
    → ✅ apex tem NXDOMAIN — host real é **`www.itnow.com.br`** (BaseUrl default já aponta pra lá).

## Processo

23. ❌ Editar `ROADMAP.md` direto
    → ✅ editar `.roadmap/**/*.json` → `roadmap:validate` → `roadmap:generate`.
24. ❌ Assumir banco/Docker ativos ao rodar testes/migrations
    → ✅ FND-013 pendente: containers podem estar parados por decisão do dono; SDK usa
    `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`.
25. ❌ Travar campo manualmente editando linha no banco
    → ✅ curadoria via `locked_fields`/`is_manually_overridden` — o sync ignora campos travados.
26. ❌ Encerrar tarefa sem atualizar roadmap + skill interna
    → ✅ ver seção "Skills internas" do CLAUDE.md raiz (definição de pronto).
27. ❌ Passar cores computadas (`getComputedStyle`) a charts canvas (lightweight-charts)
    → ✅ tokens Tailwind v4 resolvem p/ `lab()`/`oklch()`; usar `lib/chart-colors.ts` → hex.
28. ❌ Rodar `dotnet build -c Release`/`dotnet test` com o watch ligado
    → ✅ colide com `obj/` do watch e mata/recompila em loop; parar o dev ou aceitar rebuild lento.
29. ❌ Contar eventos com LEFT JOIN + `COUNT(*)`
    → ✅ asset sem proventos retorna 1 linha nula contada como 1; usar `COUNT(d."Id")`.
30. ❌ Confiar no dev server do Next após HMR pesado
    → ✅ Turbopack pode morrer com panic interno (`turbo-tasks ... Aborting`); reiniciar e revalidar
    as rotas tocadas.
