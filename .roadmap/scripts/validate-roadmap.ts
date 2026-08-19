import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { join, resolve } from 'node:path';

const roadmapDir = resolve(import.meta.dir, '..');
const rootDir = resolve(roadmapDir, '..');

const statuses = new Set([
  'not_started',
  'in_progress',
  'blocked',
  'complete',
  'deferred',
  'out_of_scope',
]);
const priorities = new Set(['P0', 'P1', 'P2', 'P3']);
const difficulties = new Set(['trivial', 'easy', 'medium', 'hard', 'complex']);
const kinds = new Set([
  'feature',
  'infrastructure',
  'architecture',
  'data',
  'database',
  'backend',
  'frontend',
  'analytics',
  'admin',
  'security',
  'seo',
  'content',
  'operations',
  'research',
  'decision',
]);

export type RoadmapData = {
  root: any;
  phases: any[];
  decisions: any;
  risks: any;
  phaseFiles: string[];
};

function readJson(path: string): any {
  try {
    return JSON.parse(readFileSync(path, 'utf8'));
  } catch (error) {
    throw new Error(
      `${path}: invalid JSON (${error instanceof Error ? error.message : String(error)})`,
    );
  }
}

export function loadRoadmap(): RoadmapData {
  const phasesDir = join(roadmapDir, 'phases');
  const phaseFiles = readdirSync(phasesDir)
    .filter((file) => file.endsWith('.json'))
    .sort();

  return {
    root: readJson(join(roadmapDir, 'roadmap.json')),
    phases: phaseFiles.map((file) => readJson(join(phasesDir, file))),
    decisions: readJson(join(roadmapDir, 'decisions.json')),
    risks: readJson(join(roadmapDir, 'risks.json')),
    phaseFiles,
  };
}

function isNonEmptyString(value: unknown): value is string {
  return typeof value === 'string' && value.trim().length > 0;
}

function addError(errors: string[], message: string): void {
  errors.push(message);
}

function checkSourceRefs(errors: string[], refs: any[], location: string): void {
  if (!Array.isArray(refs) || refs.length === 0) {
    addError(errors, `${location}: source_refs must contain at least one reference`);
    return;
  }

  refs.forEach((ref, index) => {
    if (!isNonEmptyString(ref?.file) || !isNonEmptyString(ref?.section)) {
      addError(errors, `${location}.source_refs[${index}]: file and section are required`);
    } else if (!existsSync(join(rootDir, ref.file))) {
      addError(
        errors,
        `${location}.source_refs[${index}]: source file does not exist: ${ref.file}`,
      );
    }
  });
}

function checkTask(errors: string[], task: any, phaseId: string, epicId: string): void {
  const location = `task ${task?.id ?? '<missing>'}`;
  const required = [
    'id',
    'title',
    'description',
    'status',
    'priority',
    'difficulty',
    'kind',
    'phase_id',
    'epic_id',
    'depends_on',
    'blocks',
    'source_refs',
    'acceptance_criteria',
    'deliverables',
    'risks',
    'notes',
  ];
  for (const field of required) {
    if (!(field in (task ?? {}))) addError(errors, `${location}: missing ${field}`);
  }

  if (!isNonEmptyString(task?.id)) addError(errors, `${location}: id is required`);
  if (!isNonEmptyString(task?.title)) addError(errors, `${location}: title is required`);
  if (!isNonEmptyString(task?.description))
    addError(errors, `${location}: description is required`);
  if (!statuses.has(task?.status)) addError(errors, `${location}: invalid status ${task?.status}`);
  if (!priorities.has(task?.priority))
    addError(errors, `${location}: invalid priority ${task?.priority}`);
  if (!difficulties.has(task?.difficulty))
    addError(errors, `${location}: invalid difficulty ${task?.difficulty}`);
  if (!kinds.has(task?.kind)) addError(errors, `${location}: invalid kind ${task?.kind}`);
  if (task?.phase_id !== phaseId) addError(errors, `${location}: phase_id must be ${phaseId}`);
  if (task?.epic_id !== epicId) addError(errors, `${location}: epic_id must be ${epicId}`);
  if (!Array.isArray(task?.depends_on))
    addError(errors, `${location}: depends_on must be an array`);
  if (!Array.isArray(task?.blocks)) addError(errors, `${location}: blocks must be an array`);
  if (!Array.isArray(task?.acceptance_criteria) || task.acceptance_criteria.length === 0)
    addError(errors, `${location}: acceptance_criteria must not be empty`);
  if (!Array.isArray(task?.deliverables) || task.deliverables.length === 0)
    addError(errors, `${location}: deliverables must not be empty`);
  checkSourceRefs(errors, task?.source_refs, location);

  if (task?.status === 'complete' && !isNonEmptyString(task?.completed_at)) {
    addError(errors, `${location}: complete tasks require completed_at`);
  }
  if (task?.status !== 'complete' && task?.completed_at !== null) {
    addError(errors, `${location}: completed_at must be null until status is complete`);
  }
}

export function validateRoadmap(data: RoadmapData): string[] {
  const errors: string[] = [];
  const { root, phases, decisions, risks } = data;

  if (root?.document_type !== 'roadmap')
    addError(errors, 'roadmap.json: document_type must be roadmap');
  if (root?.schema_version !== '1.0.0')
    addError(errors, 'roadmap.json: unsupported schema_version');
  const implementationStatus = root?.current_state?.implementation_status;
  const codeScaffolded = root?.current_state?.code_scaffolded;
  if (!['documentation_only', 'scaffolded', 'mvp', 'production'].includes(implementationStatus)) {
    addError(errors, `roadmap.json: unsupported implementation_status ${implementationStatus}`);
  }
  if (root?.current_state?.roadmap_tracking_scaffolded !== true)
    addError(errors, 'roadmap.json: roadmap_tracking_scaffolded must be true');
  if (implementationStatus === 'documentation_only' && codeScaffolded !== false)
    addError(errors, 'roadmap.json: documentation_only projects must set code_scaffolded=false');
  if (implementationStatus !== 'documentation_only' && codeScaffolded !== true)
    addError(errors, 'roadmap.json: scaffolded or implemented projects must set code_scaffolded=true');
  if (!Array.isArray(root?.phase_order) || root.phase_order.length !== phases.length)
    addError(errors, 'roadmap.json: phase_order must list every phase file');

  const ids = new Map<string, string>();
  const dependencyEdges = new Map<string, string[]>();
  const register = (id: unknown, location: string) => {
    if (!isNonEmptyString(id)) {
      addError(errors, `${location}: missing id`);
      return;
    }
    if (ids.has(id)) addError(errors, `duplicate id ${id}: ${ids.get(id)} and ${location}`);
    else ids.set(id, location);
  };

  for (const phaseDocument of phases) {
    const phase = phaseDocument?.phase;
    const phaseLocation = `phase ${phase?.id ?? '<missing>'}`;
    if (phaseDocument?.document_type !== 'phase')
      addError(errors, `${phaseLocation}: document_type must be phase`);
    register(phase?.id, phaseLocation);
    if (!statuses.has(phase?.status))
      addError(errors, `${phaseLocation}: invalid status ${phase?.status}`);
    if (!priorities.has(phase?.priority))
      addError(errors, `${phaseLocation}: invalid priority ${phase?.priority}`);
    if (!Array.isArray(phase?.depends_on))
      addError(errors, `${phaseLocation}: depends_on must be an array`);
    checkSourceRefs(errors, phase?.source_refs, phaseLocation);
    dependencyEdges.set(phase?.id, phase?.depends_on ?? []);

    if (!Array.isArray(phase?.epics) || phase.epics.length === 0) {
      addError(errors, `${phaseLocation}: epics must not be empty`);
      continue;
    }

    for (const epic of phase.epics) {
      const epicLocation = `epic ${epic?.id ?? '<missing>'}`;
      register(epic?.id, epicLocation);
      if (!statuses.has(epic?.status))
        addError(errors, `${epicLocation}: invalid status ${epic?.status}`);
      if (!priorities.has(epic?.priority))
        addError(errors, `${epicLocation}: invalid priority ${epic?.priority}`);
      if (!Array.isArray(epic?.depends_on))
        addError(errors, `${epicLocation}: depends_on must be an array`);
      checkSourceRefs(errors, epic?.source_refs, epicLocation);
      dependencyEdges.set(epic?.id, epic?.depends_on ?? []);
      if (!Array.isArray(epic?.tasks) || epic.tasks.length === 0) {
        addError(errors, `${epicLocation}: tasks must not be empty`);
        continue;
      }
      for (const task of epic.tasks) {
        register(task?.id, `task ${task?.id ?? '<missing>'}`);
        checkTask(errors, task, phase?.id, epic?.id);
        dependencyEdges.set(task?.id, task?.depends_on ?? []);
      }
    }
  }

  for (const decision of decisions?.decisions ?? []) {
    const location = `decision ${decision?.id ?? '<missing>'}`;
    register(decision?.id, location);
    if (!['open', 'accepted', 'rejected', 'superseded'].includes(decision?.status))
      addError(errors, `${location}: invalid status`);
    if (!priorities.has(decision?.priority)) addError(errors, `${location}: invalid priority`);
    checkSourceRefs(errors, decision?.source_refs, location);
  }

  for (const risk of risks?.risks ?? []) {
    const location = `risk ${risk?.id ?? '<missing>'}`;
    register(risk?.id, location);
    if (!['open', 'mitigated', 'accepted', 'closed'].includes(risk?.status))
      addError(errors, `${location}: invalid status`);
    if (!['low', 'medium', 'high'].includes(risk?.probability))
      addError(errors, `${location}: invalid probability`);
    if (!['low', 'medium', 'high', 'critical'].includes(risk?.impact))
      addError(errors, `${location}: invalid impact`);
    checkSourceRefs(errors, risk?.source_refs, location);
  }

  for (const [id, dependencies] of dependencyEdges) {
    for (const dependency of dependencies) {
      if (!ids.has(dependency)) addError(errors, `${id}: dependency does not exist: ${dependency}`);
    }
  }

  const visiting = new Set<string>();
  const visited = new Set<string>();
  const visit = (id: string, trail: string[] = []) => {
    if (visiting.has(id)) {
      addError(errors, `dependency cycle detected: ${[...trail, id].join(' -> ')}`);
      return;
    }
    if (visited.has(id)) return;
    visiting.add(id);
    for (const dependency of dependencyEdges.get(id) ?? []) {
      if (dependencyEdges.has(dependency)) visit(dependency, [...trail, id]);
    }
    visiting.delete(id);
    visited.add(id);
  };
  for (const id of dependencyEdges.keys()) visit(id);

  const completeTasks = [...ids.keys()].filter(
    (id) =>
      id.startsWith('FND-') ||
      id.startsWith('MVP-') ||
      id.startsWith('GROW-') ||
      id.startsWith('PORT-'),
  );
  const recorded = new Set(root?.current_state?.completed_task_ids ?? []);
  for (const id of recorded) {
    if (!completeTasks.includes(id))
      addError(errors, `current_state.completed_task_ids contains unknown task ${id}`);
  }

  return errors;
}

if (import.meta.main) {
  try {
    const data = loadRoadmap();
    const errors = validateRoadmap(data);
    if (errors.length > 0) {
      console.error(`Roadmap validation failed with ${errors.length} error(s):`);
      for (const error of errors) console.error(`- ${error}`);
      process.exit(1);
    }
    const taskCount = data.phases.reduce(
      (count, item) =>
        count +
        item.phase.epics.reduce((epicCount: number, epic: any) => epicCount + epic.tasks.length, 0),
      0,
    );
    console.log(
      `Roadmap valid: ${data.phases.length} phases, ${taskCount} tasks, ${data.decisions.decisions.length} decisions, ${data.risks.risks.length} risks.`,
    );
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error));
    process.exit(1);
  }
}
