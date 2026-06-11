import { useCallback, useEffect, useMemo, useState } from 'react';
import { AlertCircle } from 'lucide-react';
import { AdminPackageOverview } from './AdminPackageOverview';
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
  type CustomerPackageDto,
  type CustomerResponse,
  type JobRequestResponse,
  type UserResponse,
} from '@/lib/api';
import {
  canAssignSecretarialTeam,
  jobUnitsForAssignment,
} from '@/lib/packageItemStatus';
import { canAssignJobStaff } from '@/lib/roles';
import { formatQueueDate, parseQueueSortDate } from '@/lib/workQueueOrder';

interface AdminDashboardProps {
  refreshKey?: number;
  currentUser: UserResponse;
  assignableUsers: { id: number; name: string }[];
  onManagePackage: (customer: CustomerResponse, pkg: CustomerPackageDto) => void;
  onOpenTask: (jobId: number) => void;
  onViewHistory: () => void;
  onError: (message: string) => void;
  onSuccess: () => void;
}

function attentionLabel(job: JobRequestResponse): string {
  if (job.awaitingIntakeApproval || job.units?.some((u) => u.awaitingIntakeApproval))
    return 'MOI submitted — review intake';
  if (job.internalHandoffStatus === 'AdminReview')
    return 'MOA ready for head secretary review';
  if (job.internalHandoffStatus === 'MoaSharonApproved')
    return 'MOA approved — send to client';
  if (canAssignSecretarialTeam(job, []))
    return 'Assign secretarial team';
  return 'Needs attention';
}

function attentionDate(job: JobRequestResponse): string {
  const awaitingUnit = job.units?.find((u) => u.awaitingIntakeApproval);
  return formatQueueDate(
    awaitingUnit?.scheduledDate,
    job.scheduledDate,
    job.dateRequested,
  );
}

function attentionSortDate(job: JobRequestResponse): number {
  const awaitingUnit = job.units?.find((u) => u.awaitingIntakeApproval);
  return parseQueueSortDate(
    awaitingUnit?.scheduledDate,
    job.scheduledDate,
    job.dateRequested,
  );
}

export function AdminDashboard({
  refreshKey = 0,
  currentUser,
  assignableUsers,
  onManagePackage,
  onOpenTask,
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

  const loadAttention = useCallback(async () => {
    setLoadingAttention(true);
    try {
      const jobs = await getJobRequests();
      setAllJobs(jobs);
      const filtered = jobs.filter((job) => {
        const unitAwaitingIntake = job.units?.some((u) => u.awaitingIntakeApproval) ?? false;
        if ((job.awaitingIntakeApproval || unitAwaitingIntake) && currentUser.canApproveMoiIntake)
          return true;
        if (job.internalHandoffStatus === 'AdminReview' && currentUser.canApproveMoa)
          return true;
        if (job.internalHandoffStatus === 'MoaSharonApproved'
          && (currentUser.canApproveMoa || currentUser.role === 'Admin'))
          return true;
        if (canAssignSecretarialTeam(job, jobs) && canManageAssignments)
          return true;
        return false;
      });
      setAttentionJobs(filtered);
    } catch (err) {
      onError(err instanceof ApiError ? err.message : 'Failed to load action queue.');
      setAllJobs([]);
      setAttentionJobs([]);
    } finally {
      setLoadingAttention(false);
    }
  }, [currentUser, canManageAssignments, onError]);

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

  const handleAssignUnit = async (
    jobId: number,
    unitNumber: number,
    userId: number,
    remove = false,
  ) => {
    try {
      const updated = await assignJobRequest(jobId, { userId, unitNumber, remove });
      setAllJobs((prev) => prev.map((j) => (j.id === updated.id ? updated : j)));
      setAttentionJobs((prev) => prev.map((j) => (j.id === updated.id ? updated : j)));
      onSuccess();
    } catch (err) {
      onError(err instanceof ApiError ? err.message : remove ? 'Failed to remove assignee.' : 'Failed to assign staff.');
    }
  };

  const resolveJob = (jobId: number) =>
    allJobs.find((j) => j.id === jobId) ?? attentionJobs.find((j) => j.id === jobId);

  return (
    <div className="space-y-8">
      <div>
        <h2 className="text-xl font-semibold">Operations</h2>
        <p className="text-sm text-muted-foreground mt-1">
          Your work queue, packages needing action, and company-wide package overview.
        </p>
      </div>

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
            {loadingAttention ? 'Loading action queue…' : 'You are all caught up on intake, MOA review, and assignments.'}
          </p>
        ) : (
          <ul className="divide-y divide-border">
            {orderedAttentionJobs.map((job) => {
              const key = getAttentionKey(job);
              const isDragging = draggingKey === key;
              const isDropTarget = dropTargetKey === key && draggingKey !== key;
              const liveJob = resolveJob(job.id) ?? job;
              const showAssignment = canManageAssignments && canAssignSecretarialTeam(liveJob, allJobs);
              const units = jobUnitsForAssignment(liveJob);
              const multiUnit = (liveJob.totalQty ?? 1) > 1;
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
                    <div className="w-24 shrink-0">
                      <p className="text-xs text-muted-foreground">Date</p>
                      <p className="font-medium tabular-nums">{attentionDate(liveJob)}</p>
                    </div>
                    <div className="flex-1 min-w-0">
                      <p className="font-medium truncate">
                        {liveJob.customer} — {liveJob.taskType === 'Service' ? liveJob.service : liveJob.taskType}
                      </p>
                      <p className="text-xs text-muted-foreground mt-0.5">{attentionLabel(liveJob)}</p>
                      {showAssignment && (
                        <div className="mt-3 space-y-2">
                          <p className="text-xs font-medium text-muted-foreground">Assign team</p>
                          {units.map((unit) => (
                            <div key={unit.unitNumber} className="flex flex-wrap items-center gap-2">
                              {multiUnit && (
                                <span className="text-xs text-muted-foreground w-8 shrink-0">#{unit.unitNumber}</span>
                              )}
                              <UserAssignCell
                                unit={unit}
                                users={assignableUsers}
                                onAdd={(userId) => void handleAssignUnit(liveJob.id, unit.unitNumber, userId)}
                                onRemove={(userId) => void handleAssignUnit(liveJob.id, unit.unitNumber, userId, true)}
                              />
                            </div>
                          ))}
                        </div>
                      )}
                    </div>
                    <button
                      type="button"
                      onClick={() => onOpenTask(liveJob.id)}
                      className="shrink-0 px-3 py-1.5 text-xs border border-border rounded-lg hover:bg-muted"
                    >
                      Open
                    </button>
                  </div>
                </li>
              );
            })}
          </ul>
        )}
      </section>

      <MyWorkTracker
        refreshKey={refreshKey}
        userId={currentUser.userId}
        onOpenTask={onOpenTask}
        onError={onError}
        onSuccess={onSuccess}
      />

      <AdminPackageOverview refreshKey={refreshKey} onManagePackage={onManagePackage} />

      <StatsCards refreshKey={refreshKey} />

      <CompletedServicesTable refreshKey={refreshKey} onViewHistory={onViewHistory} />
    </div>
  );
}
