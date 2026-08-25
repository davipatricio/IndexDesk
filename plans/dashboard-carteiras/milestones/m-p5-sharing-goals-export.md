# Marco M-P5 — Compartilhamento, Metas & Export

Objetivo: carteira pública/link, clonagem, metas com projeção e exportação de dados.

## Tarefas

### Visibilidade

- [ ] `POST/DELETE /share-link` (gerar/regenerar/revogar, expiração configurável, token hasheado)
- [ ] Slug público gerado quando visibilidade ≠ private; `/c/[slug]` SSR+ISR indexável
- [ ] JSON-LD + OG dinâmico da página pública (padrão SEO do repo)
- [ ] `public_values_mode`: percent_only esconde R$ (testes E2E de vazamento)
- [ ] Identidade: display name ou anônimo "Investidor X"
- [ ] noindex automático para carteiras link-restritas
- [ ] Clone 1 clique: BUYs sintéticos datados hoje preservando pesos

### Metas & alocação

- [ ] DDL `portfolio_goals` aplicado; CRUD `/goals`
- [ ] Multi-metas: valor R$, % crescimento, prazo
- [ ] Projeção run-rate ("atinge em mar/2028")
- [ ] Simulação juros compostos (VP+PMT+i) na meta
- [ ] Alocação-alvo editável por classe com barras de desvio

### Export

- [ ] CSV posições/transações (server-side)
- [ ] XLSX multi-aba formatado

## Fora deste marco (decisão dono)

Social (follow/ranking/comentários), resumo semanal, notificações, import CSV corretoras,
aportes recorrentes, calendário de eventos, rebalanceamento.
