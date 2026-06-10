import { CalendarDays, ExternalLink, Plus, Trash2 } from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { formatDateDisplay, parseScheduleToDay } from '@/lib/dates';
import {
  ApiError,
  createPackageSchedule,
  deletePackageSchedule,
  getPackageSchedules,
  type CustomerResponse,
  type PackageScheduleItemDto,
} from '@/lib/api';

interface PackageTrackingProps {
  customers: CustomerResponse[];
  refreshKey?: number;
  onError: (message: string) => void;
  initialCustomerId?: number;
}

const ITEM_TYPES = [
  { value: 'call', label: 'Call' },
  { value: 'meeting', label: 'Meeting' },
  { value: 'board_meeting', label: 'Board meeting' },
  { value: 'review', label: 'Review' },
  { value: 'other', label: 'Other' },
];

function formatDisplayDate(iso: string, itemType?: string): string {
  if (itemType === 'work') {
    return formatDateDisplay(iso.slice(0, 10));
  }
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString(undefined, {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

export function PackageTracking({
  customers,
  refreshKey = 0,
  onError,
  initialCustomerId,
}: PackageTrackingProps) {
  const [customerId, setCustomerId] = useState<number | ''>(initialCustomerId ?? '');
  const [packageId, setPackageId] = useState<number | ''>('');
  const [items, setItems] = useState<PackageScheduleItemDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [monthOffset, setMonthOffset] = useState(0);
  const [form, setForm] = useState({
    itemType: 'call',
    title: '',
    scheduledAt: '',
    durationMinutes: '60',
    bookingUrl: '',
    sequenceNumber: '',
    notes: '',
  });

  const selectedCustomer = customers.find((c) => c.id === customerId);
  const packages = selectedCustomer?.packages?.filter((p) => p.status === 'Active') ?? [];

  const loadItems = useCallback(async () => {
    setLoading(true);
    try {
      const data = await getPackageSchedules(
        customerId ? { customerId: Number(customerId) } : undefined,
      );
      setItems(data);
    } catch (err) {
      onError(err instanceof ApiError ? err.message : 'Failed to load schedule.');
      setItems([]);
    } finally {
      setLoading(false);
    }
  }, [customerId, onError]);

  useEffect(() => {
    loadItems();
  }, [loadItems, refreshKey]);

  useEffect(() => {
    if (initialCustomerId) setCustomerId(initialCustomerId);
  }, [initialCustomerId]);

  useEffect(() => {
    if (packages.length === 1) setPackageId(packages[0].id);
  }, [packages]);

  const calendarMonth = useMemo(() => {
    const now = new Date();
    const view = new Date(now.getFullYear(), now.getMonth() + monthOffset, 1);
    const year = view.getFullYear();
    const month = view.getMonth();
    const firstDay = new Date(year, month, 1).getDay();
    const daysInMonth = new Date(year, month + 1, 0).getDate();
    const cells: Array<{ day: number | null; key: string }> = [];
    for (let i = 0; i < firstDay; i++) cells.push({ day: null, key: `e-${i}` });
    for (let d = 1; d <= daysInMonth; d++) cells.push({ day: d, key: `d-${d}` });
    return { year, month, cells, label: view.toLocaleString(undefined, { month: 'long', year: 'numeric' }) };
  }, [monthOffset]);

  const filteredItems = packageId
    ? items.filter((i) => i.customerPackageId === Number(packageId))
    : items;

  const itemsByDay = useMemo(() => {
    const map = new Map<number, PackageScheduleItemDto[]>();
    for (const item of filteredItems) {
      const parts = parseScheduleToDay(item.scheduledAt);
      if (!parts) continue;
      if (parts.year !== calendarMonth.year || parts.month !== calendarMonth.month) continue;
      map.set(parts.day, [...(map.get(parts.day) ?? []), item]);
    }
    return map;
  }, [filteredItems, calendarMonth.year, calendarMonth.month]);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!customerId || !packageId) {
      onError('Select a customer and package first.');
      return;
    }
    try {
      await createPackageSchedule({
        customerId: Number(customerId),
        customerPackageId: Number(packageId),
        itemType: form.itemType,
        title: form.title.trim(),
        scheduledAt: form.scheduledAt,
        durationMinutes: parseInt(form.durationMinutes, 10) || undefined,
        bookingUrl: form.bookingUrl.trim() || undefined,
        sequenceNumber: form.sequenceNumber ? parseInt(form.sequenceNumber, 10) : undefined,
        notes: form.notes.trim() || undefined,
      });
      setForm({
        itemType: 'call',
        title: '',
        scheduledAt: '',
        durationMinutes: '60',
        bookingUrl: '',
        sequenceNumber: '',
        notes: '',
      });
      await loadItems();
    } catch (err) {
      onError(err instanceof ApiError ? err.message : 'Failed to save schedule item.');
    }
  };

  const handleDelete = async (id: number) => {
    try {
      await deletePackageSchedule(id);
      await loadItems();
    } catch (err) {
      onError(err instanceof ApiError ? err.message : 'Failed to delete schedule item.');
    }
  };

  return (
    <div className="space-y-6">
      <div className="bg-card rounded-lg border border-border p-4 flex flex-wrap gap-4 items-end">
        <div className="min-w-[200px] flex-1">
          <label className="block text-xs text-muted-foreground mb-1">Customer</label>
          <select
            value={customerId === '' ? '' : String(customerId)}
            onChange={(e) => {
              setCustomerId(e.target.value ? Number(e.target.value) : '');
              setPackageId('');
            }}
            className="w-full px-3 py-2 border border-border rounded-lg bg-input-background"
          >
            <option value="">All customers</option>
            {customers.map((c) => (
              <option key={c.id} value={c.id}>
                {c.company}
              </option>
            ))}
          </select>
        </div>
        <div className="min-w-[200px] flex-1">
          <label className="block text-xs text-muted-foreground mb-1">Package</label>
          <select
            value={packageId === '' ? '' : String(packageId)}
            onChange={(e) => setPackageId(e.target.value ? Number(e.target.value) : '')}
            disabled={!customerId}
            className="w-full px-3 py-2 border border-border rounded-lg bg-input-background disabled:opacity-50"
          >
            <option value="">All packages</option>
            {packages.map((p) => (
              <option key={p.id} value={p.id}>
                {p.packageName} ({p.validity || '1 Year'})
              </option>
            ))}
          </select>
        </div>
      </div>

      <div className="grid grid-cols-1 xl:grid-cols-2 gap-6">
        <div className="bg-card rounded-lg border border-border overflow-hidden">
          <div className="p-4 border-b border-border flex items-center justify-between">
            <div className="flex items-center gap-2">
              <CalendarDays className="w-5 h-5 text-muted-foreground" />
              <h2>Calendar</h2>
            </div>
            <div className="flex gap-2">
              <button
                type="button"
                onClick={() => setMonthOffset((m) => m - 1)}
                className="px-2 py-1 border border-border rounded hover:bg-muted text-sm"
              >
                Prev
              </button>
              <button
                type="button"
                onClick={() => setMonthOffset(0)}
                className="px-2 py-1 border border-border rounded hover:bg-muted text-sm"
              >
                Today
              </button>
              <button
                type="button"
                onClick={() => setMonthOffset((m) => m + 1)}
                className="px-2 py-1 border border-border rounded hover:bg-muted text-sm"
              >
                Next
              </button>
            </div>
          </div>
          <div className="p-4">
            <p className="text-center font-medium mb-3">{calendarMonth.label}</p>
            <div className="grid grid-cols-7 gap-1 text-center text-xs text-muted-foreground mb-1">
              {['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'].map((d) => (
                <div key={d}>{d}</div>
              ))}
            </div>
            <div className="grid grid-cols-7 gap-1">
              {calendarMonth.cells.map((cell) => {
                const dayItems = cell.day ? itemsByDay.get(cell.day) ?? [] : [];
                return (
                  <div
                    key={cell.key}
                    className={`min-h-[72px] rounded border p-1 text-xs ${
                      cell.day ? 'border-border bg-muted/20' : 'border-transparent'
                    }`}
                  >
                    {cell.day && <div className="font-medium mb-1">{cell.day}</div>}
                    {dayItems.slice(0, 2).map((item) => (
                      <div
                        key={item.id}
                        className={`truncate rounded px-1 mb-0.5 ${
                          item.itemType === 'work'
                            ? 'bg-amber-100 text-amber-900'
                            : 'bg-primary/10 text-primary'
                        }`}
                        title={item.title}
                      >
                        {item.itemType === 'work'
                          ? item.title
                          : `${new Date(item.scheduledAt).toLocaleTimeString(undefined, {
                              hour: '2-digit',
                              minute: '2-digit',
                            })} ${item.title}`}
                      </div>
                    ))}
                    {dayItems.length > 2 && (
                      <div className="text-muted-foreground">+{dayItems.length - 2} more</div>
                    )}
                  </div>
                );
              })}
            </div>
          </div>
        </div>

        <div className="space-y-6">
          <div className="bg-card rounded-lg border border-border overflow-hidden">
            <div className="p-4 border-b border-border">
              <h2 className="flex items-center gap-2">
                <Plus className="w-5 h-5" />
                Schedule package item
              </h2>
              <p className="text-sm text-muted-foreground mt-1">
                Book calls, meetings, or sessions tied to a customer package (e.g. call 1 of 2).
              </p>
            </div>
            <form onSubmit={handleCreate} className="p-4 space-y-3">
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs text-muted-foreground mb-1">Type</label>
                  <select
                    value={form.itemType}
                    onChange={(e) => setForm({ ...form, itemType: e.target.value })}
                    className="w-full px-3 py-2 border border-border rounded-lg bg-input-background"
                  >
                    {ITEM_TYPES.map((t) => (
                      <option key={t.value} value={t.value}>
                        {t.label}
                      </option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="block text-xs text-muted-foreground mb-1">Sequence (e.g. 1 of 2)</label>
                  <input
                    type="number"
                    min="1"
                    placeholder="1"
                    value={form.sequenceNumber}
                    onChange={(e) => setForm({ ...form, sequenceNumber: e.target.value })}
                    className="w-full px-3 py-2 border border-border rounded-lg bg-input-background"
                  />
                </div>
              </div>
              <div>
                <label className="block text-xs text-muted-foreground mb-1">Title *</label>
                <input
                  required
                  value={form.title}
                  onChange={(e) => setForm({ ...form, title: e.target.value })}
                  placeholder="Q2 review call"
                  className="w-full px-3 py-2 border border-border rounded-lg bg-input-background"
                />
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs text-muted-foreground mb-1">Date & time *</label>
                  <input
                    type="datetime-local"
                    required
                    value={form.scheduledAt}
                    onChange={(e) => setForm({ ...form, scheduledAt: e.target.value })}
                    className="w-full px-3 py-2 border border-border rounded-lg bg-input-background"
                  />
                </div>
                <div>
                  <label className="block text-xs text-muted-foreground mb-1">Duration (mins)</label>
                  <input
                    type="number"
                    min="15"
                    step="15"
                    value={form.durationMinutes}
                    onChange={(e) => setForm({ ...form, durationMinutes: e.target.value })}
                    className="w-full px-3 py-2 border border-border rounded-lg bg-input-background"
                  />
                </div>
              </div>
              <div>
                <label className="block text-xs text-muted-foreground mb-1">Calendly / booking link</label>
                <input
                  type="url"
                  value={form.bookingUrl}
                  onChange={(e) => setForm({ ...form, bookingUrl: e.target.value })}
                  placeholder="https://calendly.com/..."
                  className="w-full px-3 py-2 border border-border rounded-lg bg-input-background"
                />
              </div>
              <textarea
                rows={2}
                placeholder="Notes (optional)"
                value={form.notes}
                onChange={(e) => setForm({ ...form, notes: e.target.value })}
                className="w-full px-3 py-2 border border-border rounded-lg bg-input-background resize-none"
              />
              <button
                type="submit"
                disabled={!customerId || !packageId}
                className="w-full px-4 py-2 bg-primary text-primary-foreground rounded-lg disabled:opacity-50"
              >
                Add to schedule
              </button>
            </form>
          </div>

          <div className="bg-card rounded-lg border border-border overflow-hidden">
            <div className="p-4 border-b border-border">
              <h2>Upcoming & scheduled</h2>
            </div>
            {loading ? (
              <p className="p-4 text-muted-foreground text-sm">Loading...</p>
            ) : filteredItems.length === 0 ? (
              <p className="p-4 text-muted-foreground text-sm">No scheduled items yet.</p>
            ) : (
              <ul className="divide-y divide-border">
                {filteredItems.map((item) => (
                  <li key={item.id} className="p-4 flex gap-3 justify-between">
                    <div className="min-w-0">
                      <div className="font-medium truncate">{item.title}</div>
                      <div className="text-sm text-muted-foreground">
                        {item.customerName} · {item.packageName}
                        {item.sequenceNumber ? ` · #${item.sequenceNumber}` : ''}
                      </div>
                      <div className="text-sm mt-1">{formatDisplayDate(item.scheduledAt, item.itemType)}</div>
                      {item.bookingUrl && (
                        <a
                          href={item.bookingUrl}
                          target="_blank"
                          rel="noreferrer"
                          className="inline-flex items-center gap-1 text-sm text-primary mt-1 hover:underline"
                        >
                          <ExternalLink className="w-3 h-3" />
                          Booking link
                        </a>
                      )}
                    </div>
                    <button
                      type="button"
                      onClick={() => handleDelete(item.id)}
                      className="p-1 text-destructive hover:bg-destructive/10 rounded shrink-0"
                      title="Remove"
                    >
                      <Trash2 className="w-4 h-4" />
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
