import type { JobRequestResponse, JobRequestUnitDto } from '@/lib/api';

export const PACKAGE_ITEM_STATUS_KEYS = {
  moiNotReceived: 'moi_not_received',
  awaitingIntake: 'awaiting_intake',
  resolutionPrep: 'resolution_prep',
  pendingRecommendation: 'pending_recommendation',
  moiSignOff: 'moi_sign_off',
  moiApproved: 'moi_approved',
  completed: 'completed',
  awaitingMoi: 'awaiting_moi',
  pendingSignOff: 'pending_sign_off',
  approved: 'approved',
  moiNotComplete: 'moi_not_complete',
  readyForMoa: 'ready_for_moa',
  moaCirculation: 'moa_circulation',
  pendingExecute: 'pending_execute',
  notStarted: 'not_started',
  inProgress: 'in_progress',
  canceled: 'canceled',
} as const;

const LABELS: Record<string, string> = {
  [PACKAGE_ITEM_STATUS_KEYS.moiNotReceived]: 'MOI not received',
  [PACKAGE_ITEM_STATUS_KEYS.awaitingIntake]: 'With LGB for review',
  [PACKAGE_ITEM_STATUS_KEYS.resolutionPrep]: 'Resolution prep',
  [PACKAGE_ITEM_STATUS_KEYS.pendingRecommendation]: 'Pending recommendation',
  [PACKAGE_ITEM_STATUS_KEYS.moiSignOff]: 'MOI sign-off',
  [PACKAGE_ITEM_STATUS_KEYS.moiApproved]: 'MOI approved',
  [PACKAGE_ITEM_STATUS_KEYS.completed]: 'Completed',
  [PACKAGE_ITEM_STATUS_KEYS.awaitingMoi]: 'Awaiting MOI',
  [PACKAGE_ITEM_STATUS_KEYS.pendingSignOff]: 'Pending sign-off',
  [PACKAGE_ITEM_STATUS_KEYS.approved]: 'Approved',
  [PACKAGE_ITEM_STATUS_KEYS.moiNotComplete]: 'MOI not complete',
  [PACKAGE_ITEM_STATUS_KEYS.readyForMoa]: 'Ready for MOA',
  [PACKAGE_ITEM_STATUS_KEYS.moaCirculation]: 'MOA circulation',
  [PACKAGE_ITEM_STATUS_KEYS.pendingExecute]: 'Pending execute',
  [PACKAGE_ITEM_STATUS_KEYS.notStarted]: 'Not started',
  [PACKAGE_ITEM_STATUS_KEYS.inProgress]: 'In progress',
  [PACKAGE_ITEM_STATUS_KEYS.canceled]: 'Canceled',
};

const BADGE_CLASSES: Record<string, string> = {
  [PACKAGE_ITEM_STATUS_KEYS.moiNotReceived]: 'bg-slate-100 text-slate-700',
  [PACKAGE_ITEM_STATUS_KEYS.awaitingIntake]: 'bg-amber-100 text-amber-900',
  [PACKAGE_ITEM_STATUS_KEYS.resolutionPrep]: 'bg-blue-100 text-blue-800',
  [PACKAGE_ITEM_STATUS_KEYS.pendingRecommendation]: 'bg-indigo-100 text-indigo-800',
  [PACKAGE_ITEM_STATUS_KEYS.moiSignOff]: 'bg-purple-100 text-purple-800',
  [PACKAGE_ITEM_STATUS_KEYS.moiApproved]: 'bg-teal-100 text-teal-800',
  [PACKAGE_ITEM_STATUS_KEYS.completed]: 'bg-green-100 text-green-800',
  [PACKAGE_ITEM_STATUS_KEYS.awaitingMoi]: 'bg-slate-100 text-slate-600',
  [PACKAGE_ITEM_STATUS_KEYS.pendingSignOff]: 'bg-purple-100 text-purple-800',
  [PACKAGE_ITEM_STATUS_KEYS.approved]: 'bg-green-100 text-green-800',
  [PACKAGE_ITEM_STATUS_KEYS.moiNotComplete]: 'bg-slate-100 text-slate-600',
  [PACKAGE_ITEM_STATUS_KEYS.readyForMoa]: 'bg-cyan-100 text-cyan-900',
  [PACKAGE_ITEM_STATUS_KEYS.moaCirculation]: 'bg-blue-100 text-blue-800',
  [PACKAGE_ITEM_STATUS_KEYS.pendingExecute]: 'bg-emerald-100 text-emerald-900',
  [PACKAGE_ITEM_STATUS_KEYS.notStarted]: 'bg-yellow-100 text-yellow-800',
  [PACKAGE_ITEM_STATUS_KEYS.inProgress]: 'bg-blue-100 text-blue-800',
  [PACKAGE_ITEM_STATUS_KEYS.canceled]: 'bg-red-100 text-red-800',
};

export const MOI_WORKFLOW_STATE_LABELS: Record<string, string> = {
  Draft: 'MOI not received',
  PendingClientMoiApproval: 'Pending client approval',
  PendingAdminIntake: 'With LGB for review',
  PendingPrep: 'Resolution prep',
  PendingRecommendation: 'Pending recommendation',
  Approved: 'MOI approved',
};

function fallbackKey(job: JobRequestResponse): string {
  if (job.status === 'Canceled') return PACKAGE_ITEM_STATUS_KEYS.canceled;
  if (job.status === 'Completed') return PACKAGE_ITEM_STATUS_KEYS.completed;

  const handoff = job.internalHandoffStatus ?? '';
  if (job.taskType === 'MOI' || job.taskType === 'Service') {
    if (handoff === 'ClientSubmitted') return PACKAGE_ITEM_STATUS_KEYS.awaitingIntake;
    if (handoff === 'PendingPrep' || handoff === 'ResoInProgress') return PACKAGE_ITEM_STATUS_KEYS.resolutionPrep;
    if (handoff === 'AdminReview') return PACKAGE_ITEM_STATUS_KEYS.moiSignOff;
    if (handoff === 'ReadyForMoa' || handoff === 'MoaCirculation') return PACKAGE_ITEM_STATUS_KEYS.moiApproved;
    if (!handoff && job.taskType === 'Service') return PACKAGE_ITEM_STATUS_KEYS.moiNotReceived;
    if (!handoff) return PACKAGE_ITEM_STATUS_KEYS.moiNotReceived;
  }
  if (job.taskType === 'MOA') {
    if (handoff === 'PendingExecute') return PACKAGE_ITEM_STATUS_KEYS.pendingExecute;
    if (handoff === 'ReadyForMoa') return PACKAGE_ITEM_STATUS_KEYS.readyForMoa;
    if (handoff === 'MoaCirculation') return PACKAGE_ITEM_STATUS_KEYS.moaCirculation;
    if (handoff === 'MoaSharonApproved') return PACKAGE_ITEM_STATUS_KEYS.moiSignOff;
    return PACKAGE_ITEM_STATUS_KEYS.moiNotComplete;
  }
  if (job.jobAssignedTo || job.assignedUserId) return PACKAGE_ITEM_STATUS_KEYS.inProgress;
  return PACKAGE_ITEM_STATUS_KEYS.notStarted;
}

export function packageItemStatusBadgeClass(statusKey?: string): string {
  if (!statusKey) return 'bg-yellow-100 text-yellow-800';
  return BADGE_CLASSES[statusKey] ?? 'bg-muted text-muted-foreground';
}

export function moiWorkflowStateLabel(state?: string): string {
  if (!state) return '—';
  return MOI_WORKFLOW_STATE_LABELS[state] ?? state;
}

export function resolveDisplayStatus(job: JobRequestResponse): { key: string; label: string } {
  if (job.displayStatusKey) {
    return {
      key: job.displayStatusKey,
      label: job.displayStatus || LABELS[job.displayStatusKey] || job.displayStatusKey,
    };
  }
  if (job.displayStatus) {
    return { key: fallbackKey(job), label: job.displayStatus };
  }
  const key = fallbackKey(job);
  return { key, label: LABELS[key] ?? '—' };
}

export function displayStatusLabel(job: JobRequestResponse): string {
  return resolveDisplayStatus(job).label;
}

export function displayStatusKey(job: JobRequestResponse): string {
  return resolveDisplayStatus(job).key;
}

export function jobHasMoiForm(job: JobRequestResponse): boolean {
  return Boolean(job.hasMoiForm) || (job.linkedFormKind === 'MOI' && Boolean(job.linkedFormId));
}

export function jobHasMoaForm(job: JobRequestResponse): boolean {
  return Boolean(job.hasMoaForm) || (job.linkedFormKind === 'MOA' && Boolean(job.linkedFormId));
}

export function unitHasMoiForm(job: JobRequestResponse, unit: JobRequestUnitDto): boolean {
  if ((job.totalQty ?? 1) > 1) {
    return Boolean(unit.hasMoiForm) || (unit.linkedFormKind === 'MOI' && Boolean(unit.linkedFormId));
  }
  return jobHasMoiForm(job);
}

export function unitHasMoaForm(job: JobRequestResponse, unit: JobRequestUnitDto): boolean {
  if ((job.totalQty ?? 1) > 1) {
    return Boolean(unit.hasMoaForm) || (unit.linkedFormKind === 'MOA' && Boolean(unit.linkedFormId));
  }
  return jobHasMoaForm(job);
}

export function resolveUnitDisplayStatus(
  job: JobRequestResponse,
  unit: JobRequestUnitDto,
): { key: string; label: string } {
  if (unit.displayStatusKey) {
    return {
      key: unit.displayStatusKey,
      label: unit.displayStatus || LABELS[unit.displayStatusKey] || unit.displayStatusKey,
    };
  }
  return resolveDisplayStatus(job);
}

export function displayStatusLabelForUnit(job: JobRequestResponse, unit: JobRequestUnitDto): string {
  return resolveUnitDisplayStatus(job, unit).label;
}

export function displayStatusKeyForUnit(job: JobRequestResponse, unit: JobRequestUnitDto): string {
  return resolveUnitDisplayStatus(job, unit).key;
}

export function canClientStartMoi(job: JobRequestResponse, unit?: JobRequestUnitDto): boolean {
  if (job.taskType !== 'Service' && job.taskType !== 'MOI')
    return false;
  if (unit && (job.totalQty ?? 1) > 1) {
    return !unitHasMoiForm(job, unit) && !unit.linkedFormId;
  }
  const key = displayStatusKey(job);
  return key === PACKAGE_ITEM_STATUS_KEYS.moiNotReceived || !job.linkedFormId;
}

export function signatoryCanSignMoi(
  job: JobRequestResponse,
  user: { name: string; needsMoiApproval?: boolean },
  unit?: JobRequestUnitDto,
): boolean {
  if (!user.needsMoiApproval)
    return false;
  if (job.taskType !== 'MOI' && job.taskType !== 'Service')
    return false;
  const key = unit ? displayStatusKeyForUnit(job, unit) : displayStatusKey(job);
  return key === PACKAGE_ITEM_STATUS_KEYS.pendingSignOff
    || (unit?.moiWorkflowState === 'PendingClientMoiApproval');
}

export function canSignatoryStartMoi(
  job: JobRequestResponse,
  user: { name: string; needsMoi?: boolean },
): boolean {
  if (!user.needsMoi || !canClientStartMoi(job))
    return false;

  if (job.taskType !== 'MOI' && job.taskType !== 'Service')
    return false;

  const holder = (job.accountHolder ?? '').trim();
  if (!holder)
    return job.taskType === 'Service';

  return holder.localeCompare(user.name.trim(), undefined, { sensitivity: 'accent' }) === 0;
}

export function canOpenMoiForm(job: JobRequestResponse): boolean {
  return job.taskType === 'MOI' || job.taskType === 'MOI Approval' || job.taskType === 'Service';
}

export function canAssignSecretarialTeam(job: JobRequestResponse, _allJobs: JobRequestResponse[]): boolean {
  const handoff = job.internalHandoffStatus ?? '';
  if (handoff === 'ClientSubmitted') return false;
  if (handoff === 'ReadyForMoa' || handoff === 'MoaCirculation' || handoff === 'Completed' || handoff === 'PendingExecute') {
    return false;
  }
  const key = displayStatusKey(job);
  return key === PACKAGE_ITEM_STATUS_KEYS.resolutionPrep
    || key === PACKAGE_ITEM_STATUS_KEYS.pendingRecommendation
    || key === PACKAGE_ITEM_STATUS_KEYS.moiSignOff
    || key === PACKAGE_ITEM_STATUS_KEYS.moiApproved
    || handoff === 'PendingPrep'
    || handoff === 'ResoInProgress'
    || handoff === 'AdminReview';
}

export function canOpenMoaForm(job: JobRequestResponse): boolean {
  const key = displayStatusKey(job);
  return job.taskType === 'MOA'
    && (key === PACKAGE_ITEM_STATUS_KEYS.readyForMoa
      || key === PACKAGE_ITEM_STATUS_KEYS.moaCirculation
      || key === PACKAGE_ITEM_STATUS_KEYS.pendingExecute
      || key === PACKAGE_ITEM_STATUS_KEYS.completed
      || jobHasMoaForm(job));
}

/** Service-line jobs in the MOA preparation / circulation phase (internal staff). */
export function canOpenMoaForJob(job: JobRequestResponse): boolean {
  if (canOpenMoaForm(job) || jobHasMoaForm(job) || job.linkedFormKind === 'MOA')
    return true;
  if (job.taskType !== 'Service')
    return false;
  const handoff = job.internalHandoffStatus ?? '';
  const key = displayStatusKey(job);
  return handoff === 'AdminReview'
    || handoff === 'MoaSharonApproved'
    || handoff === 'ReadyForMoa'
    || handoff === 'MoaCirculation'
    || handoff === 'PendingExecute'
    || key === PACKAGE_ITEM_STATUS_KEYS.moiApproved
    || key === PACKAGE_ITEM_STATUS_KEYS.readyForMoa
    || key === PACKAGE_ITEM_STATUS_KEYS.moaCirculation;
}
