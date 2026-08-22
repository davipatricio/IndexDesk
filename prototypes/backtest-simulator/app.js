const palette = ['var(--chart-1)', 'var(--chart-2)', 'var(--chart-3)', 'var(--chart-4)', 'var(--chart-5)'];
const workspace = document.querySelector('#workspace');
const toast = document.querySelector('#toast');
let activeDesign = 'terminal';
let toastTimer;

const assets = [
  { ticker: 'WRLD11', name: 'ETF Global', weight: 42, color: palette[0] },
  { ticker: 'IVVB11', name: 'S&P 500', weight: 28, color: palette[1] },
  { ticker: 'B5P211', name: 'IPCA Curto', weight: 18, color: palette[2] },
  { ticker: 'GOLD11', name: 'Ouro', weight: 12, color: palette[3] },
];

function el(tag, className, content = '') {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (content) node.innerHTML = content;
  return node;
}

function showToast(message) {
  toast.textContent = message;
  toast.classList.add('visible');
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => toast.classList.remove('visible'), 2600);
}

function iconButton(label, action) {
  return `<button class="btn" type="button" data-action="${action}">${label}</button>`;
}

function workspaceHeader(eyebrow, title, description, actionMarkup = '') {
  return `<div class="workspace-heading"><div><div class="eyebrow">${eyebrow}</div><h2>${title}</h2><p>${description}</p></div><div class="workspace-actions">${actionMarkup}</div></div>`;
}

function kpis() {
  return `<div class="kpi-grid">
    <div class="kpi"><div class="kpi-label">CAGR</div><div class="kpi-value positive">+12,84%</div><div class="kpi-delta">retorno anual composto</div></div>
    <div class="kpi"><div class="kpi-label">Volatilidade</div><div class="kpi-value">14,21%</div><div class="kpi-delta">anualizada</div></div>
    <div class="kpi"><div class="kpi-label">Sharpe</div><div class="kpi-value">0,78</div><div class="kpi-delta">CDI como taxa livre</div></div>
    <div class="kpi"><div class="kpi-label">Max drawdown</div><div class="kpi-value negative">-18,36%</div><div class="kpi-delta">mar/20 — set/20</div></div>
    <div class="kpi"><div class="kpi-label">Saldo final</div><div class="kpi-value">R$ 184,2k</div><div class="kpi-delta positive">+ R$ 84,2k ganho</div></div>
  </div>`;
}

function chartCanvas(id, kind = 'area', series = 1) {
  return `<div class="chart-area"><canvas id="${id}" data-chart="${kind}" data-series="${series}" aria-label="Gráfico ilustrativo de ${kind}"></canvas></div>`;
}

function chartCard(title, subtitle, id, kind = 'area', series = 1) {
  return `<div class="card card-pad chart-card"><div class="card-title"><div><h3>${title}</h3><span>${subtitle}</span></div><button class="btn ghost small" type="button" data-action="chart-info">ⓘ</button></div>${chartCanvas(id, kind, series)}${legend(series)}</div>`;
}

function legend(series = 1) {
  const labels = series === 2 ? [['Carteira', palette[0]], ['CDI', palette[1]]] : series === 3 ? [['Carteira A', palette[0]], ['Carteira B', palette[1]], ['CDI', palette[2]]] : [['Patrimônio', palette[0]], ['Total aportado', palette[2]]];
  return `<div class="chart-legend">${labels.map(([label, color], i) => `<span class="legend-item"><i class="legend-swatch line" style="background:${color}"></i>${label}</span>`).join('')}</div>`;
}

function assetRows(editable = false, variant = 'default') {
  return assets.map((asset, index) => `<div class="asset-option ${index < (variant === 'b' ? 2 : 4) ? 'selected' : ''}">
    <span class="asset-dot" style="background:${asset.color}"></span><div><strong>${asset.ticker}</strong><small>${asset.name}</small></div>
    ${editable ? `<input class="weight-input" type="number" min="0" max="100" value="${asset.weight}" aria-label="Peso de ${asset.ticker}" data-weight="${index}">` : `<output>${asset.weight}%</output>`}
  </div>`).join('');
}

function donut(size = '') {
  return `<div class="donut ${size}"><div class="donut-center">100%<small>alocado</small></div></div>`;
}

function terminalView() {
  return `<div class="workspace terminal-workspace">${workspaceHeader('01 / terminal workspace', 'Backtest público', 'Ajuste os parâmetros à esquerda e acompanhe a curva patrimonial em tempo real.', iconButton('↗ Compartilhar', 'share'))}<div class="split-layout"><aside class="sidebar">
    <h3>Configuração</h3>
    <div class="sidebar-section"><h4>Carteira</h4><div class="stack">${assetRows(true)}</div><button class="btn ghost small" type="button" data-action="add-asset">＋ Adicionar ativo</button></div>
    <div class="sidebar-section"><h4>Capital e horizonte</h4><div class="field-grid"><label><span class="label">Inicial</span><input value="R$ 50.000" /></label><label><span class="label">Aporte / mês</span><input value="R$ 1.000" /></label></div><div class="field-grid" style="margin-top:11px"><label><span class="label">De</span><input type="date" value="2019-01-02" /></label><label><span class="label">Até</span><input type="date" value="2024-12-30" /></label></div></div>
    <div class="sidebar-section"><h4>Estratégia</h4><label><span class="label">Rebalanceamento</span><select><option>Nenhum · Buy & Hold</option><option>Mensal</option><option>Trimestral</option><option>Anual</option></select></label><label style="display:block;margin-top:12px"><span class="label">Benchmark</span><select><option>CDI</option><option>IBOV</option><option>S&P 500</option><option>IPCA (real)</option></select></label></div>
    <button class="btn primary" style="width:100%;margin-top:16px" type="button" data-action="run">▶ Simular carteira</button>
  </aside><div class="main-panel">${kpis()}<div class="main-panel-content"><div class="chart-grid">${chartCard('Evolução patrimonial', 'jan/19 — dez/24 · escala linear', 'terminal-equity', 'area', 2)}<div class="card card-pad"><div class="card-title"><div><h3>Alocação atual</h3><span>peso alvo da carteira</span></div></div><div class="donut-wrap">${donut()}<div class="legend-stack">${assets.map((a) => `<span class="legend-item"><span><i class="legend-swatch" style="background:${a.color}"></i>${a.ticker}</span><span>${a.weight}%</span></span>`).join('')}</div></div></div></div><div class="chart-grid">${chartCard('Underwater drawdown', 'profundidade da queda · %', 'terminal-drawdown', 'drawdown', 1)}<div class="card card-pad"><div class="card-title"><div><h3>Retornos anuais</h3><span>retorno nominal</span></div></div>${chartCanvas('terminal-bars', 'bars', 1)}</div></div></div></div></div></div></div>`;
}

function wizardView() {
  return `<div class="workspace wizard">${workspaceHeader('02 / wizard', 'Monte sua simulação', 'Uma jornada em três passos para transformar uma hipótese em um relatório de investimento.', iconButton('Salvar rascunho', 'save'))}<div class="stepper"><div class="step active"><span class="step-number">1</span><span class="step-copy"><strong>Ativos</strong><small>Selecione e distribua</small></span></div><div class="step"><span class="step-number">2</span><span class="step-copy"><strong>Parâmetros</strong><small>Defina o horizonte</small></span></div><div class="step"><span class="step-number">3</span><span class="step-copy"><strong>Relatório</strong><small>Analise o resultado</small></span></div></div><div class="wizard-body"><div class="wizard-intro"><div class="eyebrow">Passo 1 de 3</div><h2>Qual é a sua carteira?</h2><p>Escolha os ativos que farão parte da simulação e defina o peso de cada um. A soma precisa ser exatamente 100%.</p></div><div class="wizard-columns"><div class="card card-pad"><div class="card-title"><h3>Ativos disponíveis</h3><span>4 selecionados</span></div><div class="asset-picker">${assetRows(true)}</div><button class="btn ghost small" type="button" data-action="add-asset">＋ Buscar outro ativo</button></div><div class="wizard-side"><div class="card card-pad"><div class="card-title"><h3>Distribuição</h3><span class="positive">100% OK</span></div><div class="allocation-ring">${donut()}<div class="legend-stack">${assets.slice(0, 3).map((a) => `<span class="legend-item"><span><i class="legend-swatch" style="background:${a.color}"></i>${a.ticker}</span><span>${a.weight}%</span></span>`).join('')}</div></div></div><div class="card card-pad"><span class="label">Dica do IndexDesk</span><p class="muted" style="font-size:11px;line-height:1.55;margin:0">Use ativos de classes diferentes para observar como a diversificação altera a volatilidade e o drawdown.</p></div></div></div><div class="wizard-actions"><button class="btn ghost" type="button" data-action="cancel">Cancelar</button><button class="btn primary" type="button" data-action="next-step">Continuar para parâmetros →</button></div></div></div>`;
}

function tabsView() {
  return `<div class="workspace tabs-workspace">${workspaceHeader('03 / dashboard modular', 'Visão analítica', 'A mesma simulação, organizada em camadas para investigação profunda.', iconButton('⌘ Exportar', 'export'))}<div class="param-strip"><span class="param-chip">Carteira <strong>4 ativos</strong></span><span class="param-chip">Período <strong>2019—2024</strong></span><span class="param-chip">Aporte <strong>R$ 1.000/mês</strong></span><span class="param-chip">Rebalanceamento <strong>Trimestral</strong></span><button class="btn small" style="margin-left:auto" type="button" data-action="edit-params">Editar parâmetros</button></div><div class="inner-tabs" id="inner-tabs"><button class="inner-tab active" data-inner="performance" type="button">Performance & patrimônio</button><button class="inner-tab" data-inner="risk" type="button">Risco & drawdown</button><button class="inner-tab" data-inner="real" type="button">Retorno real / IPCA</button><button class="inner-tab" data-inner="calendar" type="button">Rebalanceamentos</button></div><div class="tab-content" id="tab-content"><div class="metric-pair"><div class="metric-mini"><small>CAGR</small><strong class="positive">+12,84%</strong></div><div class="metric-mini"><small>Patrimônio</small><strong>R$ 184,2k</strong></div><div class="metric-mini"><small>Total aportado</small><strong>R$ 100k</strong></div><div class="metric-mini"><small>Ganho</small><strong class="positive">R$ 84,2k</strong></div></div>${chartCard('Patrimônio versus aportes', 'base 100 · escala indexada', 'tabs-equity', 'area', 2)}<div class="card card-pad" style="margin-top:14px"><div class="card-title"><div><h3>Resumo do período</h3><span>valores ilustrativos</span></div></div><table class="table"><thead><tr><th>Indicador</th><th>Carteira</th><th>CDI</th><th>Diferença</th></tr></thead><tbody><tr><td>Retorno acumulado</td><td class="mono positive">+268,4%</td><td class="mono">+198,2%</td><td class="mono positive">+70,2 p.p.</td></tr><tr><td>Volatilidade anualizada</td><td class="mono">14,21%</td><td class="mono">0,42%</td><td class="mono negative">+13,79 p.p.</td></tr><tr><td>Máximo drawdown</td><td class="mono negative">-18,36%</td><td class="mono positive">0,00%</td><td class="mono negative">-18,36 p.p.</td></tr></tbody></table></div></div></div>`;
}

function compareView() {
  return `<div class="workspace compare-workspace">${workspaceHeader('04 / canvas comparativo', 'Duas teses. Um mesmo mercado.', 'Compare carteiras com capital, datas e aportes idênticos para tomar uma decisão consciente.', iconButton('＋ Nova comparação', 'new-compare'))}<div class="compare-grid"><div class="strategy-card"><div class="strategy-head"><div><h3>Estratégia A · Global balanceada</h3><p>Menor concentração em renda variável</p></div><button class="btn ghost small" type="button" data-action="edit-strategy">Editar</button></div><div class="strategy-body"><div class="asset-picker">${assetRows(false, 'a')}</div><div class="score"><div><small>Saldo final</small><strong>R$ 184.220</strong></div><span class="badge positive">+12,84% CAGR</span></div></div></div><div class="strategy-card"><div class="strategy-head"><div><h3>Estratégia B · Crescimento</h3><p>Maior exposição a ações globais</p></div><button class="btn ghost small" type="button" data-action="edit-strategy">Editar</button></div><div class="strategy-body"><div class="asset-picker">${assetRows(false, 'b')}</div><div class="score"><div><small>Saldo final</small><strong>R$ 201.870</strong></div><span class="badge positive">+14,91% CAGR</span></div></div></div></div><div class="compare-chart">${chartCard('Curvas sobrepostas', 'mesmo capital e período · base 100', 'compare-equity', 'compare', 3)}<div class="verdict"><div><strong class="positive">Estratégia B</strong><small>melhor retorno acumulado</small></div><div class="vs">VS</div><div><strong>Estratégia A</strong><small>menor drawdown: -18,36%</small></div></div></div></div>`;
}

function studioView() {
  const heat = ['+4,2%', '+1,8%', '-2,1%', '+6,5%', '+3,2%', '-5,2%', '+7,1%', '+2,8%', '+5,4%', '-1,4%', '+8,7%', '+1,2%', '-3,6%', '+4,9%', '+2,2%', '+6,1%', '-2,8%', '+5,8%', '+1,1%', '+3,9%', '+7,4%'];
  return `<div class="workspace studio-workspace">${workspaceHeader('05 / compact financial studio', 'Explorer profissional', 'Todos os sinais em uma tela: alocação, curva, retorno anual e correlação.', `<div class="segmented"><button class="active" type="button" data-range="max">Máx</button><button type="button" data-range="5y">5A</button><button type="button" data-range="3y">3A</button></div>`)}<div class="studio-layout"><div class="studio-main"><div class="studio-toolbar"><div><div class="eyebrow">Carteira principal</div><strong style="font-size:13px">Global balanceada <span class="positive mono">+12,84%</span></strong></div><button class="btn primary small" type="button" data-action="run">▶ Atualizar</button></div>${chartCard('Curva de patrimônio', 'retorno acumulado · escala logarítmica', 'studio-equity', 'area', 2)}<div class="card card-pad" style="margin-top:14px"><div class="card-title"><div><h3>Retornos anuais</h3><span>2019 — 2024</span></div><span class="positive">média +12,8%</span></div><div class="heatmap"><span class="heat-label">Ano</span>${['Q1','Q2','Q3','Q4','Q5','Q6'].map((x) => `<span class="heat-label">${x}</span>`).join('')}${heat.map((value, i) => `<span class="heat-cell" style="background:${i % 5 === 2 ? 'color-mix(in oklch, var(--negative) 22%, var(--surface))' : i % 3 === 0 ? 'color-mix(in oklch, var(--positive) 28%, var(--surface))' : 'color-mix(in oklch, var(--brand) 16%, var(--surface))'};color:${i % 5 === 2 ? 'var(--negative)' : 'var(--ink)'}">${value}</span>`).join('')}</div></div></div><aside class="studio-side"><div class="card card-pad"><div class="card-title"><h3>Alocação</h3><span>target</span></div>${assets.map((a) => `<div style="margin-bottom:13px"><div class="control-row"><span class="asset-token"><i class="asset-dot" style="background:${a.color}"></i>${a.ticker}</span><span class="mono">${a.weight}%</span></div><div class="progress" style="margin-top:6px"><span style="width:${a.weight}%;background:${a.color}"></span></div></div>`).join('')}<button class="btn small" style="width:100%;margin-top:2px" type="button" data-action="edit-weights">Editar pesos</button></div><div class="card card-pad"><div class="card-title"><h3>Correlação</h3><span>12m rolling</span></div><div class="correlation"><span class="heat-label">·</span><span class="heat-label">W</span><span class="heat-label">I</span><span class="heat-label">B</span><span class="heat-label">W</span><span style="background:var(--brand-soft)">1,00</span><span style="background:color-mix(in oklch, var(--chart-3) 20%, var(--surface))">0,72</span><span style="background:color-mix(in oklch, var(--chart-3) 12%, var(--surface))">0,31</span><span class="heat-label">I</span><span style="background:color-mix(in oklch, var(--chart-3) 20%, var(--surface))">0,72</span><span style="background:var(--brand-soft)">1,00</span><span style="background:color-mix(in oklch, var(--chart-3) 22%, var(--surface))">0,44</span><span class="heat-label">B</span><span style="background:color-mix(in oklch, var(--chart-3) 12%, var(--surface))">0,31</span><span style="background:color-mix(in oklch, var(--chart-3) 22%, var(--surface))">0,44</span><span style="background:var(--brand-soft)">1,00</span></div></div><div class="card card-pad"><div class="card-title"><h3>Risco</h3><span>métricas-chave</span></div><div class="metric-mini"><small>Sharpe</small><strong>0,78</strong></div><div class="metric-mini" style="margin-top:8px"><small>Calmar</small><strong>0,70</strong></div></div></aside></div></div>`;
}

function drawCharts() {
  document.querySelectorAll('canvas[data-chart]').forEach((canvas) => {
    const rect = canvas.getBoundingClientRect();
    const dpr = window.devicePixelRatio || 1;
    const width = Math.max(240, rect.width);
    const height = Math.max(120, rect.height);
    canvas.width = width * dpr;
    canvas.height = height * dpr;
    const ctx = canvas.getContext('2d');
    ctx.scale(dpr, dpr);
    const styles = getComputedStyle(document.body);
    const line = styles.getPropertyValue('--line').trim();
    const faint = styles.getPropertyValue('--ink-faint').trim();
    const kind = canvas.dataset.chart;
    const seriesCount = Number(canvas.dataset.series || 1);
    const x0 = 10, x1 = width - 8, y0 = 12, y1 = height - 20;
    ctx.strokeStyle = line; ctx.lineWidth = 1;
    for (let i = 0; i < 4; i += 1) { const y = y0 + ((y1 - y0) * i) / 3; ctx.beginPath(); ctx.moveTo(x0, y); ctx.lineTo(x1, y); ctx.stroke(); }
    ctx.fillStyle = faint; ctx.font = '9px monospace';
    ['2019', '2021', '2023', '2024'].forEach((label, i) => ctx.fillText(label, x0 + ((x1 - x0) * i) / 3 - 11, height - 3));
    const color = (name) => styles.getPropertyValue(name).trim();
    const makePoints = (offset = 0, drop = false) => Array.from({ length: 40 }, (_, i) => {
      const t = i / 39;
      const cycle = Math.sin(i * 0.47 + offset) * 0.035 + Math.sin(i * 0.17 + offset) * 0.026;
      const shock = drop && i > 19 && i < 24 ? -0.17 * Math.sin(((i - 19) / 5) * Math.PI) : 0;
      return { x: x0 + t * (x1 - x0), y: y1 - 22 - (t * (y1 - y0 - 24) * (0.67 + offset * 0.02) + cycle * (y1 - y0) + shock * (y1 - y0)) };
    });
    const drawLine = (points, stroke, width = 2, fill = null) => {
      ctx.beginPath(); points.forEach((p, i) => i ? ctx.lineTo(p.x, p.y) : ctx.moveTo(p.x, p.y));
      if (fill) { ctx.lineTo(points.at(-1).x, y1); ctx.lineTo(points[0].x, y1); ctx.closePath(); ctx.fillStyle = fill; ctx.fill(); }
      ctx.beginPath(); points.forEach((p, i) => i ? ctx.lineTo(p.x, p.y) : ctx.moveTo(p.x, p.y)); ctx.strokeStyle = stroke; ctx.lineWidth = width; ctx.stroke();
    };
    if (kind === 'bars') { const values = [0.45, 0.62, 0.32, 0.73, 0.52, 0.81]; values.forEach((value, i) => { const barW = Math.min(32, (x1 - x0) / 9); const x = x0 + 15 + i * ((x1 - x0 - 25) / values.length); const barH = value * (y1 - y0); ctx.fillStyle = i === 2 ? color('--negative') : color('--chart-1'); ctx.beginPath(); ctx.roundRect(x, y1 - barH, barW, barH, 3); ctx.fill(); }); return; }
    if (kind === 'drawdown') { const points = makePoints(0, true).map((p, i) => ({ x: p.x, y: y0 + (i > 18 && i < 28 ? Math.sin(((i - 18) / 10) * Math.PI) * 68 : Math.abs(Math.sin(i * 0.4)) * 8) })); drawLine(points, color('--negative'), 2, 'color-mix(in oklch, var(--negative) 12%, transparent)'); return; }
    if (kind === 'compare') { drawLine(makePoints(1, false), color('--chart-1'), 2); drawLine(makePoints(2, false).map((p) => ({ ...p, y: p.y + 12 - Math.sin(p.x * 0.04) * 9 })), color('--chart-2'), 2); drawLine(makePoints(3, false).map((p) => ({ ...p, y: p.y + 42 })), color('--chart-3'), 1.5); return; }
    drawLine(makePoints(1), color('--chart-1'), 2.5, 'color-mix(in oklch, var(--chart-1) 11%, transparent)');
    if (seriesCount > 1) drawLine(makePoints(2).map((p, i) => ({ ...p, y: p.y + 22 + Math.sin(i * 0.3) * 4 })), color('--chart-3'), 1.6);
  });
}

function render(design = activeDesign) {
  activeDesign = design;
  document.querySelectorAll('.design-tab').forEach((tab) => tab.classList.toggle('active', tab.dataset.design === design));
  workspace.innerHTML = { terminal: terminalView, wizard: wizardView, tabs: tabsView, compare: compareView, studio: studioView }[design]();
  requestAnimationFrame(drawCharts);
}

document.querySelector('#design-switcher').addEventListener('click', (event) => {
  const tab = event.target.closest('[data-design]');
  if (tab) render(tab.dataset.design);
});

document.querySelector('#theme-toggle').addEventListener('click', (event) => {
  document.body.classList.toggle('dark');
  event.currentTarget.textContent = document.body.classList.contains('dark') ? '☾' : '☼';
  requestAnimationFrame(drawCharts);
});

workspace.addEventListener('click', (event) => {
  const actionElement = event.target.closest('[data-action]');
  if (!actionElement) return;
  const action = actionElement.dataset.action;
  const messages = { run: 'Simulação recalculada — valores ilustrativos atualizados.', share: 'Link de compartilhamento copiado para a área de transferência.', save: 'Rascunho salvo localmente neste protótipo.', export: 'Relatório pronto para exportação em PDF.', 'new-compare': 'Nova coluna de estratégia adicionada no conceito.', 'edit-strategy': 'Editor de alocação aberto no conceito.', 'edit-params': 'Painel de parâmetros aberto no conceito.', 'edit-weights': 'Pesos liberados para edição.', 'add-asset': 'Busca de ativos aberta no conceito.', 'next-step': 'Próxima etapa: parâmetros financeiros.', cancel: 'Simulação descartada.' };
  showToast(messages[action] || 'Interação registrada no protótipo.');
  if (action === 'next-step') { document.querySelectorAll('.step')[0]?.classList.remove('active'); document.querySelectorAll('.step')[1]?.classList.add('active'); }
});

workspace.addEventListener('click', (event) => {
  const inner = event.target.closest('[data-inner]');
  if (!inner) return;
  document.querySelectorAll('.inner-tab').forEach((tab) => tab.classList.toggle('active', tab === inner));
  showToast(`Aba “${inner.textContent}” selecionada.`);
});

workspace.addEventListener('click', (event) => {
  const range = event.target.closest('[data-range]');
  if (!range) return;
  range.parentElement.querySelectorAll('button').forEach((button) => button.classList.toggle('active', button === range));
  showToast(`Horizonte ${range.textContent} selecionado.`);
});

window.addEventListener('resize', () => requestAnimationFrame(drawCharts));
render();
