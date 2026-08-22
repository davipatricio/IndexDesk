# SEO Programático, Ferramentas Públicas & Conversão

Fonte integral: `SEO_TOOLS.md`. Filosofia: ferramenta 100% pública e interativa → gatilho de conversão
não-intrusivo → cadastro opcional (retenção/LTV). Nada fica atrás de login.

## Infraestrutura SEO obrigatória

- **SSG/ISR por ticker** (`/etf/[ticker]`, `/bdr/[ticker]`): HTML completo para crawlers.
- **JSON-LD:** `FinancialProduct` + `BreadcrumbList` nas páginas de ativo; `WebApplication`/`SoftwareApplication`
  + `FAQPage` nas ferramentas.
- **OG images dinâmicas (`@vercel/og`, 1200×630):** card com logo, cotação R$, retorno 12M, mini-gráfico;
  comparadores exibem as duas linhas sobrepostas.
- **Sitemaps segmentados** (`sitemap-assets.xml`, `sitemap-comparisons.xml`, `sitemap-tools.xml`) gerados em
  runtime e cacheados; `lastmod` = timestamp da última cotação ingerida pelo Worker. Páginas finas: canonical/noindex.
- Whitelist de comparações no MVP; expansão ilimitada só na Phase 02 (evitar conteúdo fino — RISK-007).

## Rotas programáticas (hubs)

| Rota | H1 / intenção |
| :--- | :--- |
| `/etfs/renda-fixa` | ETFs RF da B3 (sem come-cotas) |
| `/etfs/dividendos` | ETFs de dividendos (DIVO11, NSDV11…) |
| `/etfs/sp500` | Comparativo IVVB11 × SPXI11 × SPXB11 |
| `/bdrs/etfs-internacionais` | Guia de BDRs de ETFs globais |
| `/gestoras/[gestora]` | ETFs por gestora (BlackRock, Investo, Itaú…) |
| `/relatorios`, `/noticias` | Cartas mensais / fatos relevantes |
| `/rankings` | Hub de rankings (retorno/risco/liquidez × classe) — **implementado**; expansão programática `/rankings/[metrica]` e hubs por índice (`/indices/[indice]`, requer `index_provider`/`index_family` em `etf_metadata`) ficam na Phase 02 |

## Ferramentas públicas (URL · resolve · gatilho de conversão)

1. **Comparador 1v1** `/comparador/[t1]-vs-[t2]` · rentabilidade normalizada + tabela fiscal · "adicionar 3º ativo".
2. **Tax drag / domicílio fiscal** `/ferramentas/calculadora-tax-drag-irlanda` · retenção 30% EUA vs 15% Irlanda
   vs ganho de capital B3 em 10/20/30 anos · PDF com memória de cálculo.
3. **Overlap matrix** `/ferramentas/overlap` (+ `/overlap/[t1]-[t2]`) · % de ações repetidas ponderadas ·
   overlap da carteira inteira (>2 ativos).
4. **Rendimento real (Fisher)** `/ferramentas/calculadora-rendimento-real-ipca` · desinflacionamento exato com
   IPCA histórico do SGS · simular carteira completa.
5. **Equivalência RF** `/ferramentas/equivalencia-renda-fixa` · CDB tributado vs LCI/LCA isenta vs ETF RF
   (Lei 13.043/14) · alertas de oportunidade.
6. **Regra dos 4% / FIRE** `/ferramentas/simulador-regra-4-por-cento` · montante necessário p/ renda passiva · salvar plano.
7. **DARF ETF** `/ferramentas/calculadora-darf-etf` · lucro, IR 15%/20%, código DARF **6015**, vencimento
   (último dia útil do mês subsequente), alerta de ausência da isenção R$20k · importar notas de corretagem.
8. **Fluxo CVM** `/ferramentas/fluxo-etfs-cvm` · rankings D+1: captação líquida, novos cotistas, volume ·
   relatório semanal WhatsApp/e-mail.

## Gatilhos de conversão (lead magnets éticos)

Salvar simulação · Exportar PDF · Enviar memória de cálculo por e-mail · Alerta de desvio/topo histórico ·
Newsletter "Radar Semanal dos ETFs". Persistidos na Fase 02 (GROW-001..006).

## Regras de implementação

- Ferramentas leem **somente dados locais** (Postgres/Redis) — nunca chamar BCB/CVM em runtime (DEC-006 aceita).
- Resultado fiscal sempre com premissas explícitas, data-fonte e disclaimer educacional (não é orientação individual).
- Métricas SEO/CWV não coletam dado sensível sem consentimento (GROW-004).
