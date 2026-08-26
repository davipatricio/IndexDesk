# Provedores, Ingestão & Cache (Local-First)

Fontes integrais: `PROVIDERS.md` + `PROVIDERS_SYNC.md`.

## Princípio Local-First (NON-NEGOTIABLE)

Nenhuma requisição de usuário chama API externa em tempo real. Backtest/comparação consulta **apenas**
Postgres + Redis local (<~10ms). Provedores são lidos **somente** por jobs agendados do `IndexDesk.Worker`.
Fluxo: Worker → Postgres → evento RabbitMQ → `Modules.Analytics` invalida chaves Redis afetadas → reaque do Postgres.

## Provedores e limites

| Provedor | Custo | Rate limit | Uso |
| :--- | :--- | :--- | :--- |
| **BCB SGS** (API REST pública) | Grátis | ~100 req/min | CDI (`12`), Selic (`11`), IPCA (`433`), IGP-M (`189`) |
| **CVM Informe Diário** | Grátis | sem limite rígido | Cota/PL/cotistas diários (D+1) |
| **CVM CDA** (mensal) | Grátis | sem limite rígido | Holdings & overlap ETFs nacionais |
| **B3 dados abertos** | Grátis | download programático | Carteiras teóricas IBOV/SMLL/IDIV, ISIN |
| **ANBIMA** | Grátis | sem limite rígido | Feriados até 2099 (base 252), índices IMA-B/IDA |
| **Feeds gestoras** (iShares/Investo/Vanguard) | Grátis | CSVs públicos | Holdings diários oficiais |
| **Brapi.dev** | Free · ciclo **15k req** | dados +30 min · 1 ativo/req | Batch diário + catálogo (`/available`, `/quote/list` com type/subType). **Histórico >3mo e dividendos = paywall Startup (400/403)** — ver `PROVIDERS.md` §2.5 |
| **Yahoo Finance** (não oficial) | Grátis | ~2000 req/IP/h | **Fonte primária efetiva de histórico e proventos** (ações/BDRs/FIIs ricos; ETFs B3 vazios). Benchmarks `^BVSP ^GSPC ^IXIC`, câmbio `USDBRL=X`, ouro `GC=F`; overflow natural do Brapi em guardrail (tickers `.SA`) |
| **B3 sistemaswebb3-listados** (sidecar `b3`) | Grátis | Akamai (warm-up cookies) | Catálogos oficiais: ~3500 empresas (CNPJ+codeCVM), FIIs listados (ticker+nome) — metadata cadastral |
| **Bora Investir** | Grátis | WordPress REST aberto | Universo ETF 519 tickers + gestor/índice/geografia; sem candles/proventos |
| **COTAHIST** (candidato, não implementado) | Grátis | zip público sem auth | Redundância OHLCV oficial futura; sem adj_close/proventos |
| **FMP / HG Brasil** | FMP freemium · HG Brasil **pago (não contratado)** | FMP 250–500 req/dia | Contingência (UCITS/cotações). HG Brasil: client mantido, **sem key por ora**, desativado |

## Agendamentos (Quartz.NET)

BCB **23:00 UTC** diário · CVM informe **~04:00** · Brapi pós-fechamento (nunca pollar <30 min — delay upstream) · IPCA mensal · holdings semanal.
Implementado (Fase 3 provider-sync): `MarketDataDailySyncJob` **22:00 UTC MON-FRI** (1 batch Brapi + proventos espaçados ≥7 s + gap fill Yahoo/TV sidecar) · `FxRatesDailySyncJob` **22:05 UTC MON-FRI** (AwesomeAPI → `fx_rates`) · `TradingViewDailySyncJob` **22:30 UTC MON-FRI** · `HoldingsWeeklySyncJob` **sáb 08:00 UTC** (`0 0 8 ? * SAT *`). Cadeia OHLCV declarativa: Brapi → YahooSidecar → TVSidecar; InfoMoney fora da cadeia (backfill explícito).
**Prioridade efetiva por uso (26/08, `PROVIDERS.md` §1.2):** daily close mantém Brapi no slot 1 (batch barato de 1 request); histórico/backfill e proventos = Yahoo direto (CLI `--provider yahoo`); catálogo = Brapi ⊕ Bora Investir ⊕ B3 sidecar `b3 companies|fiis`.
Cada job: schedule configurável, correlation id, grava em `sync_job_logs`, falha de um provider não derruba os demais.

## Pipeline CVM streaming (padrão para arquivos grandes)

`inf_diario_fi_YYYYMM.zip` → CsvHelper em stream → filtro estrito por CNPJs mapeados **antes** do COPY →
`NpgsqlBinaryImporter` (COPY binário) → 50k+ linhas/s sem pico de RAM. Reexecução idempotente.
Campos: `CNPJ_Fundo, DT_COMPTC, VL_QUOTA, VL_PATRIM_LIQ, NR_COTST`. CDA: `TP_APLIC, CD_ATIVO, QT_TIT, VL_MERC`.

## Resiliência (Polly)

Retry com backoff exponencial respeitando rate limit · circuit breaker (abre/recupera) · rate limiter por provider.

## TTLs de cache Redis (por classe)

| Dado | TTL |
| :--- | :--- |
| Cotações históricas fechadas | 30 dias / infinito (passado não muda) |
| Dado do dia vigente (pregão 10–18h) | 15 minutos |
| Indicadores macro (CDI/Selic/IPCA/feriados) | 24 horas |
| Holdings/CDA/gestoras | 7 dias |

Invalidação orientada a eventos: invalidar somente as chaves afetadas. Política formal pendente em DEC-002.

## Segredos

Somente `.env` / appsettings: `ConnectionStrings__{Postgres,Redis,RabbitMQ}`, `Providers__{Brapi__ApiKey,BCB__BaseUrl,CVM__BaseUrl,ANBIMA__BaseUrl,FMP__ApiKey,HGBrasil__ApiKey,AwesomeApi__Token,TradingView__Cookie,InfoMoney__SubscriptionKeys__0}` (HGBrasil **sem valor por ora** — plano pago não contratado; client mantido desativado). Frontend lê apenas `NEXT_PUBLIC_API_URL`. Nunca em código/commits; nunca logar tokens/dados financeiros em spans OTel.
