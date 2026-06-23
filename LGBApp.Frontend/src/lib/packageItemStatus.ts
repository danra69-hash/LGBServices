import type { JobRequestResponse, JobRequestUnitDto, UserResponse, WorkTrackerItemDto } from '@/lib/api';

export const PACKAGE_ITEM_STATUS_KEYS = {
  moiNotReceived: 'moi_not_received',
  moiRejected: 'moi_rejected',
  awaitingIntake: 'awaiting_intake',
  resolutionPrep: 'resolution_prep',
  pendingRecommendation: 'pending_recommendation',
  moiSignOff: 'moi_sign_off',
  awaitingSecAssignment: 'awaiting_sec_assignment',
  moiApproved: 'moi_approved',
  completed: 'completed',
  awaitingMoi: 'awaiting_moi',
  pendingSignOff: 'pending_sign_off',
  approved: 'approved',
  moiNotComplete: 'moi_not_complete',
  readyForMoa: 'ready_for_moa',
  moaCirculation: 'moa_circulation',
  pendingExecute: 'pending_execute',
  execution: 'execution',
  executionPendingApproval: 'execution_pending_approval',
  notStarted: 'not_started',
  inProgress: 'in_progress',
  canceled: 'canceled',
} as const;

const LABELS: Record<string, string> = {
  [PACKAGE_ITEM_STATUS_KEYS.moiNotReceived]: 'MOI not received',
  [PACKAGE_ITEM_STATUS_KEYS.moiRejected]: 'MOI rejected',
  [PACKAGE_ITEM_STATUS_KEYS.awaitingIntake]: 'With LGB for review',
  [PACKAGE_ITEM_STATUS_KEYS.resolutionPrep]: 'Resolution prep',
  [PACKAGE_ITEM_STATUS_KEYS.pendingRecommendation]: 'Pending recommendation',
  [PACKAGE_ITEM_STATUS_KEYS.moiSignOff]: 'MOI sign-off',
  [PACKAGE_ITEM_STATUS_KEYS.awaitingSecAssignment]: 'Assign secretarial team',
  [PACKAGE_ITEM_STATUS_KEYS.moiApproved]: 'MOI approved',
  [PACKAGE_ITEM_STATUS_KEYS.completed]: 'Completed',
  [PACKAGE_ITEM_STATUS_KEYS.awaitingMoi]: 'Awaiting MOI',
  [PACKAGE_ITEM_STATUS_KEYS.pendingSignOff]: 'Pending sign-off',
  [PACKAGE_ITEM_STATUS_KEYS.approved]: 'Approved',
  [PACKAGE_ITEM_STATUS_KEYS.moiNotComplete]: 'MOI not complete',
  [PACKAGE_ITEM_STATUS_KEYS.readyForMoa]: 'Ready for MOA',
  [PACKAGE_ITEM_STATUS_KEYS.moaCirculation]: 'MOA circulation',
  [PACKAGE_ITEM_STATUS_KEYS.pendingExecute]: 'Executing',
  [PACKAGE_ITEM_STATUS_KEYS.execution]: 'Executing',
  [PACKAGE_ITEM_STATUS_KEYS.executionPendingApproval]: 'Awaiting execution sign-off',
  [PACKAGE_ITEM_STATUS_KEYS.notStarted]: 'Not started',
  [PACKAGE_ITEM_STATUS_KEYS.inProgress]: 'In progress',
  [PACKAGE_ITEM_STATUS_KEYS.canceled]: 'Canceled',
};

const BADGE_CLASSES: Record<string, string> = {
  [PACKAGE_ITEM_STATUS_KEYS.moiNotReceived]: 'bg-slate-100 text-slate-700',
  [PACKAGE_ITEM_STATUS_KEYS.moiRejected]: 'bg-red-100 text-red-800',
  [PACKAGE_ITEM_STATUS_KEYS.awaitingIntake]: 'bg-amber-100 text-amber-900',
  [PACKAGE_ITEM_STATUS_KEYS.resolutionPrep]: 'bg-blue-100 text-blue-800',
  [PACKAGE_ITEM_STATUS_KEYS.pendingRecommendation]: 'bg-indigo-100 text-indigo-800',
  [PACKAGE_ITEM_STATUS_KEYS.moiSignOff]: 'bg-purple-100 text-purple-800',
  [PACKAGE_ITEM_STATUS_KEYS.awaitingSecAssignment]: 'bg-orange-100 text-orange-900',
  [PACKAGE_ITEM_STATUS_KEYS.moiApproved]: 'bg-teal-100 text-teal-800',
  [PACKAGE_ITEM_STATUS_KEYS.completed]: 'bg-green-100 text-green-800',
  [PACKAGE_ITEM_STATUS_KEYS.awaitingMoi]: 'bg-slate-100 text-slate-600',
  [PACKAGE_ITEM_STATUS_KEYS.pendingSignOff]: 'bg-purple-100 text-purple-800',
  [PACKAGE_ITEM_STATUS_KEYS.approved]: 'bg-green-100 text-green-800',
  [PACKAGE_ITEM_STATUS_KEYS.moiNotComplete]: 'bg-slate-100 text-slate-600',
  [PACKAGE_ITEM_STATUS_KEYS.readyForMoa]: 'bg-cyan-100 text-cyan-900',
  [PACKAGE_ITEM_STATUS_KEYS.moaCirculation]: 'bg-blue-100 text-blue-800',
  [PACKAGE_ITEM_STATUS_KEYS.pendingExecute]: 'bg-emerald-100 text-emerald-900',
  [PACKAGE_ITEM_STATUS_KEYS.execution]: 'bg-emerald-100 text-emerald-900',
  [PACKAGE_ITEM_STATUS_KEYS.executionPendingApproval]: 'bg-violet-100 text-violet-900',
  [PACKAGE_ITEM_STATUS_KEYS.notStarted]: 'bg-yellow-100 text-yellow-800',
  [PACKAGE_ITEM_STATUS_KEYS.inProgress]: 'bg-blue-100 text-blue-800',
  [PACKAGE_ITEM_STATUS_KEYS.canceled]: 'bg-red-100 text-red-800',
};

export const MOI_WORKFLOW_STATE_LABELS: Record<string, string> = {
  Draft: 'MOI not received',
  MoiRejected: 'MOI rejected',
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
  if (handoff === 'PendingExecute' || handoff === 'ExecutionSecComplete') {
    return PACKAGE_ITEM_STATUS_KEYS.execution;
  }
  if (job.taskType === 'MOI' || job.taskType === 'Service') {
    if (handoff === 'ClientSubmitted') return PACKAGE_ITEM_STATUS_KEYS.awaitingIntake;
    if (handoff === 'AwaitingSecAssignment') return PACKAGE_ITEM_STATUS_KEYS.resolutionPrep;
    if (handoff === 'PendingPrep' || handoff === 'ResoInProgress') return PACKAGE_ITEM_STATUS_KEYS.resolutionPrep;
    if (handoff === 'AdminReview') return PACKAGE_ITEM_STATUS_KEYS.moiSignOff;
    if (handoff === 'MoaCirculation') return PACKAGE_ITEM_STATUS_KEYS.moaCirculation;
    if (handoff === 'ReadyForMoa') return PACKAGE_ITEM_STATUS_KEYS.readyForMoa;
    if (!handoff && job.taskType === 'Service') return PACKAGE_ITEM_STATUS_KEYS.moiNotReceived;
    if (!handoff) return PACKAGE_ITEM_STATUS_KEYS.moiNotReceived;
  }
  if (job.taskType === 'MOA') {
    if (handoff === 'ReadyForMoa') return PACKAGE_ITEM_STATUS_KEYS.readyForMoa;
    if (handoff === 'MoaCirculation') return PACKAGE_ITEM_STATUS_KEYS.moaCirculation;
    if (handoff === 'Completed') return PACKAGE_ITEM_STATUS_KEYS.completed;
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

/** True when assigned internal secretaries may edit the MOA pack (resolution prep). */
export function canInternalEditMoa(
  handoff: string,
  options?: {
    hasWorkflow?: boolean;
    sharonApproved?: boolean;
    awaitingAdminReview?: boolean;
    canApproveMoa?: boolean;
    isAdmin?: boolean;
    moiWorkflowState?: string;
  },
): boolean {
  if (options?.hasWorkflow) return Boolean(options.isAdmin || options.canApproveMoa);
  if (options?.sharonApproved) return false;
  if (options?.awaitingAdminReview) return Boolean(options.isAdmin || options.canApproveMoa);
  if (handoff === 'PendingPrep' || handoff === 'ResoInProgress') return true;
  const moiState = options?.moiWorkflowState ?? '';
  if (handoff === 'AwaitingSecAssignment' && (moiState === 'Approved' || moiState === 'PendingPrep')) {
    return true;
  }
  return (moiState === 'PendingPrep' || moiState === 'PendingRecommendation')
    && handoff !== 'ClientSubmitted';
}

export function scopeJobForUnit(
  job: JobRequestResponse,
  unit?: JobRequestUnitDto,
): JobRequestResponse {
  if (!unit || (job.totalQty ?? 1) <= 1) return job;
  return {
    ...job,
    internalHandoffStatus: effectiveUnitHandoff(job, unit),
    displayStatusKey: unit.displayStatusKey ?? job.displayStatusKey,
    moiWorkflowState: unit.moiWorkflowState ?? job.moiWorkflowState,
    hasMoaForm: unit.hasMoaForm ?? job.hasMoaForm,
    hasMoiForm: unit.hasMoiForm ?? job.hasMoiForm,
    linkedFormKind: unit.linkedFormKind ?? job.linkedFormKind,
    linkedFormId: unit.linkedFormId ?? job.linkedFormId,
    activeUnitNumber: unit.unitNumber,
  };
}

/** Handoffs where internal staff work on the MOA pack (not client MOI). */
export function isMoaInternalWorkflowHandoff(handoff: string): boolean {
  return handoff === 'PendingPrep'
    || handoff === 'ResoInProgress'
    || handoff === 'AdminReview'
    || handoff === 'MoaSharonApproved'
    || handoff === 'ReadyForMoa'
    || handoff === 'MoaCirculation'
    || handoff === 'Completed';
}

/** Handoffs where internal staff may open the MOA form (prep through execution). */
export function isMoaViewableHandoff(handoff: string): boolean {
  return isMoaInternalWorkflowHandoff(handoff) || isExecutingHandoff(handoff);
}

/** True when the secretary should open/prepare MOA (resolution prep onward). */
export function shouldOpenMoaForm(job: JobRequestResponse, unit?: JobRequestUnitDto): boolean {
  const scoped = scopeJobForUnit(job, unit);

  if (scoped.taskType !== 'Service')
    return canOpenMoaForm(scoped);

  const handoff = scoped.internalHandoffStatus ?? '';
  if (isMoaViewableHandoff(handoff))
    return true;

  if (isExecutingStatusKey(scoped.displayStatusKey) && scoped.taskType === 'Service')
    return true;

  const moiState = scoped.moiWorkflowState ?? '';
  if (
    handoff === 'AwaitingSecAssignment'
    && (moiState === 'Approved' || moiState === 'PendingPrep' || unitHasMoaForm(scoped, unit))
  ) {
    return true;
  }

  if (
    (scoped.displayStatusKey === PACKAGE_ITEM_STATUS_KEYS.resolutionPrep
      || moiState === 'PendingPrep'
      || moiState === 'PendingRecommendation')
    && handoff !== 'ClientSubmitted'
    && moiState !== 'PendingAdminIntake'
    && moiState !== 'PendingClientMoiApproval'
  ) {
    return true;
  }

  return canOpenMoaForJob(scoped, unit);
}

export function unitHasMoiForm(job: JobRequestResponse, unit?: JobRequestUnitDto): boolean {
  if ((job.totalQty ?? 1) > 1) {
    if (!unit) return false;
    return Boolean(unit.hasMoiForm) || (unit.linkedFormKind === 'MOI' && Boolean(unit.linkedFormId));
  }
  return jobHasMoiForm(job);
}

export function unitHasMoaForm(job: JobRequestResponse, unit?: JobRequestUnitDto): boolean {
  if ((job.totalQty ?? 1) > 1) {
    if (!unit) return false;
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
  if ((job.totalQty ?? 1) > 1) {
    const handoff = unit.internalHandoffStatus ?? '';
    const key = fallbackKey({
      ...job,
      internalHandoffStatus: handoff,
      moiWorkflowState: unit.moiWorkflowState ?? job.moiWorkflowState,
    });
    return { key, label: LABELS[key] ?? key };
  }
  return resolveDisplayStatus(job);
}

export function displayStatusLabelForUnit(job: JobRequestResponse, unit: JobRequestUnitDto): string {
  return resolveUnitDisplayStatus(job, unit).label;
}

export function displayStatusKeyForUnit(job: JobRequestResponse, unit: JobRequestUnitDto): string {
  return resolveUnitDisplayStatus(job, unit).key;
}

export function isMoiRejected(
  job: JobRequestResponse,
  unit?: JobRequestUnitDto,
): boolean {
  if (unit) {
    if (unit.moiWorkflowState === 'MoiRejected') return true;
    return displayStatusKeyForUnit(job, unit) === PACKAGE_ITEM_STATUS_KEYS.moiRejected;
  }
  if (job.moiWorkflowState === 'MoiRejected') return true;
  return displayStatusKey(job) === PACKAGE_ITEM_STATUS_KEYS.moiRejected;
}

export function canClientStartMoi(job: JobRequestResponse, unit?: JobRequestUnitDto): boolean {
  if (job.taskType !== 'Service' && job.taskType !== 'MOI')
    return false;
  if (isMoiRejected(job, unit))
    return false;
  if ((job.totalQty ?? 1) > 1) {
    if (!unit) return false;
    return !unitHasMoiForm(job, unit) && !unit.linkedFormId;
  }
  if (unitHasMoiForm(job, unit) || job.linkedFormId)
    return false;
  const key = displayStatusKey(job);
  return key === PACKAGE_ITEM_STATUS_KEYS.moiNotReceived;
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

/** Client may sign MOA once LGB has released it (ReadyForMoa) or while circulation is in progress. */
export function isMoaClientSignoffHandoff(handoff: string): boolean {
  return handoff === 'ReadyForMoa' || handoff === 'MoaCirculation';
}

/** MOA is with client signatories for approval (not internal prep). */
export function isMoaClientSignoffPhase(job: JobRequestResponse, unit?: JobRequestUnitDto): boolean {
  const handoff = unit && (job.totalQty ?? 1) > 1
    ? effectiveUnitHandoff(job, unit)
    : (job.internalHandoffStatus ?? '');
  if (isMoaClientSignoffHandoff(handoff)) return true;
  const key = unit ? displayStatusKeyForUnit(job, unit) : displayStatusKey(job);
  return key === PACKAGE_ITEM_STATUS_KEYS.moaCirculation
    || key === PACKAGE_ITEM_STATUS_KEYS.readyForMoa;
}

/** Client signatory or admin who is listed as an MOA approver and may sign now. */
export function signatoryCanSignMoa(
  job: JobRequestResponse,
  user: { name: string; needsMoa?: boolean; isInternalSignatory?: boolean; canApproveMoa?: boolean },
  unit?: JobRequestUnitDto,
): boolean {
  if (!unitHasMoaForm(job, unit)) return false;
  if (!isMoaClientSignoffPhase(job, unit)) return false;
  if (user.needsMoa) return true;
  return Boolean(user.isInternalSignatory || user.canApproveMoa);
}

/** Client may open MOA read-only (e.g. MOI-only signatories after MOA is released). */
export function canClientViewMoa(job: JobRequestResponse, unit?: JobRequestUnitDto): boolean {
  if (!unitHasMoaForm(job, unit)) return false;
  const handoff = unit && (job.totalQty ?? 1) > 1
    ? effectiveUnitHandoff(job, unit)
    : (job.internalHandoffStatus ?? '');
  if (handoff === 'MoaCirculation' || handoff === 'ReadyForMoa' || handoff === 'Completed') {
    return true;
  }
  const key = unit ? displayStatusKeyForUnit(job, unit) : displayStatusKey(job);
  return key === PACKAGE_ITEM_STATUS_KEYS.moaCirculation
    || key === PACKAGE_ITEM_STATUS_KEYS.readyForMoa
    || key === PACKAGE_ITEM_STATUS_KEYS.completed;
}

/** Client may view a signed or in-progress MOI (paper trail during and after MOA). */
export function canClientViewMoi(job: JobRequestResponse, unit?: JobRequestUnitDto): boolean {
  if (!unitHasMoiForm(job, unit)) return false;

  const moiState = unit?.moiWorkflowState ?? job.moiWorkflowState ?? '';
  if (
    moiState === 'Draft'
    || moiState === 'Approved'
    || moiState === 'PendingClientMoiApproval'
    || moiState === 'MoiRejected'
  ) {
    return true;
  }

  const key = unit ? displayStatusKeyForUnit(job, unit) : displayStatusKey(job);
  return key !== PACKAGE_ITEM_STATUS_KEYS.moiNotReceived
    && key !== PACKAGE_ITEM_STATUS_KEYS.awaitingIntake;
}

export function canSignatoryStartMoi(
  job: JobRequestResponse,
  user: { name: string; needsMoi?: boolean },
  unit?: JobRequestUnitDto,
): boolean {
  if (!user.needsMoi || !canClientStartMoi(job, unit))
    return false;

  if (job.taskType !== 'MOI' && job.taskType !== 'Service')
    return false;

  const holder = (job.accountHolder ?? '').trim();
  if (!holder)
    return job.taskType === 'Service';

  return holder.localeCompare(user.name.trim(), undefined, { sensitivity: 'accent' }) === 0;
}

/** Client may edit MOI in Draft or after LGB/client rejection (MoiRejected). */
export function canClientEditMoiDraft(
  job: JobRequestResponse,
  options: {
    workflowState?: string;
    isClientAdmin?: boolean;
    isSignatory?: boolean;
    userName?: string;
  },
): boolean {
  if (job.taskType === 'MOI Approval')
    return false;
  if (job.taskType !== 'MOI' && job.taskType !== 'Service')
    return false;

  const state = options.workflowState ?? job.moiWorkflowState ?? 'Draft';
  if (state !== 'Draft' && state !== 'MoiRejected')
    return false;

  if (options.isClientAdmin)
    return true;

  if (options.isSignatory && options.userName) {
    const holder = (job.accountHolder ?? '').trim();
    if (!holder)
      return job.taskType === 'Service';
    return holder.localeCompare(options.userName.trim(), undefined, { sensitivity: 'accent' }) === 0;
  }

  return false;
}

export function canInternalEditMoi(workflowState?: string): boolean {
  return workflowState === 'PendingPrep' || workflowState === 'PendingRecommendation';
}

export function canOpenMoiForm(job: JobRequestResponse): boolean {
  return job.taskType === 'MOI' || job.taskType === 'MOI Approval' || job.taskType === 'Service';
}

export function jobUnitsForAssignment(job: JobRequestResponse): JobRequestUnitDto[] {
  if (job.units?.length) return job.units;
  return [{
    id: 0,
    unitNumber: 1,
    assignedUserName: job.jobAssignedTo ?? '',
    status: job.status as JobRequestUnitDto['status'],
    scheduledDate: job.scheduledDate,
    assignees: [],
  }];
}

export function canAssignSecretarialTeam(job: JobRequestResponse, _allJobs: JobRequestResponse[]): boolean {
  const units = jobUnitsForAssignment(job);
  if ((job.totalQty ?? 1) > 1) {
    return units.some((unit) => canAssignUnit(job, unit, units));
  }
  const handoff = job.internalHandoffStatus ?? '';
  if (handoff === 'ClientSubmitted') return false;
  if (handoff === 'AwaitingSecAssignment') return true;
  if (handoff === 'ReadyForMoa' || handoff === 'MoaCirculation' || handoff === 'Completed') {
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

export function isUnitMoiComplete(unit: JobRequestUnitDto): boolean {
  if (unit.moiWorkflowState === 'Approved') return true;
  const key = unit.displayStatusKey ?? '';
  return key === PACKAGE_ITEM_STATUS_KEYS.moiApproved
    || key === PACKAGE_ITEM_STATUS_KEYS.resolutionPrep
    || key === PACKAGE_ITEM_STATUS_KEYS.pendingRecommendation
    || key === PACKAGE_ITEM_STATUS_KEYS.readyForMoa
    || key === PACKAGE_ITEM_STATUS_KEYS.moaCirculation
    || key === PACKAGE_ITEM_STATUS_KEYS.completed;
}

export function canAssignSecretarialTeamForUnit(job: JobRequestResponse, unit: JobRequestUnitDto): boolean {
  const handoff = effectiveUnitHandoff(job, unit);
  if (handoff === 'ClientSubmitted') return false;
  if (handoff === 'AwaitingSecAssignment') return true;
  if (handoff === 'ReadyForMoa' || handoff === 'MoaCirculation' || handoff === 'Completed') {
    return false;
  }
  const key = unit.displayStatusKey ?? displayStatusKeyForUnit(job, unit);
  return key === PACKAGE_ITEM_STATUS_KEYS.resolutionPrep
    || key === PACKAGE_ITEM_STATUS_KEYS.pendingRecommendation
    || key === PACKAGE_ITEM_STATUS_KEYS.moiSignOff
    || key === PACKAGE_ITEM_STATUS_KEYS.moiApproved
    || key === PACKAGE_ITEM_STATUS_KEYS.readyForMoa
    || key === PACKAGE_ITEM_STATUS_KEYS.moaCirculation
    || handoff === 'PendingPrep'
    || handoff === 'ResoInProgress'
    || handoff === 'AdminReview';
}

export function canAssignUnit(
  job: JobRequestResponse,
  unit: JobRequestUnitDto,
  units: JobRequestUnitDto[],
): boolean {
  if (!canAssignSecretarialTeamForUnit(job, unit)) return false;

  if (unit.unitNumber <= 1) {
    return Boolean(unit.hasMoiForm) || (unit.displayStatusKey ?? displayStatusKeyForUnit(job, unit)) !== PACKAGE_ITEM_STATUS_KEYS.moiNotReceived;
  }

  const previous = units.find((u) => u.unitNumber === unit.unitNumber - 1);
  return previous != null && isUnitMoiComplete(previous);
}

export function assignableUnitsForJob(job: JobRequestResponse): JobRequestUnitDto[] {
  const units = jobUnitsForAssignment(job);
  return units.filter((unit) => canAssignUnit(job, unit, units));
}

export function canOpenMoaForm(job: JobRequestResponse): boolean {
  const key = displayStatusKey(job);
  return job.taskType === 'MOA'
    && (key === PACKAGE_ITEM_STATUS_KEYS.readyForMoa
      || key === PACKAGE_ITEM_STATUS_KEYS.moaCirculation
      || key === PACKAGE_ITEM_STATUS_KEYS.execution
      || key === PACKAGE_ITEM_STATUS_KEYS.pendingExecute
      || key === PACKAGE_ITEM_STATUS_KEYS.completed
      || jobHasMoaForm(job));
}

export function canOpenMoaForJob(job: JobRequestResponse, unit?: JobRequestUnitDto): boolean {
  const scoped = scopeJobForUnit(job, unit);
  if (unitHasMoaForm(scoped, unit))
    return true;
  if (canOpenMoaForm(scoped))
    return true;
  if (scoped.linkedFormKind === 'MOA' && Boolean(scoped.linkedFormId))
    return true;
  if (scoped.taskType !== 'Service')
    return false;
  const handoff = scoped.internalHandoffStatus ?? '';
  return isMoaViewableHandoff(handoff) || isExecutingStatusKey(scoped.displayStatusKey);
}

/** Job- or unit-level handoff for multi-session package lines. */
export function resolveActiveHandoff(
  job: JobRequestResponse,
  unitNumber?: number,
): string {
  if (unitNumber != null && (job.totalQty ?? 1) > 1) {
    const unit = job.units?.find((u) => u.unitNumber === unitNumber);
    return effectiveUnitHandoff(job, unit);
  }
  return job.internalHandoffStatus ?? '';
}

export function effectiveUnitHandoff(
  job: JobRequestResponse,
  unit?: JobRequestUnitDto,
): string {
  if ((job.totalQty ?? 1) > 1) {
    return unit?.internalHandoffStatus || job.internalHandoffStatus || '';
  }
  if (unit?.internalHandoffStatus) return unit.internalHandoffStatus;
  return job.internalHandoffStatus ?? '';
}

export function isUserAssignedToJob(job: JobRequestResponse, userId: number): boolean {
  const units = job.units?.length ? job.units : jobUnitsForAssignment(job);
  return units.some(
    (unit) => unit.assignedUserId === userId
      || unit.assignees?.some((assignee) => assignee.userId === userId),
  );
}

export function jobHasMoaClientCirculation(job: JobRequestResponse): boolean {
  const units = job.units?.length ? job.units : jobUnitsForAssignment(job);
  if ((job.totalQty ?? 1) > 1) {
    return units.some((unit) => effectiveUnitHandoff(job, unit) === 'MoaCirculation');
  }
  return (job.internalHandoffStatus ?? '') === 'MoaCirculation';
}

export function isExecutingHandoff(handoff: string): boolean {
  return handoff === 'PendingExecute' || handoff === 'ExecutionSecComplete';
}

export function isExecutingStatusKey(key?: string): boolean {
  return key === PACKAGE_ITEM_STATUS_KEYS.execution
    || key === PACKAGE_ITEM_STATUS_KEYS.pendingExecute;
}

export function unitIsExecuting(job: JobRequestResponse, unit: JobRequestUnitDto): boolean {
  return isExecutingHandoff(effectiveUnitHandoff(job, unit))
    || isExecutingStatusKey(unit.displayStatusKey);
}

export function jobHasExecutionReady(job: JobRequestResponse): boolean {
  if (job.status === 'Completed') return false;
  const units = job.units?.length ? job.units : jobUnitsForAssignment(job);
  if (isExecutingStatusKey(job.displayStatusKey)) return true;
  if ((job.totalQty ?? 1) > 1) {
    return units.some((unit) => unitIsExecuting(job, unit));
  }
  return isExecutingHandoff(job.internalHandoffStatus ?? '')
    || units.some((unit) => unitIsExecuting(job, unit));
}

export function executingUnitsForJob(job: JobRequestResponse): JobRequestUnitDto[] {
  const units = job.units?.length ? job.units : jobUnitsForAssignment(job);
  return units.filter((unit) => unitIsExecuting(job, unit) && unit.status !== 'Completed');
}

export function canMarkExecutionComplete(
  job: JobRequestResponse,
  unit: JobRequestUnitDto,
  user: { isAdmin?: boolean; canApproveMoa?: boolean },
): boolean {
  if (unit.status === 'Completed') return false;
  return unitIsExecuting(job, unit)
    && Boolean(user.isAdmin || user.canApproveMoa);
}

export function canShowUnitDoneButton(
  job: JobRequestResponse,
  unit: JobRequestUnitDto,
  user: { isAdmin?: boolean; canApproveMoa?: boolean },
): boolean {
  if (unit.status === 'Completed') return false;
  if (canMarkExecutionComplete(job, unit, user)) return true;
  if (job.taskType === 'Service' || job.taskType === 'MOI' || job.taskType === 'MOI Approval' || job.taskType === 'MOA') {
    return false;
  }
  return true;
}

export function unitAwaitingHeadExecutionSignoff(
  _job: JobRequestResponse,
  _unit: JobRequestUnitDto,
): boolean {
  return false;
}

export function trackerItemAwaitingHeadExecutionSignoff(_item: WorkTrackerItemDto): boolean {
  return false;
}

export function canTrackerRejectExecutionSignoff(
  _item: WorkTrackerItemDto,
  _options: { canApproveMoa?: boolean; isAdmin?: boolean },
): boolean {
  return false;
}

export function jobNeedsUserAttention(
  job: JobRequestResponse,
  user: Pick<UserResponse, 'userId' | 'role' | 'canApproveMoiIntake' | 'canApproveMoa'>,
  allJobs: JobRequestResponse[],
  canManageAssignments: boolean,
): boolean {
  const unitAwaitingIntake = job.units?.some((u) => u.awaitingIntakeApproval) ?? false;
  if ((job.awaitingIntakeApproval || unitAwaitingIntake) && user.canApproveMoiIntake)
    return true;
  if (jobHasMoaClientCirculation(job) && user.canApproveMoa)
    return true;
  if (job.internalHandoffStatus === 'AdminReview' && user.canApproveMoa)
    return true;
  if (job.internalHandoffStatus === 'MoaSharonApproved'
    && (user.canApproveMoa || user.role === 'Admin'))
    return true;
  if (jobHasExecutionReady(job) && (user.canApproveMoa || user.role === 'Admin'))
    return true;
  if (canAssignSecretarialTeam(job, allJobs) && canManageAssignments)
    return true;
  return false;
}

export function jobHasExecutionPendingApproval(_job: JobRequestResponse): boolean {
  return false;
}

export function isTrackerItemAssigned(item: WorkTrackerItemDto, userId?: number): boolean {
  if (!userId) return false;
  return item.assignedUserId === userId
    || Boolean(item.assignees?.some((assignee) => assignee.userId === userId));
}

export function canTrackerMarkExecutionDone(
  item: WorkTrackerItemDto,
  options: { userId?: number; canApproveMoa?: boolean; isAdmin?: boolean },
): boolean {
  return (isExecutingStatusKey(item.displayStatusKey)
    || isExecutingHandoff(item.internalHandoffStatus ?? ''))
    && Boolean(options.isAdmin || options.canApproveMoa);
}

export function trackerExecutionDoneLabel(_item: WorkTrackerItemDto): string {
  return 'Done';
}

export function moaAttentionUnit(job: JobRequestResponse): JobRequestUnitDto | undefined {
  const units = job.units?.length ? job.units : jobUnitsForAssignment(job);
  return units.find((unit) => effectiveUnitHandoff(job, unit) === 'MoaCirculation');
}
