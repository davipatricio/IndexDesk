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
| **Brapi.dev** | Freemium R$29–99 | 10–1000 req/min | Cotações B3 + proventos (data COM/EX) |
| **Yahoo Finance** (não oficial) | Grátis | ~2000 req/IP/h | Benchmarks `^BVSP ^GSPC ^IXIC`, câmbio `USDBRL=X`, ouro `GC=F` |
| **FMP / HG Brasil** | Freemium | 250–500 req/dia | Contingência (UCITS/cotações) |

## Agendamentos (Quartz.NET)

BCB **23:00 UTC** diário · CVM informe **~04:00** · Brapi pós-fechamento · IPCA mensal · holdings semanal.
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

Somente `.env` / appsettings: `ConnectionStrings__{Postgres,Redis,RabbitMQ}`, `Providers__{Brapi__ApiKey,BCB__BaseUrl,CVM__BaseUrl,ANBIMA__BaseUrl,FMP__ApiKey,HGBrasil__ApiKey}`. Frontend lê apenas `NEXT_PUBLIC_API_URL`. Nunca em código/commits; nunca logar tokens/dados financeiros em spans OTel.
