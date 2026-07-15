import { useCallback, useEffect, useMemo, useState } from 'react';
import { AlertCircle } from 'lucide-react';
import { CompletedServicesTable } from './CompletedServicesTable';
import { MyWorkTracker } from './MyWorkTracker';
import { StatsCards } from './StatsCards';
import { UserAssignCell } from './UserAssignCell';
import { WorkQueueDragHandle } from './WorkQueueDragHandle';
import { WorkQueueOrderHint } from './WorkQueueOrderHint';
import { useWorkQueueOrder } from '@/hooks/useWorkQueueOrder';
import {
  ApiError,
  assignJobRequest,
  getJobRequests,
  recordJobProgress,
  type JobRequestResponse,
  type UserResponse,
} from '@/lib/api';
import {
  canAssignSecretarialTeam,
  assignableUnitsForJob,
  jobUnitsForAssignment,
  jobHasMoaClientCirculation,
  jobHasExecutionReady,
  canMarkExecutionComplete,
  executingUnitsForJob,
  isUserAssignedToJob,
  moaAttentionUnit,
  jobNeedsUserAttention,
} from '@/lib/packageItemStatus';
import { canAssignJobStaff } from '@/lib/roles';
import { formatDateDisplay } from '@/lib/dates';
import { formatQueueDate, parseQueueSortDate } from '@/lib/workQueueOrder';
import { jobDisplayTitle } from '@/lib/jobDisplayTitle';

interface AdminDashboardProps {
  refreshKey?: number;
  currentUser: UserResponse;
  assignableUsers: { id: number; name: string }[];
  secTeamUsers: { id: number; name: string }[];
  onOpenTask: (jobId: number, unitNumber?: number) => void;
  onViewMoi?: (jobId: number, unitNumber: number, moiFormId: number) => void;
  onViewHistory: () => void;
  onError: (message: string) => void;
  onSuccess: (message?: string) => void;
}

function attentionLabel(job: JobRequestResponse): string {
  if (job.awaitingIntakeApproval || job.units?.some((u) => u.awaitingIntakeApproval))
    return 'MOI submitted — review intake';
  if (
    job.internalHandoffStatus === 'AdminBypass'
    || job.workflowMode === 'AdminBypass'
    || job.units?.some((u) => u.internalHandoffStatus === 'AdminBypass')
  ) {
    const note = job.adminBypassNote
      || job.units?.find((u) => u.adminBypassNote)?.adminBypassNote
      || '';
    return note
      ? `Client request (no MOI/MOA): ${note.length > 80 ? `${note.slice(0, 80)}…` : note}`
      : 'Client request (no MOI/MOA) — open to action';
  }
  if (jobHasMoaClientCirculation(job))
    return 'Client signed MOA — awaiting remaining signatories';
  if (job.internalHandoffStatus === 'AdminReview')
    return 'MOA draft submitted — review for client release';
  if (job.internalHandoffStatus === 'ResoInProgress' || job.internalHandoffStatus === 'PendingPrep')
    return 'MOA draft in progress — assign team or open to complete';
  if (job.internalHandoffStatus === 'MoaSharonApproved')
    return 'MOA approved — send to client';
  if (jobHasExecutionReady(job))
    return 'MOA signed — mark completed when execution is done';
  if (canAssignSecretarialTeam(job, []))
    return 'Resolution prep — assign or reassign team';
  return 'Needs attention';
}

function attentionContextUnit(job: JobRequestResponse) {
  const awaitingUnit = job.units?.find((u) => u.awaitingIntakeApproval);
  if (awaitingUnit) return awaitingUnit;
  return moaAttentionUnit(job)
    ?? job.units?.find((u) => u.unitNumber === 1)
    ?? job.units?.[0];
}

function attentionDate(job: JobRequestResponse): string {
  const unit = attentionContextUnit(job);
  return formatQueueDate(
    unit?.scheduledDate,
    job.scheduledDate,
    job.dateRequested,
  );
}

function attentionExecRequired(job: JobRequestResponse): string {
  const unit = attentionContextUnit(job);
  return formatDateDisplay(unit?.requiredExecutionDate) || '—';
}

function attentionSortDate(job: JobRequestResponse): number {
  const unit = attentionContextUnit(job);
  return parseQueueSortDate(
    unit?.scheduledDate,
    job.scheduledDate,
    job.dateRequested,
  );
}

export function AdminDashboard({
  refreshKey = 0,
  currentUser,
  assignableUsers,
  secTeamUsers,
  onOpenTask,
  onViewMoi,
  onViewHistory,
  onError,
  onSuccess,
}: AdminDashboardProps) {
  const [allJobs, setAllJobs] = useState<JobRequestResponse[]>([]);
  const [attentionJobs, setAttentionJobs] = useState<JobRequestResponse[]>([]);
  const [loadingAttention, setLoadingAttention] = useState(true);
  const [draggingKey, setDraggingKey] = useState<string | null>(null);
  const [dropTargetKey, setDropTargetKey] = useState<string | null>(null);

  const canManageAssignments = canAssignJobStaff(currentUser);

  const filterAttentionJobs = useCallback((
    jobs: JobRequestResponse[],
  ) => jobs.filter((job) => jobNeedsUserAttention(
    job,
    currentUser,
    jobs,
    canManageAssignments,
  )), [currentUser, canManageAssignments]);

  const loadAttention = useCallback(async () => {
    setLoadingAttention(true);
    try {
      const jobs = await getJobRequests();
      setAllJobs(jobs);
      setAttentionJobs(filterAttentionJobs(jobs));
    } catch (err) {
      onError(err instanceof ApiError ? err.message : 'Failed to load action queue.');
      setAllJobs([]);
      setAttentionJobs([]);
    } finally {
      setLoadingAttention(false);
    }
  }, [filterAttentionJobs, onError]);

  useEffect(() => {
    void loadAttention();
  }, [loadAttention, refreshKey]);

  const getAttentionKey = useCallback((job: JobRequestResponse) => `job-${job.id}`, []);

  const {
    sortedItems: orderedAttentionJobs,
    moveItem,
    resetOrder,
    hasCustomOrder,
  } = useWorkQueueOrder(
    currentUser.userId,
    'attention',
    attentionJobs,
    getAttentionKey,
    attentionSortDate,
  );

  const attentionSummary = useMemo(() => {
    if (loadingAttention) return 'Loading…';
    if (orderedAttentionJobs.length === 0) return 'No items waiting on you right now.';
    return `${orderedAttentionJobs.length} item${orderedAttentionJobs.length === 1 ? '' : 's'} need your action`;
  }, [orderedAttentionJobs.length, loadingAttention]);

  const handleDrop = (targetKey: string) => {
    if (draggingKey) moveItem(draggingKey, targetKey);
    setDraggingKey(null);
    setDropTargetKey(null);
  };

  const applyJobUpdate = (updated: JobRequestResponse) => {
    setAllJobs((prev) => {
      const next = prev.map((j) => (j.id === updated.id ? updated : j));
      setAttentionJobs(filterAttentionJobs(next));
      return next;
    });
  };

  const handleAssignUnit = async (
    jobId: number,
    unitNumber: number,
    userId: number,
    remove = false,
  ) => {
    try {
      const updated = await assignJobRequest(jobId, { userId, unitNumber, remove });
      applyJobUpdate(updated);
      onSuccess();
    } catch (err) {
      onError(err instanceof ApiError ? err.message : remove ? 'Failed to remove assignee.' : 'Failed to assign staff.');
    }
  };

  const handleAssignMany = async (jobId: number, unitNumber: number, userIds: number[]) => {
    if (userIds.length === 0) return;
    try {
      let updated: JobRequestResponse | undefined;
      for (const userId of userIds) {
        updated = await assignJobRequest(jobId, { userId, unitNumber });
      }
      if (updated) applyJobUpdate(updated);
      onSuccess();
    } catch (err) {
      onError(err instanceof ApiError ? err.message : 'Failed to tag sec team.');
    }
  };

  const handleMarkExecutionComplete = async (jobId: number, unitNumber: number) => {
    try {
      const updated = await recordJobProgress(jobId, { unitNumber, markUnitComplete: true });
      applyJobUpdate(updated);
      onSuccess('Package line marked completed.');
    } catch (err) {
      onError(err instanceof ApiError ? err.message : 'Failed to mark completed.');
    }
  };

  const canCompleteExecution = Boolean(currentUser.canApproveMoa || currentUser.role === 'Admin');

  const executingJobs = useMemo(
    () => allJobs.filter((job) => jobHasExecutionReady(job)),
    [allJobs],
  );

  const resolveJob = (jobId: number) =>
    allJobs.find((j) => j.id === jobId) ?? attentionJobs.find((j) => j.id === jobId);

  return (
    <div className="space-y-8">
      <div>
        <h2 className="text-xl font-semibold">Operations</h2>
        <p className="text-sm text-muted-foreground mt-1">
          Your work queue and company-wide activity at a glance.
        </p>
      </div>

      <StatsCards refreshKey={refreshKey} />

      <section className="bg-card border border-border rounded-lg overflow-hidden">
        <div className="p-4 border-b border-border flex items-center gap-2">
          <AlertCircle className="w-5 h-5 text-amber-600" />
          <div className="flex-1">
            <h3 className="font-medium">Needs your attention</h3>
            <p className="text-xs text-muted-foreground">{attentionSummary}</p>
            {orderedAttentionJobs.length > 0 && (
              <WorkQueueOrderHint hasCustomOrder={hasCustomOrder} onReset={resetOrder} />
            )}
          </div>
        </div>
        {orderedAttentionJobs.length === 0 ? (
          <p className="p-6 text-sm text-muted-foreground">
            {loadingAttention ? 'Loading action queue…' : 'You are all caught up on intake, MOA review, client sign-off, and assignments.'}
          </p>
        ) : (
          <ul className="divide-y divide-border">
            {orderedAttentionJobs.map((job) => {
              const key = getAttentionKey(job);
              const isDragging = draggingKey === key;
              const isDropTarget = dropTargetKey === key && draggingKey !== key;
              const liveJob = resolveJob(job.id) ?? job;
              const showAssignment = canManageAssignments && canAssignSecretarialTeam(liveJob, allJobs);
              const units = assignableUnitsForJob(liveJob);
              const allUnits = jobUnitsForAssignment(liveJob);
              const multiUnit = (liveJob.totalQty ?? 1) > 1;
              const hiddenUnitCount = allUnits.length - units.length;
              return (
                <li
                  key={key}
                  draggable
                  onDragStart={() => setDraggingKey(key)}
                  onDragEnd={() => {
                    setDraggingKey(null);
                    setDropTargetKey(null);
                  }}
                  onDragOver={(e) => {
                    e.preventDefault();
                    setDropTargetKey(key);
                  }}
                  onDragLeave={() => {
                    if (dropTargetKey === key) setDropTargetKey(null);
                  }}
                  onDrop={(e) => {
                    e.preventDefault();
                    handleDrop(key);
                  }}
                  className={`px-4 py-3 text-sm transition-colors ${
                    isDragging ? 'opacity-50 bg-muted/40' : ''
                  } ${isDropTarget ? 'bg-primary/5 ring-1 ring-inset ring-primary/30' : ''}`}
                >
                  <div className="flex items-start gap-3">
                    <WorkQueueDragHandle />
                    <div className="w-28 shrink-0">
                      <p className="text-xs text-muted-foreground">Scheduled</p>
                      <p className="font-medium tabular-nums">{attentionDate(liveJob)}</p>
                      <p className="text-xs text-muted-foreground mt-2">Exec required</p>
                      <p className="font-medium tabular-nums">{attentionExecRequired(liveJob)}</p>
                    </div>
                    <div className="flex-1 min-w-0">
                      <p className="font-medium truncate">
                        {liveJob.customer} — {jobDisplayTitle(liveJob)}
                      </p>
                      <p className="text-xs text-muted-foreground mt-0.5">{attentionLabel(liveJob)}</p>
                      {showAssignment && (
                        <div className="mt-3 space-y-2 rounded-lg border border-slate-200/80 bg-slate-50/60 px-3 py-2.5">
                          <p className="text-xs font-medium text-slate-600">Assign secretarial team</p>
                          {hiddenUnitCount > 0 && (
                            <p className="text-[11px] text-muted-foreground">
                              Sessions not ready yet are hidden until the prior session&apos;s MOI is complete.
                            </p>
                          )}
                          {units.length === 0 ? (
                            <p className="text-xs text-muted-foreground">No sessions ready for assignment yet.</p>
                          ) : units.map((unit) => (
                            <div key={unit.unitNumber} className="flex flex-wrap items-center gap-2">
                              {multiUnit && (
                                <span className="text-xs text-muted-foreground w-20 shrink-0">
                                  #{unit.unitNumber}
                                  {unit.requiredExecutionDate && (
                                    <span className="block text-[10px]">Exec {formatDateDisplay(unit.requiredExecutionDate)}</span>
                                  )}
                                </span>
                              )}
                              <UserAssignCell
                                unit={unit}
                                users={assignableUsers}
                                secTeamUsers={secTeamUsers}
                                onAdd={(userId) => void handleAssignUnit(liveJob.id, unit.unitNumber, userId)}
                                onRemove={(userId) => void handleAssignUnit(liveJob.id, unit.unitNumber, userId, true)}
                                onAddMany={(userIds) => handleAssignMany(liveJob.id, unit.unitNumber, userIds)}
                              />
                            </div>
                          ))}
                        </div>
                      )}
                    </div>
                    <div className="flex flex-col items-end gap-2 shrink-0">
                    {canCompleteExecution && jobHasExecutionReady(liveJob) && allUnits
                      .filter((unit) => canMarkExecutionComplete(liveJob, unit, {
                        isAdmin: currentUser.role === 'Admin',
                        canApproveMoa: currentUser.canApproveMoa,
                      }))
                      .map((unit) => (
                        <button
                          key={`complete-${unit.unitNumber}`}
                          type="button"
                          onClick={() => void handleMarkExecutionComplete(liveJob.id, unit.unitNumber)}
                          className="px-3 py-1.5 text-xs bg-green-600 text-white rounded-lg hover:bg-green-700"
                        >
                          Mark completed{(liveJob.totalQty ?? 1) > 1 ? ` (#${unit.unitNumber})` : ''}
                        </button>
                      ))}
                    <button
                      type="button"
                      onClick={() => onOpenTask(liveJob.id)}
                      className="px-3 py-1.5 text-xs border border-border rounded-lg hover:bg-muted"
                    >
                      Open
                    </button>
                    </div>
                  </div>
                </li>
              );
            })}
          </ul>
        )}
      </section>

      {canCompleteExecution && executingJobs.length > 0 && (
        <section className="bg-card border border-emerald-200 rounded-lg overflow-hidden">
          <div className="p-4 border-b border-emerald-200 bg-emerald-50/60">
            <h3 className="font-medium text-emerald-950">Awaiting completion</h3>
            <p className="text-xs text-emerald-900/80 mt-1">
              MOA is fully signed. Mark each line completed once secretarial execution is done — it will leave the work tracker.
            </p>
          </div>
          <ul className="divide-y divide-border">
            {executingJobs.map((job) => {
              const units = executingUnitsForJob(job);
              return (
                <li key={job.id} className="px-4 py-3 text-sm flex flex-wrap items-center gap-3 justify-between">
                  <div className="min-w-0">
                    <p className="font-medium truncate">
                      {job.customer} — {jobDisplayTitle(job)}
                    </p>
                    <p className="text-xs text-muted-foreground mt-0.5">Status: Executing</p>
                  </div>
                  <div className="flex flex-wrap items-center gap-2">
                    {units.map((unit) => (
                      <button
                        key={unit.unitNumber}
                        type="button"
                        onClick={() => void handleMarkExecutionComplete(job.id, unit.unitNumber)}
                        className="px-3 py-1.5 text-xs bg-green-600 text-white rounded-lg hover:bg-green-700"
                      >
                        Mark completed{(job.totalQty ?? 1) > 1 ? ` (#${unit.unitNumber})` : ''}
                      </button>
                    ))}
                    {units.map((unit) => (
                      <button
                        key={`moa-${unit.unitNumber}`}
                        type="button"
                        onClick={() => onOpenTask(job.id, unit.unitNumber)}
                        className="px-3 py-1.5 text-xs border border-primary/30 text-primary rounded-lg hover:bg-primary/5"
                      >
                        View MOA{(job.totalQty ?? 1) > 1 ? ` (#${unit.unitNumber})` : ''}
                      </button>
                    ))}
                  </div>
                </li>
              );
            })}
          </ul>
        </section>
      )}

      <MyWorkTracker
        refreshKey={refreshKey}
        userId={currentUser.userId}
        canApproveMoa={currentUser.canApproveMoa}
        isAdmin={currentUser.role === 'Admin'}
        onMarkExecutionComplete={canCompleteExecution ? handleMarkExecutionComplete : undefined}
        onOpenTask={onOpenTask}
        onViewMoi={onViewMoi}
        onError={onError}
        onSuccess={onSuccess}
      />

      <CompletedServicesTable refreshKey={refreshKey} onViewHistory={onViewHistory} />
    </div>
  );
}
