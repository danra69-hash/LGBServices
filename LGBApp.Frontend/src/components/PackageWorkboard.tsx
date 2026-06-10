import { ArrowLeft, Check, ClipboardList, Undo2 } from 'lucide-react';
import { useCallback, useEffect, useRef, useState } from 'react';
import { DateInput } from './DateInput';
import { UserAssignCell } from './UserAssignCell';
import { formatDateDisplay, parseDateToIso } from '@/lib/dates';
import {
  ApiError,
  advanceJobHandoff,
  assignJobRequest,
  getJobRequests,
  recordJobProgress,
  type CustomerPackageDto,
  type CustomerResponse,
  type JobRequestResponse,
  type JobRequestUnitDto,
} from '@/lib/api';
import { handoffLabel } from '@/lib/handoff';

interface PackageWorkboardProps {
  customer: CustomerResponse;
  package: CustomerPackageDto;
  users: { id: number; name: string }[];
  refreshKey?: number;
  userIsAdmin?: boolean;
  onBack: () => void;
  onOpenTask: (job: JobRequestResponse) => void;
  onError: (message: string) => void;
  onSuccess: () => void;
  /** Lightweight refresh for tracking calendar only (avoids full-table reload). */
  onScheduleSaved?: () => void;
}

const FORM_TASKS = ['MOI', 'MOI Approval', 'MOA'];

function draftKey(jobId: number, unitNumber: number) {
  return `${jobId}-${unitNumber}`;
}

export function PackageWorkboard({
  customer,
  package: pkg,
  users,
  refreshKey = 0,
  userIsAdmin = false,
  onBack,
  onOpenTask,
  onError,
  onSuccess,
  onScheduleSaved,
}: PackageWorkboardProps) {
  const [items, setItems] = useState<JobRequestResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const hasLoaded = useRef(false);

  const loadItems = useCallback(async (options?: { silent?: boolean }) => {
    if (!options?.silent) setLoading(true);
    try {
      const data = await getJobRequests(pkg.id, true);
      setItems(data);
      hasLoaded.current = true;
    } catch (err) {
      onError(err instanceof ApiError ? err.message : 'Failed to load package work items.');
      setItems([]);
    } finally {
      if (!options?.silent) setLoading(false);
    }
  }, [pkg.id, onError]);

  useEffect(() => {
    void loadItems({ silent: hasLoaded.current });
  }, [loadItems, refreshKey]);

  const handleScheduleSave = useCallback(async (jobId: number, unitNumber: number, isoValue: string) => {
    const job = items.find((j) => j.id === jobId);
    if (!job) return;

    const unit = job.units?.find((u) => u.unitNumber === unitNumber);
    const savedIso = unit?.scheduledDate ? parseDateToIso(unit.scheduledDate) ?? '' : '';
    if (isoValue === savedIso) return;

    const rollback = job;
    const optimisticDisplay = isoValue ? formatDateDisplay(isoValue) : undefined;

    setItems((prev) =>
      prev.map((j) => {
        if (j.id !== jobId) return j;
        return {
          ...j,
          units: j.units?.map((u) =>
            u.unitNumber === unitNumber ? { ...u, scheduledDate: optimisticDisplay } : u,
          ),
        };
      }),
    );

    try {
      const updated = await recordJobProgress(jobId, {
        unitNumber,
        scheduledDate: isoValue,
      });
      setItems((prev) => prev.map((j) => (j.id === jobId ? updated : j)));
      onScheduleSaved?.();
    } catch (err) {
      setItems((prev) => prev.map((j) => (j.id === jobId ? rollback : j)));
      onError(err instanceof ApiError ? err.message : 'Failed to save date.');
    }
  }, [items, onError, onScheduleSaved]);

  const handleAssignUnit = async (job: JobRequestResponse, unitNumber: number, userId: number, remove = false) => {
    try {
      const updated = await assignJobRequest(job.id, { userId, unitNumber, remove });
      setItems((prev) => prev.map((j) => (j.id === updated.id ? updated : j)));
      onSuccess();
    } catch (err) {
      onError(err instanceof ApiError ? err.message : remove ? 'Failed to remove user.' : 'Failed to assign user.');
    }
  };

  const handleMarkUnit = async (job: JobRequestResponse, unitNumber: number) => {
    try {
      const updated = await recordJobProgress(job.id, { unitNumber, markUnitComplete: true });
      setItems((prev) => prev.map((j) => (j.id === updated.id ? updated : j)));
      onSuccess();
      onScheduleSaved?.();
    } catch (err) {
      onError(err instanceof ApiError ? err.message : 'Failed to update progress.');
    }
  };

  const handleRevertUnit = async (job: JobRequestResponse, unitNumber: number) => {
    try {
      const updated = await recordJobProgress(job.id, { unitNumber, markUnitIncomplete: true });
      setItems((prev) => prev.map((j) => (j.id === updated.id ? updated : j)));
      onSuccess();
      onScheduleSaved?.();
    } catch (err) {
      onError(err instanceof ApiError ? err.message : 'Failed to revert completion.');
    }
  };

  const renderUnitRow = (job: JobRequestResponse, unit: JobRequestUnitDto, label: string, isForm: boolean) => {
    const key = draftKey(job.id, unit.unitNumber);
    const canOpen = isForm && (unit.status === 'In Progress' || unit.status === 'Pending');
    const showUnitLabel = (job.totalQty ?? 1) > 1;

    return (
      <tr key={key} className="border-t border-border bg-muted/10">
        <td className="px-4 py-2 pl-8">
          {showUnitLabel && <span className="text-xs text-muted-foreground mr-2">#{unit.unitNumber}</span>}
          {canOpen ? (
            <button type="button" onClick={() => onOpenTask(job)} className="text-primary hover:underline font-medium text-left">
              {label}
              {job.linkedFormId && (
                <span className="ml-2 text-xs font-normal text-muted-foreground">
                  ({job.linkedFormKind ?? 'Form'} #{job.linkedFormId})
                </span>
              )}
            </button>
          ) : (
            <span className="font-medium">
              {label}
              {job.linkedFormId && (
                <span className="ml-2 text-xs font-normal text-muted-foreground">
                  ({job.linkedFormKind ?? 'Form'} #{job.linkedFormId})
                </span>
              )}
            </span>
          )}
        </td>
        <td className="px-4 py-2 text-sm">
          {isForm ? (
            <div>
              <div>{job.accountHolder || '—'}</div>
              {(job.accountHolderEmail || job.accountHolderPhone) && (
                <div className="text-xs text-muted-foreground">
                  {[job.accountHolderEmail, job.accountHolderPhone].filter(Boolean).join(' · ')}
                </div>
              )}
            </div>
          ) : (
            '—'
          )}
        </td>
        <td className="px-4 py-2 text-center text-xs text-muted-foreground">
          {unit.status === 'Completed' ? '✓' : '—'}
        </td>
        <td className="px-4 py-2">
          <DateInput
            value={unit.scheduledDate}
            onChange={(iso) => void handleScheduleSave(job.id, unit.unitNumber, iso)}
          />
        </td>
        <td className="px-4 py-2">
          <UserAssignCell
            unit={unit}
            users={users}
            onAdd={(userId) => void handleAssignUnit(job, unit.unitNumber, userId)}
            onRemove={(userId) => void handleAssignUnit(job, unit.unitNumber, userId, true)}
          />
        </td>
        <td className="px-4 py-2 text-center">
          <span className={`px-2 py-0.5 rounded-full text-xs ${
            unit.status === 'Completed' ? 'bg-green-100 text-green-800'
              : unit.status === 'In Progress' ? 'bg-blue-100 text-blue-800'
              : 'bg-yellow-100 text-yellow-800'
          }`}>
            {unit.status}
          </span>
        </td>
        <td className="px-4 py-2 text-xs text-muted-foreground">
          {handoffLabel(job.internalHandoffStatus)}
          {userIsAdmin && job.internalHandoffStatus === 'AdminReview' && (
            <button
              type="button"
              className="ml-2 text-primary hover:underline"
              onClick={() =>
                void advanceJobHandoff(job.id, 'approve-for-moa')
                  .then(() => onSuccess())
                  .catch((err) => onError(err instanceof ApiError ? err.message : 'Handoff failed.'))
              }
            >
              Approve for MOA
            </button>
          )}
        </td>
        <td className="px-4 py-2 text-right">
          {unit.status === 'Completed' ? (
            <button
              type="button"
              onClick={() => handleRevertUnit(job, unit.unitNumber)}
              className="inline-flex items-center gap-1 px-2 py-1 text-xs border border-border rounded hover:bg-muted"
              title="Mark as not done"
            >
              <Undo2 className="w-3 h-3" />
              Undo
            </button>
          ) : (
            <button
              type="button"
              onClick={() => handleMarkUnit(job, unit.unitNumber)}
              className="inline-flex items-center gap-1 px-2 py-1 text-xs bg-green-600 text-white rounded hover:bg-green-700"
            >
              <Check className="w-3 h-3" />
              Done
            </button>
          )}
        </td>
      </tr>
    );
  };

  const renderJob = (job: JobRequestResponse) => {
    const isForm = FORM_TASKS.includes(job.taskType);
    const label = isForm ? job.taskType : job.service;
    const units = job.units?.length
      ? job.units
      : [{ id: 0, unitNumber: 1, assignedUserName: job.jobAssignedTo, status: job.status, scheduledDate: job.scheduledDate } as JobRequestUnitDto];

    if (job.totalQty > 1) {
      return (
        <>
          <tr key={`summary-${job.id}`} className="border-t border-border bg-muted/20">
            <td className="px-4 py-3 font-semibold" colSpan={2}>
              {label}
              <span className="ml-2 text-xs font-normal text-muted-foreground">
                {job.usedQty}/{job.totalQty} done
              </span>
            </td>
            <td className="px-4 py-3 text-center font-medium">{job.usedQty}/{job.totalQty}</td>
            <td className="px-4 py-3 text-xs text-muted-foreground" colSpan={5}>
              Assign users and dates per unit below
            </td>
          </tr>
          {units.map((unit) => renderUnitRow(job, unit, label, isForm))}
        </>
      );
    }

    return renderUnitRow(job, units[0], label, isForm);
  };

  const serviceItems = items.filter((j) => !FORM_TASKS.includes(j.taskType));
  const formItems = items.filter((j) => FORM_TASKS.includes(j.taskType));

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <button type="button" onClick={onBack} className="p-2 border border-border rounded-lg hover:bg-muted">
          <ArrowLeft className="w-4 h-4" />
        </button>
        <div>
          <h2 className="text-lg font-semibold">{customer.company}</h2>
          <p className="text-sm text-muted-foreground">
            {pkg.packageName} · {pkg.validity || '1 Year'} · Active value MYR{' '}
            {(pkg.activeValue ?? pkg.packageValue ?? 0).toLocaleString('en-MY', { minimumFractionDigits: 2 })}
          </p>
        </div>
      </div>

      <div className="bg-card rounded-lg border border-border overflow-hidden">
        <div className="p-4 border-b border-border flex items-center gap-2">
          <ClipboardList className="w-5 h-5 text-muted-foreground" />
          <h3>Package deliverables</h3>
        </div>
        {loading ? (
          <p className="p-6 text-muted-foreground text-sm">Loading...</p>
        ) : (
          <div className="overflow-auto">
            <table className="w-full text-sm">
              <thead className="bg-muted/50">
                <tr>
                  <th className="px-4 py-3 text-left">Component</th>
                  <th className="px-4 py-3 text-left">Send to (signer)</th>
                  <th className="px-4 py-3 text-center">Qty done</th>
                  <th className="px-4 py-3 text-left">Scheduled date</th>
                  <th className="px-4 py-3 text-left">Users</th>
                  <th className="px-4 py-3 text-center">Status</th>
                  <th className="px-4 py-3 text-left">Handoff</th>
                  <th className="px-4 py-3 text-right">Actions</th>
                </tr>
              </thead>
              <tbody>
                {serviceItems.length === 0 && formItems.length === 0 ? (
                  <tr>
                    <td colSpan={8} className="px-4 py-8 text-center text-muted-foreground">
                      No work items yet. Re-save the customer to sync from the product catalog.
                    </td>
                  </tr>
                ) : (
                  <>
                    {serviceItems.map(renderJob)}
                    {formItems.length > 0 && serviceItems.length > 0 && (
                      <tr className="bg-muted/20">
                        <td colSpan={8} className="px-4 py-2 text-xs font-medium text-muted-foreground uppercase tracking-wide">
                          Forms (client signers)
                        </td>
                      </tr>
                    )}
                    {formItems.map(renderJob)}
                  </>
                )}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
