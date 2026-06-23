import { AlertCircle, CalendarDays } from 'lucide-react';
import { useCallback, useEffect, useRef, useState } from 'react';
import { WorkQueueDragHandle } from './WorkQueueDragHandle';
import { WorkQueueOrderHint } from './WorkQueueOrderHint';
import { useWorkQueueOrder } from '@/hooks/useWorkQueueOrder';
import {
  ApiError,
  getMyWorkTracker,
  type WorkTrackerItemDto,
} from '@/lib/api';
import { packageItemStatusBadgeClass, PACKAGE_ITEM_STATUS_KEYS, canTrackerMarkExecutionDone, isExecutingStatusKey } from '@/lib/packageItemStatus';
import { formatDateDisplay } from '@/lib/dates';
import { formatQueueDate, parseQueueSortDate } from '@/lib/workQueueOrder';

interface MyWorkTrackerProps {
  refreshKey?: number;
  userId?: number;
  canApproveMoa?: boolean;
  isAdmin?: boolean;
  onMarkExecutionComplete?: (jobId: number, unitNumber: number) => void | Promise<void>;
  onOpenTask?: (jobId: number, taskType: string, unitNumber?: number) => void;
  onViewMoi?: (jobId: number, unitNumber: number, moiFormId: number) => void;
  onError: (message: string) => void;
  onSuccess: (message?: string) => void;
}

function trackerKey(item: WorkTrackerItemDto): string {
  return `unit-${item.unitId}`;
}

function trackerSortDate(item: WorkTrackerItemDto): number {
  return parseQueueSortDate(item.scheduledDate, item.dateRequested);
}

function trackerDisplayDate(item: WorkTrackerItemDto): string {
  return formatQueueDate(item.scheduledDate, item.dateRequested);
}

function trackerTaskLabel(item: WorkTrackerItemDto): string {
  const service = item.taskType === 'Service' ? item.service : item.taskType;
  const canOpenMoa = item.linkedFormKind === 'MOA'
    || (item.hasMoaForm && item.taskType === 'Service')
    || (item.taskType === 'Service' && isExecutingStatusKey(item.displayStatusKey));
  if (canOpenMoa) {
    return `${service} · MOA`;
  }
  if (item.linkedFormKind === 'MOI' || item.hasMoiForm) {
    return `${service} · MOI`;
  }
  return service;
}

function canOpenTrackerForm(item: WorkTrackerItemDto): boolean {
  if (item.linkedFormKind === 'MOA' && item.linkedFormId) return true;
  if (item.linkedFormKind === 'MOI' && item.linkedFormId) return true;
  if (item.hasMoaForm && item.taskType === 'Service') return true;
  if (item.taskType === 'Service' && isExecutingStatusKey(item.displayStatusKey)) return true;
  return item.hasMoiForm;
}

function trackerActionNotice(item: WorkTrackerItemDto): string | null {
  if (item.displayStatusKey === PACKAGE_ITEM_STATUS_KEYS.moaCirculation) {
    return 'Client has signed MOA — awaiting remaining client signatories.';
  }
  if (item.displayStatusKey === PACKAGE_ITEM_STATUS_KEYS.execution
    || item.displayStatusKey === PACKAGE_ITEM_STATUS_KEYS.pendingExecute) {
    return 'MOA signed — mark completed when execution is done.';
  }
  return null;
}

function trackerNeedsAction(item: WorkTrackerItemDto): boolean {
  return item.displayStatusKey === PACKAGE_ITEM_STATUS_KEYS.moaCirculation
    || item.displayStatusKey === PACKAGE_ITEM_STATUS_KEYS.execution
    || item.displayStatusKey === PACKAGE_ITEM_STATUS_KEYS.pendingExecute;
}

function trackerDueDate(item: WorkTrackerItemDto): string {
  return formatDateDisplay(item.requiredExecutionDate) || '—';
}

export function MyWorkTracker({
  refreshKey = 0,
  canApproveMoa,
  isAdmin,
  onMarkExecutionComplete,
  onOpenTask,
  onViewMoi,
  onError,
  onSuccess,
}: MyWorkTrackerProps) {
  const [items, setItems] = useState<WorkTrackerItemDto[]>([]);
  const [loading, setLoading] = useState(true);
  const hasLoaded = useRef(false);
  const [draggingKey, setDraggingKey] = useState<string | null>(null);
  const [dropTargetKey, setDropTargetKey] = useState<string | null>(null);

  const load = useCallback(async (silent = false) => {
    if (!silent) setLoading(true);
    try {
      setItems(await getMyWorkTracker());
      hasLoaded.current = true;
    } catch (err) {
      const message = err instanceof ApiError
        ? err.message
        : err instanceof DOMException && err.name === 'AbortError'
          ? 'Tracker request timed out. Please refresh or try again.'
          : 'Failed to load your tracker.';
      onError(message);
      setItems([]);
    } finally {
      if (!silent) setLoading(false);
    }
  }, [onError]);

  useEffect(() => {
    void load(hasLoaded.current);
  }, [load, refreshKey]);

  useEffect(() => {
    const timer = window.setInterval(() => void load(true), 30000);
    return () => window.clearInterval(timer);
  }, [load]);

  const {
    sortedItems: orderedItems,
    moveItem,
    resetOrder,
    hasCustomOrder,
  } = useWorkQueueOrder(undefined, 'tracker', items, trackerKey, trackerSortDate);

  const handleDrop = (targetKey: string) => {
    if (draggingKey) moveItem(draggingKey, targetKey);
    setDraggingKey(null);
    setDropTargetKey(null);
  };

  const actionItems = orderedItems.filter(trackerNeedsAction);

  return (
    <div className="bg-card rounded-lg border border-border overflow-hidden">
      <div className="p-4 border-b border-border">
        <div className="flex items-center gap-2">
          <CalendarDays className="w-5 h-5 text-muted-foreground" />
          <h2>My work tracker</h2>
        </div>
        <p className="text-xs text-muted-foreground mt-1">
          Click a task name to open the MOA (or MOI while client sign-off is still in progress). Use MOI to review the source instruction and supporting documents.
        </p>
        {orderedItems.length > 0 && (
          <div className="mt-2">
            <WorkQueueOrderHint hasCustomOrder={hasCustomOrder} onReset={resetOrder} />
          </div>
        )}
        {actionItems.length > 0 && (
          <div className="mt-3 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2.5 text-xs text-amber-950">
            <div className="flex items-start gap-2">
              <AlertCircle className="mt-0.5 h-4 w-4 shrink-0 text-amber-700" />
              <div>
                <p className="font-medium">
                  {`${actionItems.length} item${actionItems.length === 1 ? '' : 's'} with client MOA activity`}
                </p>
                <p className="mt-0.5 text-amber-900/80">
                  A client signatory has signed — remaining client approvals may still be pending.
                </p>
              </div>
            </div>
          </div>
        )}
      </div>
      {loading ? (
        <p className="p-6 text-sm text-muted-foreground">Loading...</p>
      ) : orderedItems.length === 0 ? (
        <p className="p-6 text-sm text-muted-foreground">No assigned work items yet.</p>
      ) : (
        <div className="overflow-auto">
          <table className="w-full text-sm">
            <thead className="bg-muted/50">
              <tr>
                <th className="px-2 py-3 w-8" aria-label="Reorder" />
                <th className="px-4 py-3 text-left">Scheduled</th>
                <th className="px-4 py-3 text-left">Due</th>
                <th className="px-4 py-3 text-left">Customer</th>
                <th className="px-4 py-3 text-left">Task</th>
                <th className="px-4 py-3 text-left">Unit</th>
                <th className="px-4 py-3 text-left">Team</th>
                <th className="px-4 py-3 text-center">Status</th>
                <th className="px-4 py-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody>
              {orderedItems.map((item) => {
                const key = trackerKey(item);
                const label = trackerTaskLabel(item);
                const openable = canOpenTrackerForm(item) && onOpenTask;
                const isDragging = draggingKey === key;
                const isDropTarget = dropTargetKey === key && draggingKey !== key;
                const teamLabel = item.assignees?.length
                  ? item.assignees.map((a) => a.userName).join(', ')
                  : item.assignedUserName || '—';
                const statusLabel = item.displayStatus || item.status;
                const statusKey = item.displayStatusKey || item.status;
                const actionNotice = trackerActionNotice(item);
                const needsAction = trackerNeedsAction(item);
                const canComplete = canTrackerMarkExecutionDone(item, { canApproveMoa, isAdmin });
                return (
                  <tr
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
                    className={`border-t border-border transition-colors ${
                      isDragging ? 'opacity-50 bg-muted/40' : ''
                    } ${isDropTarget ? 'bg-primary/5 ring-1 ring-inset ring-primary/30' : ''} ${
                      needsAction ? 'bg-amber-50/60' : ''
                    }`}
                  >
                    <td className="px-2 py-3">
                      <WorkQueueDragHandle />
                    </td>
                    <td className="px-4 py-3 tabular-nums text-muted-foreground whitespace-nowrap">
                      {trackerDisplayDate(item)}
                    </td>
                    <td className="px-4 py-3 tabular-nums text-muted-foreground whitespace-nowrap">
                      {trackerDueDate(item)}
                    </td>
                    <td className="px-4 py-3 font-medium">{item.customer}</td>
                    <td className="px-4 py-3">
                      {openable ? (
                        <button
                          type="button"
                          onClick={() => onOpenTask(item.jobId, item.taskType, item.unitNumber)}
                          className="text-primary hover:underline text-left"
                        >
                          {label}
                        </button>
                      ) : (
                        label
                      )}
                      {actionNotice && (
                        <p className="mt-1 text-[11px] text-amber-800">{actionNotice}</p>
                      )}
                    </td>
                    <td className="px-4 py-3">#{item.unitNumber}</td>
                    <td className="px-4 py-3 text-muted-foreground">{teamLabel}</td>
                    <td className="px-4 py-3 text-center">
                      <span className={`px-2 py-0.5 rounded-full text-xs ${packageItemStatusBadgeClass(statusKey)}`}>
                        {statusLabel}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-right space-x-1 whitespace-nowrap">
                      {canComplete && onMarkExecutionComplete && (
                        <button
                          type="button"
                          onClick={() => void Promise.resolve(onMarkExecutionComplete(item.jobId, item.unitNumber))
                            .then(() => onSuccess?.('Package line marked completed.'))
                            .catch((err) => onError(err instanceof ApiError ? err.message : 'Failed to mark completed.'))}
                          className="inline-flex items-center gap-1 px-2 py-1 text-xs bg-green-600 text-white rounded hover:bg-green-700"
                        >
                          Complete
                        </button>
                      )}
                      {item.moiFormId && onViewMoi && (
                        <button
                          type="button"
                          onClick={() => onViewMoi(item.jobId, item.unitNumber, item.moiFormId!)}
                          className="inline-flex items-center gap-1 px-2 py-1 text-xs border border-border rounded hover:bg-muted"
                        >
                          MOI
                        </button>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
