# Decisões em Aberto & Riscos

Snapshot do roadmap (2026-08-22). Estado atual: `.roadmap/**/decisions.json` e `risks.json` → `ROADMAP.md`.

## Decisões

| ID | Tema | Status | Recomendação registrada |
| :--- | :--- | :---: | :--- |
| **DEC-001** (P0) | Bloqueio editorial de metadados | open | `locked_fields` JSONB é a persistência canônica por campo; `is_manually_overridden` = flag de conveniência; `metadata_lock` apenas contrato de API. |
| **DEC-002** (P0) | Política de cache histórico/SLOs | open | TTLs por classe (quotes fechadas 30d, intraday 15min, macro 24h, holdings 7d); medir hit rate e latência local separadamente. |
| **DEC-003** (P0) | TimescaleDB self-hosted vs Postgres puro | open | Começar TimescaleDB em Docker; validar RAM/backup/compressão; manter migrations compatíveis com particionamento nativo PG18 como fallback. |
| **DEC-004** (P1) | Provedores de contingência + storage editorial | open | Escolher pós-spike por custo/cobertura; abstrair provider/storage atrás de interfaces; páginas públicas não acopladas ao fornecedor. |
| **DEC-005** (P1) | TanStack DB + biblioteca primária de gráficos | open | Lightweight Charts p/ séries longas; spike TanStack DB × Store × IndexedDB p/ camada offline; Recharts para agregados. |
| **DEC-007** (P0) | Toolchain de qualidade frontend/C# | open | Oxlint/Oxfmt latest; TS moderno (preserve/bundler/noEmit); fixar TS 7 só quando publicado e validado com Next/Turbopack; CSharpier + dotnet format analyzers/style. |
| **DEC-006** (P0) | Local-first estrito nas calculadoras | **accepted** | Calculadoras/páginas públicas leem só Postgres/Redis; Worker ingere tudo em background. |
| **DEC-008** (P1) | Auto-retomada de sync pós-downtime (Sync Bootstrap) | **accepted** | `IHostedService` no boot do Worker detecta gap por `max(Date) asset_quotes` vs último dia útil B3 (`market_holidays`+`BusinessDayCalculator`) e roda `IDailyCloseSyncService.SyncDailyCloseAsync(targetDate: dia)` dia-a-dia, do mais antigo ao mais novo (cap `Sync:CatchUp:MaxBacklogDays`, default 30). Para dias passados o `DailyCloseSyncService` pula batch Brapi + fila de proventos (Brapi free só expõe "hoje") e roda só Yahoo→TV. Single-writer: advisory lock PG `BackfillSyncLockId` (`0x4241434B46494C4C` = "BACKFILL") compartilhado com CLI `--backfill` e endpoints `POST /api/v1/assets/sync/{daily,backfill}` — uma única instância de escrita por vez (resposta `409 Sync.LockBusy` na API, abort no CLI). Idempotência dos upserts garante retomada limpa após crash. |

### Iniciativa provider-sync (plans/provider-sync-scrapers.md) — decisões de implementação

Registradas em 2026-08-23 durante a execução das Fases 0–5 (namespace próprio `DEC-PS-*`
para não colidir com a numeração do roadmap; refinam o eixo "provedores" de DEC-004).

| ID | Tema | Status | Recomendação registrada |
| :--- | :--- | :---: | :--- |
| **DEC-PS-01** | Sidecar Python p/ fetch de providers | **accepted** | Fetch externo delegado ao processo Python `tools/providers/sidecar` (uv; `yfinance==1.6.0`, `tv-scraper==1.5.1`, `curl-cffi==0.16.1` pinados) via `SidecarProcessRunner`; contrato NDJSON v1 no stdout (erros JSON no stderr; exit 0/2/3/4); .NET orquestra (Quartz/upsert idempotente), Python só transporta anti-bloqueio (curl_cffi impersonate=chrome). |
| **DEC-PS-02** | TradingView como fonte OHLCV slot 3 | **accepted** | `tv history` (tv-scraper `CandleStreamer`, pin/fork consciente — protocolo privado), símbolos `BMFBOVESPA:TICKER`, auth por **COOKIE autenticado** (lib sem login email/senha), chunking interno por payloads ≥4500 bars flaky; dividends vazio por design; job dedicado 22:30 UTC MON–FRI. |
| **DEC-PS-03** | HG Brasil removido da cadeia | **accepted** | Descoberto pago (não freemium): fora da coleção `IMarketDataClient`; client compilando inativo; contingência de câmbio herdada pela AwesomeAPI (`fx_rates`, bid=proxy de close). Reativação só com decisão explícita de custo. |
| **DEC-PS-04** | InfoMoney secundária-fora-da-cadeia | **accepted** | Key APIM pública do frontend + WAF TLS → acesso só via sidecar `im`; série sem adjclose (não substitui Brapi/Yahoo p/ backtest); vocabulário B3 nativo nos proventos (JSCP ≠ DIVIDENDO); uso apenas por `--backfill --provider infomoney` e validação cruzada; nunca primária (ToS não-oficial + chave revogável). |
| **DEC-PS-05** | Transporte sidecar `fetch` p/ fontes WAF'd | **accepted** | Hosts com fingerprinting TLS Akamai (It Now hoje; XP/InfoMoney mesmo padrão) vão pelo comando genérico `sidecar fetch` (`ISidecarHttp`/`SidecarHttp`) — UA spoof nativo descartado (JA3), binário curl-impersonate descartado (segundo runtime); `Providers:Holdings:ItNow:Transport=sidecar` default com degradação p/ nativo. |

## Riscos

| ID | Risco | P×I | Mitigação |
| :--- | :--- | :---: | :--- |
| RISK-001 | APIs não oficiais instáveis | high×high | Adapters isolados, snapshot local, Polly, circuit breaker, logs sync, contingência, publicação manual. |
| RISK-002 | Custo operacional Timescale/mensageria | med×high | Spike de medição, profiles Docker opcionais, fallback particionamento PG18. |
| RISK-003 | Precisão fiscal/regulatória | med×**critical** | Metadados versionados, fonte/competência explícitas, revisão editorial, disclaimers, testes de cenário; nunca tratar como aconselhamento fiscal. |
| RISK-004 | Streaming CVM/arquivos grandes | med×high | Stream CsvHelper, filtro CNPJ, COPY, idempotência, métricas, testes com arquivos reais. |
| RISK-005 | Serwist+Turbopack offline | med×high | Versionar cache, testar update/rollback do SW, separar público×autenticado, validar em dispositivos reais. |
| RISK-006 | Segurança backoffice/uploads | med×critical | RBAC forte, auditoria, sanitização, MIME/tamanho, storage privado, URLs assinadas, override por campo. |
| RISK-007 | SEO programático duplicado/fino | med×high | Whitelist de pares, canonical, sitemap segmentado, noindex em páginas finas, monitorar Search Console. |
| RISK-008 | Entrega notificações/storage editorial | med×med | Conversão opcional, consentimento na Phase 02, abstrações com retry e opt-out imediato. |
| RISK-009 | Backlog de catch-up do bootstrap (Yahoo rate-limit) | med×med | `MaxBacklogDays` default 30 + throttle 200 ms do `DailyCloseSyncService` (~5 req/s); operacionais longos viram `PARTIAL_WARNING` e o ciclo seguinte completa; Brapi não usado no catch-up (orçamento diário). |

## Como decidir

Antes de fechar uma DEC: propor implementação seguindo a recomendação registrada, validar com o dono do repo,
marcar status em `.roadmap/**/decisions.json`, rodar `bun run roadmap:validate && bun run roadmap:generate`.
