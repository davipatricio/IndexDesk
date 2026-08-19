import { writeFileSync } from "node:fs";
import { resolve } from "node:path";
import { loadRoadmap, type RoadmapData } from "./validate-roadmap";

const rootDir = resolve(import.meta.dir, "../..");
const outputPath = resolve(rootDir, "ROADMAP.md");

function escapeTable(value: unknown): string {
  return String(value ?? "").replaceAll("|", "\\|").replaceAll("\n", " ");
}

function statusIcon(status: string): string {
  return {
    complete: "✅",
    in_progress: "🔵",
    blocked: "🚫",
    deferred: "⏸️",
    out_of_scope: "⛔",
    not_started: "⬜",
  }[status] ?? "❔";
}

function percent(done: number, total: number): number {
  return total === 0 ? 0 : Math.round((done / total) * 100);
}

function bar(value: number, width = 20): string {
  const filled = Math.round((value / 100) * width);
  return `[${"█".repeat(filled)}${"░".repeat(width - filled)}]`;
}

function flattenTasks(data: RoadmapData): any[] {
  return data.phases.flatMap((document) => document.phase.epics.flatMap((epic: any) => epic.tasks));
}

function phaseStats(phase: any) {
  const tasks = phase.epics.flatMap((epic: any) => epic.tasks);
  const done = tasks.filter((task: any) => task.status === "complete").length;
  const active = tasks.filter((task: any) => task.status === "in_progress").length;
  const blocked = tasks.filter((task: any) => task.status === "blocked").length;
  return { tasks, done, active, blocked, percentage: percent(done, tasks.length) };
}

function renderPhase(phase: any): string[] {
  const stats = phaseStats(phase);
  const lines = [
    `## Fase ${String(phase.order).padStart(2, "0")} — ${phase.name}`,
    "",
    `**Status:** ${statusIcon(phase.status)} \`${phase.status}\` · **Prioridade:** \`${phase.priority}\` · **Progresso:** ${bar(stats.percentage)} ${stats.percentage}% (${stats.done}/${stats.tasks.length})`,
    `**Objetivo:** ${phase.objective}`,
    `**Depende de:** ${phase.depends_on.length > 0 ? phase.depends_on.map((id: string) => `\`${id}\``).join(", ") : "nenhuma fase"}`,
    "",
  ];

  for (const epic of phase.epics) {
    const done = epic.tasks.filter((task: any) => task.status === "complete").length;
    const epicPercentage = percent(done, epic.tasks.length);
    lines.push(`### ${epic.name}`, "", `_${epic.description}_`, "", `**Status:** ${statusIcon(epic.status)} \`${epic.status}\` · **Prioridade:** \`${epic.priority}\` · **Progresso:** ${bar(epicPercentage, 12)} ${epicPercentage}% (${done}/${epic.tasks.length})`, "");
    lines.push("| ID | Tarefa | Prioridade | Dificuldade | Status | Dependências |", "| :--- | :--- | :---: | :---: | :--- | :--- | ");
    for (const task of epic.tasks) {
      const dependencies = task.depends_on.length > 0 ? task.depends_on.map((id: string) => `\`${id}\``).join(", ") : "—";
      lines.push(`| \`${task.id}\` | ${escapeTable(task.title)} | \`${task.priority}\` | \`${task.difficulty}\` | ${statusIcon(task.status)} \`${task.status}\` | ${dependencies} |`);
    }
    lines.push("", "<details>", "<summary>Critérios e entregáveis</summary>", "");
    for (const task of epic.tasks) {
      lines.push(`- **${task.id} — ${task.title}**`);
      lines.push(`  - Critérios: ${task.acceptance_criteria.join("; ")}`);
      lines.push(`  - Entregáveis: ${task.deliverables.join("; ")}`);
      if (task.notes) lines.push(`  - Notas: ${task.notes}`);
    }
    lines.push("", "</details>", "");
  }

  return lines;
}

function render(data: RoadmapData): string {
  const tasks = flattenTasks(data);
  const done = tasks.filter((task) => task.status === "complete").length;
  const active = tasks.filter((task) => task.status === "in_progress").length;
  const blocked = tasks.filter((task) => task.status === "blocked").length;
  const overall = percent(done, tasks.length);
  const state = data.root.current_state;

  const lines = [
    "# 🗺️ IndexDesk — Roadmap de Execução",
    "",
    "> **Arquivo gerado:** não edite `ROADMAP.md` diretamente. Atualize `.roadmap/**/*.json` e execute `bun run roadmap:generate`.",
    "> **Estado atual:** requisitos documentados, implementação ainda não scaffoldada.",
    "",
    `**Atualizado em:** ${data.root.updated_at} · **Estado:** \`${state.implementation_status}\` · **Progresso:** ${bar(overall)} ${overall}% (${done}/${tasks.length})`,
    `**Tarefas:** ${tasks.length} total · ${done} concluídas · ${active} em andamento · ${blocked} bloqueadas · ${tasks.length - done - active - blocked} não iniciadas/deferidas`,
    "",
    "## Estado do projeto",
    "",
    `- **Ciclo de vida:** \`${state.lifecycle}\``,
    `- **Tracking do roadmap scaffoldado:** \`${state.roadmap_tracking_scaffolded}\``,
    `- **Código do produto scaffoldado:** \`${state.code_scaffolded}\``,
    `- **Commits registrados no snapshot:** \`${state.git_commits}\``,
    `- **Bloqueadores:** ${state.blockers.join(" ")}`,
    `- **Nota:** ${state.notes}`,
    "",
    "## Visão por fase",
    "",
    "| Fase | Status | Prioridade | Progresso | Dependências |",
    "| :--- | :--- | :---: | :---: | :--- |",
  ];

  for (const document of data.phases) {
    const phase = document.phase;
    const stats = phaseStats(phase);
    lines.push(`| **${String(phase.order).padStart(2, "0")} — ${phase.name}** | ${statusIcon(phase.status)} \`${phase.status}\` | \`${phase.priority}\` | ${stats.percentage}% (${stats.done}/${stats.tasks.length}) | ${phase.depends_on.length ? phase.depends_on.map((id: string) => `\`${id}\``).join(", ") : "—"} |`);
  }

  lines.push("", "## Dependências críticas", "", "```text", "PHASE-00 foundation + infrastructure", "  -> PHASE-01 persistence + ingestion + APIs + public MVP", "  -> PHASE-02 saved backtests + programmatic SEO + conversion", "  -> PHASE-03 portfolios + fixed income + events + tax automation", "", "Auth/RBAC/audit -> admin -> saved backtests -> portfolios/alerts", "Holdings ingestion -> overlap -> comparison/SEO overlap pages", "Macro/holidays -> real yield -> backtest/fixed income accrual", "Quotes/corporate actions/transactions -> portfolio performance/rebalance/DARF", "News/reports/storage/RSS -> public hubs + admin publishing", "```", "", "## Fases e tarefas", "");

  for (const document of data.phases) lines.push(...renderPhase(document.phase));

  lines.push("## Decisões em aberto", "", "| ID | Decisão | Status | Prioridade | Recomendação |", "| :--- | :--- | :---: | :---: | :--- | ");
  for (const decision of data.decisions.decisions) lines.push(`| \`${decision.id}\` | ${escapeTable(decision.title)} | \`${decision.status}\` | \`${decision.priority}\` | ${escapeTable(decision.recommendation)} |`);

  lines.push("", "## Riscos", "", "| ID | Risco | Probabilidade | Impacto | Status | Mitigação |", "| :--- | :--- | :---: | :---: | :---: | :--- | ");
  for (const risk of data.risks.risks) lines.push(`| \`${risk.id}\` | ${escapeTable(risk.title)} | \`${risk.probability}\` | \`${risk.impact}\` | \`${risk.status}\` | ${escapeTable(risk.mitigation)} |`);

  lines.push("", "## Documentos-fonte", "", ...data.root.source_documents.map((source: any) => `- \`${source.file}\` — ${source.section}`), "", "## Como atualizar", "", "1. Edite a fase/decisão/risco correspondente em `.roadmap/`.", "2. Execute `bun run roadmap:validate`.", "3. Execute `bun run roadmap:generate`.", "4. Revise o diff gerado e mantenha `CLAUDE.md` sincronizado com a implementação real.", "");

  return `${lines.join("\n")}\n`;
}

if (import.meta.main) {
  const data = loadRoadmap();
  const content = render(data);
  writeFileSync(outputPath, content);
  console.log(`Generated ${outputPath}`);
}
