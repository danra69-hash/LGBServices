import { Bell } from 'lucide-react';
import { useCallback, useEffect, useRef, useState } from 'react';
import {
  ApiError,
  getNotifications,
  markNotificationRead,
  type NotificationDto,
} from '@/lib/api';

interface NotificationBellProps {
  refreshKey?: number;
  onOpenJob?: (jobId: number) => void;
}

function formatRelativeTime(iso: string): string {
  const created = new Date(iso).getTime();
  if (Number.isNaN(created)) return '';
  const minutes = Math.floor((Date.now() - created) / 60_000);
  if (minutes < 1) return 'Just now';
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  return `${days}d ago`;
}

export function NotificationBell({ refreshKey = 0, onOpenJob }: NotificationBellProps) {
  const [open, setOpen] = useState(false);
  const [items, setItems] = useState<NotificationDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const rootRef = useRef<HTMLDivElement>(null);

  const unreadCount = items.filter((item) => !item.isRead).length;

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      setItems(await getNotifications());
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to load notifications.');
      setItems([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load, refreshKey]);

  useEffect(() => {
    if (!open) return;
    const timer = window.setInterval(() => {
      void load();
    }, 60_000);
    return () => window.clearInterval(timer);
  }, [open, load]);

  useEffect(() => {
    if (!open) return;
    const handlePointerDown = (event: MouseEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) {
        setOpen(false);
      }
    };
    document.addEventListener('mousedown', handlePointerDown);
    return () => document.removeEventListener('mousedown', handlePointerDown);
  }, [open]);

  const handleItemClick = async (item: NotificationDto) => {
    if (!item.isRead) {
      try {
        await markNotificationRead(item.id);
        setItems((prev) => prev.map((row) => (
          row.id === item.id ? { ...row, isRead: true } : row
        )));
      } catch {
        // ignore
      }
    }
    setOpen(false);
    if (item.jobRequestId != null) {
      onOpenJob?.(item.jobRequestId);
    }
  };

  return (
    <div ref={rootRef} className="relative">
      <button
        type="button"
        onClick={() => {
          setOpen((value) => !value);
          if (!open) void load();
        }}
        className="relative p-2 rounded-lg border border-border hover:bg-muted transition-colors"
        aria-label="Notifications"
        aria-expanded={open}
      >
        <Bell className="w-4 h-4" />
        {unreadCount > 0 && (
          <span className="absolute -top-1 -right-1 min-w-[1.1rem] h-[1.1rem] px-1 rounded-full bg-primary text-primary-foreground text-[10px] font-semibold flex items-center justify-center">
            {unreadCount > 9 ? '9+' : unreadCount}
          </span>
        )}
      </button>

      {open && (
        <div className="absolute right-0 top-full mt-2 w-80 max-h-96 overflow-y-auto rounded-lg border border-border bg-card shadow-lg z-50">
          <div className="px-3 py-2 border-b border-border">
            <p className="text-sm font-medium">Notifications</p>
          </div>

          {loading && items.length === 0 && (
            <p className="px-3 py-4 text-sm text-muted-foreground">Loading…</p>
          )}

          {error && (
            <p className="px-3 py-4 text-sm text-destructive">{error}</p>
          )}

          {!loading && !error && items.length === 0 && (
            <p className="px-3 py-4 text-sm text-muted-foreground">No notifications yet.</p>
          )}

          {items.length > 0 && (
            <ul className="divide-y divide-border">
              {items.map((item) => (
                <li key={item.id}>
                  <button
                    type="button"
                    onClick={() => void handleItemClick(item)}
                    className={`w-full text-left px-3 py-3 hover:bg-muted/60 transition-colors ${
                      !item.isRead ? 'bg-primary/5' : ''
                    }`}
                  >
                    <div className="flex items-start justify-between gap-2">
                      <p className={`text-sm ${!item.isRead ? 'font-medium' : ''}`}>{item.title}</p>
                      <span className="text-[10px] text-muted-foreground shrink-0">
                        {formatRelativeTime(item.createdAt)}
                      </span>
                    </div>
                    <p className="text-xs text-muted-foreground mt-1 line-clamp-2">{item.message}</p>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </div>
  );
}
