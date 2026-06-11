import { CalendarDays, Check } from 'lucide-react';
import { useCallback, useEffect, useRef, useState } from 'react';
import {
  ApiError,
  getMyWorkTracker,
  recordJobProgress,
  type WorkTrackerItemDto,
} from '@/lib/api';

interface MyWorkTrackerProps {
  refreshKey?: number;
  onOpenTask?: (jobId: number, taskType: string, unitNumber?: number) => void;
  onError: (message: string) => void;
  onSuccess: () => void;
}

export function MyWorkTracker({ refreshKey = 0, onOpenTask, onError, onSuccess }: MyWorkTrackerProps) {
  const [items, setItems] = useState<WorkTrackerItemDto[]>([]);
  const [loading, setLoading] = useState(true);
  const hasLoaded = useRef(false);

  const load = useCallback(async (silent = false) => {
    if (!silent) setLoading(true);
    try {
      setItems(await getMyWorkTracker());
      hasLoaded.current = true;
    } catch (err) {
      onError(err instanceof ApiError ? err.message : 'Failed to load your tracker.');
      setItems([]);
    } finally {
      if (!silent) setLoading(false);
    }
  }, [onError]);

  useEffect(() => {
    void load(!hasLoaded.current);
  }, [load, refreshKey]);

  useEffect(() => {
    const timer = window.setInterval(() => void load(true), 30000);
    return () => window.clearInterval(timer);
  }, [load]);

  const handleComplete = async (item: WorkTrackerItemDto) => {
    try {
      await recordJobProgress(item.jobId, {
        unitNumber: item.unitNumber,
        markUnitComplete: true,
      });
      onSuccess();
      await load(true);
    } catch (err) {
      onError(err instanceof ApiError ? err.message : 'Failed to mark complete.');
    }
  };

  return (
    <div className="bg-card rounded-lg border border-border overflow-hidden">
      <div className="p-4 border-b border-border flex items-center gap-2">
        <CalendarDays className="w-5 h-5 text-muted-foreground" />
        <h2>My work tracker</h2>
      </div>
      {loading ? (
        <p className="p-6 text-sm text-muted-foreground">Loading...</p>
      ) : items.length === 0 ? (
        <p className="p-6 text-sm text-muted-foreground">No assigned work items yet.</p>
      ) : (
        <div className="overflow-auto">
          <table className="w-full text-sm">
            <thead className="bg-muted/50">
              <tr>
                <th className="px-4 py-3 text-left">Customer</th>
                <th className="px-4 py-3 text-left">Task</th>
                <th className="px-4 py-3 text-left">Unit</th>
                <th className="px-4 py-3 text-left">Send to</th>
                <th className="px-4 py-3 text-left">Scheduled</th>
                <th className="px-4 py-3 text-center">Status</th>
                <th className="px-4 py-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody>
              {items.map((item) => {
                const label = item.taskType === 'Service' ? item.service : item.taskType;
                const isForm = item.taskType === 'MOI' || item.taskType === 'MOI Approval' || item.taskType === 'MOA';
                return (
                  <tr key={item.unitId} className="border-t border-border">
                    <td className="px-4 py-3 font-medium">{item.customer}</td>
                    <td className="px-4 py-3">
                      {isForm && onOpenTask ? (
                        <button
                          type="button"
                          onClick={() => onOpenTask(item.jobId, item.taskType, item.unitNumber)}
                          className="text-primary hover:underline"
                        >
                          {label}
                        </button>
                      ) : (
                        label
                      )}
                    </td>
                    <td className="px-4 py-3">#{item.unitNumber}</td>
                    <td className="px-4 py-3">{item.accountHolder || '—'}</td>
                    <td className="px-4 py-3 text-sm text-muted-foreground">
                      {item.scheduledDate || '—'}
                    </td>
                    <td className="px-4 py-3 text-center">
                      <span className="px-2 py-0.5 rounded-full text-xs bg-blue-100 text-blue-800">
                        {item.status}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-right">
                      <button
                        type="button"
                        onClick={() => handleComplete(item)}
                        className="inline-flex items-center gap-1 px-2 py-1 text-xs bg-green-600 text-white rounded hover:bg-green-700"
                      >
                        <Check className="w-3 h-3" />
                        Done
                      </button>
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
