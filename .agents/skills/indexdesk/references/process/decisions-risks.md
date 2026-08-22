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

## Como decidir

Antes de fechar uma DEC: propor implementação seguindo a recomendação registrada, validar com o dono do repo,
marcar status em `.roadmap/**/decisions.json`, rodar `bun run roadmap:validate && bun run roadmap:generate`.
