import { useCallback, useEffect, useMemo, useState } from 'react';
import { AlertCircle } from 'lucide-react';
import { AdminPackageOverview } from './AdminPackageOverview';
import { CompletedServicesTable } from './CompletedServicesTable';
import { MyWorkTracker } from './MyWorkTracker';
import { StatsCards } from './StatsCards';
import {
  ApiError,
  getJobRequests,
  type CustomerPackageDto,
  type CustomerResponse,
  type JobRequestResponse,
  type UserResponse,
} from '@/lib/api';
import { canAssignSecretarialTeam } from '@/lib/packageItemStatus';

interface AdminDashboardProps {
  refreshKey?: number;
  currentUser: UserResponse;
  onManagePackage: (customer: CustomerResponse, pkg: CustomerPackageDto) => void;
  onOpenTask: (jobId: number) => void;
  onViewHistory: () => void;
  onError: (message: string) => void;
  onSuccess: () => void;
}

function attentionLabel(job: JobRequestResponse): string {
  if (job.awaitingIntakeApproval)
    return 'MOI submitted — review intake';
  if (job.internalHandoffStatus === 'AdminReview')
    return 'MOA ready for head secretary review';
  if (job.internalHandoffStatus === 'MoaSharonApproved')
    return 'MOA approved — send to client';
  if (canAssignSecretarialTeam(job, []))
    return 'Assign secretarial team';
  return 'Needs attention';
}

export function AdminDashboard({
  refreshKey = 0,
  currentUser,
  onManagePackage,
  onOpenTask,
  onViewHistory,
  onError,
  onSuccess,
}: AdminDashboardProps) {
  const [attentionJobs, setAttentionJobs] = useState<JobRequestResponse[]>([]);
  const [loadingAttention, setLoadingAttention] = useState(true);

  const loadAttention = useCallback(async () => {
    setLoadingAttention(true);
    try {
      const jobs = await getJobRequests();
      const filtered = jobs.filter((job) => {
        if (job.awaitingIntakeApproval && currentUser.canApproveMoiIntake)
          return true;
        if (job.internalHandoffStatus === 'AdminReview' && currentUser.canApproveMoa)
          return true;
        if (job.internalHandoffStatus === 'MoaSharonApproved'
          && (currentUser.canApproveMoa || currentUser.role === 'Admin'))
          return true;
        if (canAssignSecretarialTeam(job, jobs) && currentUser.role === 'Admin')
          return true;
        return false;
      });
      setAttentionJobs(filtered.slice(0, 12));
    } catch (err) {
      onError(err instanceof ApiError ? err.message : 'Failed to load action queue.');
      setAttentionJobs([]);
    } finally {
      setLoadingAttention(false);
    }
  }, [currentUser, onError]);

  useEffect(() => {
    void loadAttention();
  }, [loadAttention, refreshKey]);

  const attentionSummary = useMemo(() => {
    if (loadingAttention) return 'Loading…';
    if (attentionJobs.length === 0) return 'No items waiting on you right now.';
    return `${attentionJobs.length} item${attentionJobs.length === 1 ? '' : 's'} need your action`;
  }, [attentionJobs.length, loadingAttention]);

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
          <div>
            <h3 className="font-medium">Needs your attention</h3>
            <p className="text-xs text-muted-foreground">{attentionSummary}</p>
          </div>
        </div>
        {attentionJobs.length === 0 ? (
          <p className="p-6 text-sm text-muted-foreground">
            {loadingAttention ? 'Loading action queue…' : 'You are all caught up on intake, MOA review, and assignments.'}
          </p>
        ) : (
          <ul className="divide-y divide-border">
            {attentionJobs.map((job) => (
              <li key={job.id} className="flex items-center justify-between gap-4 px-4 py-3 text-sm">
                <div>
                  <p className="font-medium">{job.customer} — {job.taskType === 'Service' ? job.service : job.taskType}</p>
                  <p className="text-xs text-muted-foreground mt-0.5">{attentionLabel(job)}</p>
                </div>
                <button
                  type="button"
                  onClick={() => onOpenTask(job.id)}
                  className="shrink-0 px-3 py-1.5 text-xs border border-border rounded-lg hover:bg-muted"
                >
                  Open
                </button>
              </li>
            ))}
          </ul>
        )}
      </section>

      <MyWorkTracker
        refreshKey={refreshKey}
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
