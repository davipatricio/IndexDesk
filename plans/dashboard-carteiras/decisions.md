# Decisões do Grill (fonte de verdade de escopo)

Sessão de 66 perguntas com o dono do produto em 2026-08-25. Nenhuma decisão aqui pode ser trocada
sem registrar a nova escolha neste arquivo.

## 1. Carteiras & visibilidade

| Decisão | Escolha |
| :--- | :--- |
| Limite por usuário | **3 carteiras grátis** |
| Visibilidade | Privada (default) / Pública / Link restrito |
| Valores na pública/link | **Dono escolhe**: `percent_only` ou `full_values` |
| Link restrito | Expiração configurável + revogável/regenerável |
| Clonagem de pública | Sim, 1 clique (copia composição como BUYs sintéticos datados hoje) |
| Perfil de risco | Conservador/Moderado/Arrojado = **só etiqueta**, sem validação de alocação |
| Moeda base | BRL fixo |
| FX estrangeiro | Auto via `fx_rates` (custo congela câmbio do dia; valuation usa último) |
| Home do dashboard | = consolidado de todas as carteiras, drill-in individual |
| Slug público | `/c/[slug]` indexável (SEO) |
| Social MVP | Nenhum (sem follow/ranking/comentário) |
| Identidade pública | Dono escolhe: display name ou anônimo "Investidor X" |

## 2. Classes de ativo (dia 1)

1. Ações B3
2. ETFs B3
3. BDRs (inclui BDR de ETF)
4. FIIs
5. ETFs internacionais
6. Cripto — top 100 cotação automática ⛔ provider pendente (ver README)
7. Moedas estrangeiras em espécie (USD/EUR)
8. CDI/Selic sintético (caixa rendendo indexador)
9. Tesouro Direto — títulos específicos tipo+vencimento ⛔ catálogo pendente
10. Renda fixa privada genérica por indexador (CDI%, CDI+%, Selic, IPCA+%, Prefixado) cobrindo
    CDB/LC/CRI/CRA/debênture/LCI/LCA
11. Fundos de investimento — cota manual até MVP-003 ⛔
12. Previdência PGBL/VGBL — tipo próprio (regressiva/progressiva, fase acumulação)

Regras:

- Sem ativo custom fora das classes.
- Ações internacionais diretas: fora do escopo (BDR primeiro).
- **Preço manual sempre possível** para qualquer classe; automático quando existir feed.
- Proventos/juros/cupom/amortização: caixa via accrual automático do Worker.

## 3. Transações & wizard

| Decisão | Escolha |
| :--- | :--- |
| Wizard | 3 etapas + revisão (identificação → financeiro → confirmação) |
| Tipos MVP | BUY, SELL, INCOME (provento/juros/cupom/amortização), CORP_ACTION (split/inpc/bonificação/subscrição), TRANSFER_IN/OUT entre carteiras |
| Retroativo | Sem limite de data |
| Import CSV corretoras | Fora do MVP |
| Anexo de nota | Não |
| Quantidade | Decimal livre |
| Método de custo | Preço médio (sem FIFO) |
| Corretora | Lista curada + "Outra" livre |
| Despesas | Campo único somado ao custo |
| Edição | **Editar livre** (ver §7 abaixo) |
| Aporte recorrente | Fora do MVP |

## 4. Rentabilidade & metas

| Decisão | Escolha |
| :--- | :--- |
| Janela | Presets (1M default, 3M/6M/YTD/1A/Max) + data início/fim livre |
| Métricas | Simples + TWR + MWR/TIR (todas) |
| Benchmark | Linhas sobrepostas no chart (CDI/IPCA/IBOV/S&P) |
| Risco | Aba dedicada "Análise" (vol, Sharpe, drawdown) |
| Contribuição | Coluna na tabela de posições |
| Metas | Multi-metas (valor R$, % crescimento, prazo) |
| Projeção meta | Run-rate + simulação juros compostos com aporte mensal × taxa |
| Alocação-alvo | Editável % por classe com desvio visual |
| Resumo semanal | Depriorizado |
| Hero | Patrimônio grande + Δ dia + retorno período |

## 5. Fiscal

| Decisão | Escolha |
| :--- | :--- |
| Simulador resgate | IR regressiva (fronteiras 180/360/720d), come-cotas, IOF <30d, isenções (R$20k ações — não vale ETF; R$35k FII; LCI/LCA isentas) |
| Resgate parcial | Sim (por R$ ou quantidade) |
| IR na posição | Só dentro do simulador |
| DARF | Projeção MVP ("se vender hoje"); apuração completa PORT-009 depois |
| Prejuízos | Ledger compensável por classe |
| Come-cotas pago | Explícito na posição RF |
| Alerta isenção mensal | Dentro do simulador |
| Rebalanceamento | Depois (PORT-008 intocado) |
| Vencimentos | Timeline de vencimentos/carências |
| Disclaimer | Nos simuladores/apuração |

## 6. Layout & UX

| Decisão | Escolha |
| :--- | :--- |
| Layout | Fixo denso estilo Google Finance (sem drag) |
| Chart principal | Área patrimônio com marcações de aporte |
| Device | Mobile-first |
| Export | CSV + XLSX |
| Offline | Read-only cache Serwist |
| Notificações | Depois |
| Tema | Segue sistema |
| Empty state | Demo seedada deletável + onboarding vazio |
| Calendário eventos | Depois (PORT-006) |
| Multi-corretora | Agrupamento por custódia com sub-total |
| Histórico | Snapshots diários (hypertable) + recálculo on-demand |

## 7. Tensão registrada: edição livre × imutabilidade

Aceite original de PORT-001: "transações são imutáveis/auditáveis". Dono escolheu editar livre.

Resolução adotada: **edição lógica** — transação original permanece (`is_amendment = false`),
edição cria linha nova com `amended_transaction_id`; projeções consideram só versão vigente;
histórico sempre auditável. Se rejeitada em revisão, reverter para estorno-only e reconfirmar.
