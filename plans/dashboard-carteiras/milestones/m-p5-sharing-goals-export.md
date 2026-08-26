# Marco M-P5 — Compartilhamento, Metas & Export

Objetivo: carteira pública/link, clonagem, metas com projeção e exportação de dados.

## Tarefas

### Visibilidade

[x] `POST /share-link/regenerate` · `DELETE /share-link` · `PATCH /visibility` (token claro retornado 1×, hash SHA256 no banco)
[x] Slug gerado (`titulo-abc123`) + endpoint público anônimo `/public/{slug}`
[x] Página pública `/c/[slug]` (SSR, revalidate 300, metadata + JSON-LD, CTA clonar p/ logados)
[x] Metadata + JSON-LD na página pública (OG dinâmico de imagem fica p/ fase SEO)
[x] `percent_only` respeitado no DTO público — smoke E2E (allocation % sem R$, positions sem valores)
[x] Identidade: display name ou anônimo "Investidor X" — smoke ✓
- [ ] noindex automático para carteiras link-restritas
[x] Clone 1 clique via POST `/public/{slug}/clone` — smoke: pesos normalizados p/ base 10000 ✓

### Metas & alocação

- [x] DDL `portfolio_goals` aplicado; endpoints CRUD `/goals` + `/goals/{id}/projection`
- [x] Multi-metas: valor R$, % crescimento, prazo (smoke: criação + progresso ✓)
- [x] Projeção run-rate ("atinge em mar/2028") — `GoalProjectionCalculator` (10 testes)
- [x] Simulação juros compostos (VP+PMT+i) na meta
- [x] Controles de compartilhamento na carteira (visibilidade + gerar/copiar/revogar link)
- [ ] UI de metas/alocação-alvo (endpoints prontos)

### Export

- [x] CSV posições/transações server-side (BOM UTF-8, ; pt-BR) — `/export/positions.csv`, `/export/transactions.csv`
- [ ] ⛔ XLSX multi-aba: aguarda decisão de pacote (ClosedXML licença)

## Fora deste marco (decisão dono)

Social (follow/ranking/comentários), resumo semanal, notificações, import CSV corretoras,
aportes recorrentes, calendário de eventos, rebalanceamento.
