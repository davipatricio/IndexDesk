# Cálculos Financeiros — Fórmulas de Implementação

Implementar como **funções puras** em `Modules.Analytics/Calculators/` (C#) com testes unitários cobrindo
casos extremos (períodos vazios, 1 dia, fluxo no mesmo dia da avaliação). Usar `adj_close` para retornos;
decimais com precisão suficiente (evitar double acumulado em juros); dias úteis via `market_holidays`.

## 1. Acúmulo de CDI/Selic (base 252)

```
fator_diário(t) = (1 + taxa_anual_t)^(1/252)        // taxa_anual em decimal (ex.: 0,10)
VF = VP × ∏ fator_diário(d), d ∈ dias úteis do período
CDI_acumulado = VF/VP − 1
```

Para "X% do CDI": multiplicar a taxa diária antes do expoente (`(1 + cdi×X)^(1/252)`).
Para "CDI + p.p." ou IPCA+p.p.: somar o spread na taxa anual antes de anualizar por convenção documentada.

## 2. Retorno total e normalizado

- Retorno simples entre datas: `P_t/P_0 − 1`. Comparador normaliza todas as séries à **base 100** (ou %) na data inicial.
- Série de retornos diários: `r_t = adj_close_t / adj_close_{t−1} − 1`.

## 3. CAGR (retorno anualizado)

```
CAGR = (VF / VI)^(1/n_anos) − 1
```
Com aportes mensais usar o método de cotas (§14) antes de anualizar.

## 4. Volatilidade anualizada

```
σ_dia = desvio-padrão amostral dos r_t      σ_aa = σ_dia × √252
```
Convenção brasileira: base **252 dias úteis**.

## 5. Sharpe

```
Sharpe = (R_p − R_f) / σ_p
```
- `R_f` = **CDI acumulado** no mesmo período (não uma constante); `σ_p` = vol anualizada da carteira.
- Anualizar retorno e vol consistentemente; período <1 ano: documentar premissa (anualização linear do excesso não é permitida sem nota).

## 6. Maximum Drawdown

```
DD_t = P_t / max(P_0..P_t) − 1        MaxDD = min DD_t   (valor ≤ 0)
```
Exibir também a janela (data do pico e do vale).

## 7. Tracking Error / Tracking Difference (vs benchmark)

```
TE  = σ(r_ativo − r_benchmark) × √252       // desvio dos excessos
TD  = retorno_acum_ativo − retorno_acum_benchmark
```
Campos: `tracking_error_12m`, `tracking_difference_12m`.

## 8. Rendimento real (equação de Fisher exata)

```
r_real = (1 + r_nominal) / (1 + inflação_acumulada) − 1
```
Inflação = IPCA acumulado no mesmo intervalo (série SGS 433). Nunca usar subtração simples.

## 9. Equivalência de renda fixa

- "% do CDI → equivalente IPCA+": rodar §1 com CDI projetado/histórico e resolver a taxa IPCA+ que reproduz o VF:
  `x = ((VF/VP)^(1/n) − 1)` convertido com Fisher usando o IPCA do período.
- Tributado vs isento: aplicar tabela regressiva/isenção ao VF antes de comparar (ver imposto-renda-fixa.md).
- Pré-fixado: `(1+pré)^n` direto. Sempre expor as premissas (projeção vs histórico).

## 10. Overlap (matriz de sobreposição)

Para dois ETFs A e B com pesos `w_A(i)`, `w_B(i)` por ativo subjacente `i`:

```
overlap(A,B) = Σ_i min(w_A(i), w_B(i))    sobre os tickers compartilhados
exposição_repetida(i) = min(w_A(i), w_B(i))
```
Usar holdings da mesma `as_of_date` mais recente; listar Top ações repetidas ponderadas. Para N ativos,
calcular pares e exposição agregada ponderada.

## 11. Tax drag (B3 × EUA × Irlanda)

Simular VF final nas 3 jurisdições aplicando: dividend yield × retenção na fonte (0% B3 acumulador /
30% EUA / 15% Irlanda) reinvestido, taxa adm, e IR de saída (15% ganho de capital B3/BDR; regime do exterior
p/ investidor direto). Premissas explícitas e editáveis; horizonte 10/20/30 anos.

## 12. Preço médio (PM) — padrão Receita Federal

```
compra:  PM_novo = (Qtd_ant × PM_ant + custo_total_compra) / (Qtd_ant + qtd)
venda:   Qtd -= qtd_vendida (PM inalterado); resultado = valor_venda − custos − (qtd × PM)
split:   Qtd ×= fator ; PM /= fator
```
Custos (corretagem/emolumentos) entram no custo de aquisição. Venda nunca altera PM.

## 13. TWR (Time-Weighted Return)

Quebrar a série em sub-períodos delimitados por **fluxos externos** (aportes/saques):

```
TWR = ∏ (V_fim_i / V_ini_ajustado_i) − 1     // V_ini ajustado exclui o fluxo do dia
```
Neutraliza o timing de aportes — mede a qualidade da estratégia.

## 14. MWR / TIR (Money-Weighted, XIRR)

Taxa `r` que zera o VPL dos fluxos com datas:

```
Σ CF_j / (1+r)^((d_j − d_0)/365) = 0
```
Resolver numericamente (Newton/bisseção com fallback). Casos de teste: sem fluxo intermediário;
aporte no mesmo dia da avaliação; fluxos negativos após perdas.

## 15. Backtest com aportes mensais (método de cotas)

```
cotas_novas = aporte_mês / preço_mês
patrimônio_t = Σ cotas_i × preço_i,t
```
Rebalanceamento mensal/semestral/anual: redistribuir o patrimônio aos pesos-alvo na data da regra
(gera vendas/compras internas simuladas — sem IR no motor público, documentar essa premissa).
Saídas obrigatórias: curva de patrimônio, drawdown, CAGR, vol, Sharpe, melhor/pior ano, retorno real (Fisher).

## 16. Rebalanceamento por aporte (sem vender)

Com alvo `w_i`, patrimônio atual `V_i` e aporte `A`:

```
deficit_i = max(0, (w_i × (V + A)) − V_i)      compra_i = deficit_i / preço_i (arredondar p/ baixo em lotes)
residual = A − Σ custo_compras                  distribuir residual pelo maior déficit restante
```
Determinístico; nunca recomenda venda por padrão (PORT-008).

## 17. Correlação e beta

```
ρ(A,B) = cov(r_A, r_B)/(σ_A σ_B)        β_A,B = cov(r_A, r_B)/var(r_B)
```
Heatmap usa `@visx`; matriz calculada server-side sobre retornos alinhados por data.
