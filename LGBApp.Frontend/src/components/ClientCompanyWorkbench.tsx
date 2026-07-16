import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { Building2, Check, CheckCircle2, ChevronDown, ChevronLeft, ChevronRight, FileText, History, Plus, Undo2 } from 'lucide-react';
import { DateInput } from './DateInput';
import { formatDateDisplay } from '@/lib/dates';
import type { JobRequestResponse, JobRequestUnitDto, UserResponse } from '@/lib/api';
import {
  displayStatusKeyForUnit,
  displayStatusLabelForUnit,
  isMoaClientSignoffPhase,
  packageItemStatusBadgeClass,
  signatoryCanSignMoa,
  signatoryCanSignMoi,
} from '@/lib/packageItemStatus';
import { ALL_SERVICES, resolveServiceCategory, SERVICE_CATEGORY_ORDER } from '@/lib/serviceCategory';
import { jobDisplayTitle } from '@/lib/jobDisplayTitle';

export type PortalWorkItem = { job: JobRequestResponse; unit: JobRequestUnitDto };
type BrowseMode = 'open' | 'completed';

interface ClientCompanyWorkbenchProps {
  jobs: JobRequestResponse[];
  currentUser: UserResponse;
  isSignatoryView: boolean;
  loading: boolean;
  onOpenPrimary: (job: JobRequestResponse, unit: JobRequestUnitDto) => void;
  renderFormActions: (job: JobRequestResponse, unit: JobRequestUnitDto) => ReactNode;
  onSchedule?: (job: JobRequestResponse, unitNumber: number, iso: string) => void;
  onMarkDone?: (job: JobRequestResponse, unitNumber: number) => void | Promise<void>;
  onUndo?: (job: JobRequestResponse, unitNumber: number) => void | Promise<void>;
  /** Claim next dormant multi-qty session; returns updated job. */
  onActivateSession?: (job: JobRequestResponse) => Promise<JobRequestResponse | void>;
}

function jobUnits(job: JobRequestResponse): JobRequestUnitDto[] {
  if (job.units?.length) return job.units;
  return [{
    id: 0,
    unitNumber: 1,
    assignedUserName: job.jobAssignedTo,
    status: job.status as JobRequestUnitDto['status'],
    scheduledDate: job.scheduledDate,
    assignees: [],
  }];
}

function unitIsComplete(job: JobRequestResponse, unit: JobRequestUnitDto): boolean {
  if (unit.status === 'Completed' || job.status === 'Completed') return true;
  const key = displayStatusKeyForUnit(job, unit);
  return key === 'completed' || key === 'canceled';
}

/** Qty-1 always active. Multi-qty needs clientActivatedAt (or completed). */
function isSessionActive(job: JobRequestResponse, unit: JobRequestUnitDto): boolean {
  if ((job.totalQty ?? 1) <= 1) return true;
  if (unitIsComplete(job, unit)) return true;
  return Boolean(unit.clientActivatedAt);
}

function isSessionDormant(job: JobRequestResponse, unit: JobRequestUnitDto): boolean {
  return (job.totalQty ?? 1) > 1 && !unitIsComplete(job, unit) && !unit.clientActivatedAt;
}

/** Pending MOI / MOA signature for this user (or company-wide client approval wait). */
function unitNeedsSignature(
  job: JobRequestResponse,
  unit: JobRequestUnitDto,
  user: UserResponse,
  isSignatoryView: boolean,
): boolean {
  if (unitIsComplete(job, unit)) return false;
  if (!isSessionActive(job, unit)) return false;
  if (signatoryCanSignMoi(job, user, unit)) return true;
  if (signatoryCanSignMoa(job, user, unit)) return true;
  if (!isSignatoryView && user.needsMoa && isMoaClientSignoffPhase(job, unit)) return true;
  const key = displayStatusKeyForUnit(job, unit);
  if (key === 'pending_sign_off' || unit.moiWorkflowState === 'PendingClientMoiApproval') return true;
  if (key === 'moa_circulation' || key === 'ready_for_moa') return true;
  return false;
}

function CircleProgress({ completed, total, size = 64, alert = false }: {
  completed: number;
  total: number;
  size?: number;
  alert?: boolean;
}) {
  const pct = total > 0 ? Math.min(1, completed / total) : 0;
  const r = (size - 10) / 2;
  const c = 2 * Math.PI * r;
  const offset = c * (1 - pct);
  return (
    <div className="relative inline-flex items-center justify-center shrink-0" style={{ width: size, height: size }}>
      <svg width={size} height={size} className="-rotate-90" aria-hidden>
        <circle cx={size / 2} cy={size / 2} r={r} fill="none" stroke="currentColor" strokeWidth="5" className="text-muted" />
        <circle
          cx={size / 2}
          cy={size / 2}
          r={r}
          fill="none"
          stroke="currentColor"
          strokeWidth="5"
          strokeLinecap="round"
          strokeDasharray={c}
          strokeDashoffset={offset}
          className={`${alert ? 'text-amber-500' : 'text-primary'} transition-[stroke-dashoffset] duration-300`}
        />
      </svg>
      <span
        className={`absolute font-bold tabular-nums ${size >= 80 ? 'text-base' : size >= 64 ? 'text-sm' : 'text-xs'}`}
      >
        {completed}/{total}
      </span>
    </div>
  );
}

/** Square tile with border fill as % complete. Amber when a signature is pending. */
function ProgressBorderFrame({
  pct,
  pendingSign,
  className = '',
  children,
}: {
  pct: number;
  pendingSign?: boolean;
  className?: string;
  children: ReactNode;
}) {
  const clamped = Math.max(0, Math.min(1, pct));
  const fill = pendingSign ? '#f59e0b' : 'hsl(var(--primary))';
  const track = pendingSign ? 'rgba(245, 158, 11, 0.22)' : 'hsl(var(--border))';

  return (
    <div
      className={`relative rounded-[14px] p-[3px] ${className}`}
      style={{
        background: `conic-gradient(from -90deg, ${fill} ${clamped * 100}%, ${track} 0)`,
      }}
    >
      <div
        className={`relative h-full w-full rounded-[11px] bg-card overflow-hidden ${
          pendingSign ? 'shadow-[inset_0_0_0_1px_rgba(245,158,11,0.35)]' : ''
        }`}
      >
        {children}
      </div>
    </div>
  );
}

function collectItems(
  jobs: JobRequestResponse[],
  company: string,
  category: string | null,
  mode: BrowseMode | 'all',
): PortalWorkItem[] {
  const items: PortalWorkItem[] = [];
  for (const job of jobs) {
    if ((job.customer?.trim() || 'Unknown company') !== company) continue;
    if (job.taskType !== 'Service') continue;
    if (category && category !== ALL_SERVICES && resolveServiceCategory(job.service) !== category) continue;
    for (const unit of jobUnits(job)) {
      const done = unitIsComplete(job, unit);
      if (mode === 'open') {
        // Multi-qty dormant sessions are not open until client Adds them.
        if (done || !isSessionActive(job, unit)) continue;
      } else if (mode === 'completed') {
        if (!done) continue;
      }
      // mode 'all': include every unit for entitlement progress (completed/total)
      items.push({ job, unit });
    }
  }
  return items;
}

function categoryStats(jobs: JobRequestResponse[], company: string) {
  const map = new Map<string, { completed: number; open: number; total: number; remaining: number; needsSign: boolean }>();
  for (const job of jobs) {
    if ((job.customer?.trim() || 'Unknown company') !== company) continue;
    if (job.taskType !== 'Service') continue;
    const cat = resolveServiceCategory(job.service);
    for (const unit of jobUnits(job)) {
      const row = map.get(cat) ?? { completed: 0, open: 0, total: 0, remaining: 0, needsSign: false };
      row.total += 1;
      if (unitIsComplete(job, unit)) row.completed += 1;
      else if (isSessionActive(job, unit)) row.open += 1;
      else if (isSessionDormant(job, unit)) row.remaining += 1;
      map.set(cat, row);
    }
  }
  return SERVICE_CATEGORY_ORDER
    .filter((c) => c !== ALL_SERVICES && map.has(c))
    .map((c) => ({ category: c, ...(map.get(c)!) }));
}

function itemKey(item: PortalWorkItem) {
  return `${item.job.id}-${item.unit.unitNumber}`;
}

export function ClientCompanyWorkbench({
  jobs,
  currentUser,
  isSignatoryView,
  loading,
  onOpenPrimary,
  renderFormActions,
  onSchedule,
  onMarkDone,
  onUndo,
  onActivateSession,
}: ClientCompanyWorkbenchProps) {
  const [selectedCompany, setSelectedCompany] = useState<string | null>(null);
  const [selectedCategory, setSelectedCategory] = useState<string | null>(null);
  const [browseMode, setBrowseMode] = useState<BrowseMode>('open');
  const [itemIndex, setItemIndex] = useState(0);
  const [openDropdown, setOpenDropdown] = useState<string | null>(null);
  const [focusKey, setFocusKey] = useState<string | null>(null);
  const [activatingJobId, setActivatingJobId] = useState<number | null>(null);

  const companies = useMemo(() => {
    const fromAccess = currentUser.accessibleCompanies?.map((c) => c.company).filter(Boolean) ?? [];
    const fromJobs = [...new Set(jobs.map((j) => j.customer?.trim() || 'Unknown company'))];
    const ordered = fromAccess.length > 0
      ? [...fromAccess, ...fromJobs.filter((c) => !fromAccess.includes(c))]
      : fromJobs;
    if (ordered.length === 0 && currentUser.customerName) return [currentUser.customerName];
    return ordered;
  }, [jobs, currentUser.accessibleCompanies, currentUser.customerName]);

  const companyMeta = useMemo(() => {
    const map = new Map<string, { completed: number; total: number; open: number; needsSign: boolean }>();
    for (const company of companies) {
      const all = collectItems(jobs, company, null, 'all');
      const completed = all.filter(({ job, unit }) => unitIsComplete(job, unit)).length;
      const open = all.filter(
        ({ job, unit }) => !unitIsComplete(job, unit) && isSessionActive(job, unit),
      ).length;
      const needsSign = all.some(({ job, unit }) => unitNeedsSignature(job, unit, currentUser, isSignatoryView));
      map.set(company, { completed, total: all.length, open, needsSign });
    }
    return map;
  }, [jobs, companies, currentUser, isSignatoryView]);

  const categories = useMemo(() => {
    if (!selectedCompany) return [];
    const stats = categoryStats(jobs, selectedCompany).map((cat) => {
      const items = collectItems(jobs, selectedCompany, cat.category, 'all');
      const needsSign = items.some(({ job, unit }) => unitNeedsSignature(job, unit, currentUser, isSignatoryView));
      return { ...cat, needsSign };
    });
    if (browseMode === 'completed') return stats.filter((c) => c.completed > 0);
    // Keep categories with open work OR remaining multi-qty quota to Add
    return stats.filter((c) => c.open > 0 || c.remaining > 0 || c.total > 0);
  }, [jobs, selectedCompany, browseMode, currentUser, isSignatoryView]);

  const multiQtyAddable = useMemo(() => {
    if (!selectedCompany || !selectedCategory || browseMode === 'completed') return [];
    const lines: { job: JobRequestResponse; remaining: number }[] = [];
    for (const job of jobs) {
      if ((job.customer?.trim() || 'Unknown company') !== selectedCompany) continue;
      if (job.taskType !== 'Service') continue;
      if (resolveServiceCategory(job.service) !== selectedCategory) continue;
      if ((job.totalQty ?? 1) <= 1) continue;
      const remaining = jobUnits(job).filter((u) => isSessionDormant(job, u)).length;
      if (remaining > 0) lines.push({ job, remaining });
    }
    return lines;
  }, [jobs, selectedCompany, selectedCategory, browseMode]);

  const workItems = useMemo(() => {
    if (!selectedCompany || !selectedCategory) return [];
    return collectItems(jobs, selectedCompany, selectedCategory, browseMode);
  }, [jobs, selectedCompany, selectedCategory, browseMode]);

  const categoryAllItems = useMemo(() => {
    if (!selectedCompany || !selectedCategory) return [];
    return collectItems(jobs, selectedCompany, selectedCategory, 'all');
  }, [jobs, selectedCompany, selectedCategory]);

  // Keep focus on the same item after reload; otherwise clamp / auto-advance into next open item
  useEffect(() => {
    if (workItems.length === 0) {
      setItemIndex(0);
      return;
    }
    if (focusKey) {
      const idx = workItems.findIndex((i) => itemKey(i) === focusKey);
      if (idx >= 0) {
        setItemIndex(idx);
        return;
      }
      // Item left the list (e.g. marked done while in open mode) → stay on same index (auto-advance)
      setFocusKey(null);
      setItemIndex((i) => Math.min(i, workItems.length - 1));
      return;
    }
    setItemIndex((i) => Math.min(i, workItems.length - 1));
  }, [workItems, focusKey]);

  const safeIndex = workItems.length === 0 ? 0 : Math.min(itemIndex, workItems.length - 1);
  const current = workItems[safeIndex] ?? null;

  const resetToCompanies = () => {
    setSelectedCompany(null);
    setSelectedCategory(null);
    setBrowseMode('open');
    setItemIndex(0);
    setFocusKey(null);
    setOpenDropdown(null);
  };

  const enterCompany = (company: string, mode: BrowseMode = 'open') => {
    setSelectedCompany(company);
    setSelectedCategory(null);
    setBrowseMode(mode);
    setItemIndex(0);
    setFocusKey(null);
    setOpenDropdown(null);
  };

  const enterCategory = (category: string, mode?: BrowseMode) => {
    if (mode) setBrowseMode(mode);
    setSelectedCategory(category);
    setItemIndex(0);
    setFocusKey(null);
  };

  const handleDone = async (job: JobRequestResponse, unitNumber: number) => {
    if (!onMarkDone) return;
    // Keep index; after reload the completed item drops out and the next open item takes its place
    setFocusKey(null);
    await onMarkDone(job, unitNumber);
  };

  const handleActivate = async (job: JobRequestResponse) => {
    if (!onActivateSession) return;
    setActivatingJobId(job.id);
    try {
      const updated = await onActivateSession(job);
      const unitNumber = updated?.activeUnitNumber
        ?? updated?.units?.find((u) => u.clientActivatedAt && u.status !== 'Completed')?.unitNumber;
      if (unitNumber != null) {
        setBrowseMode('open');
        setFocusKey(`${job.id}-${unitNumber}`);
      }
    } finally {
      setActivatingJobId(null);
    }
  };

  if (loading) {
    return <p className="p-6 text-sm text-muted-foreground">Loading jobs…</p>;
  }

  if (companies.length === 0) {
    return <p className="p-6 text-sm text-muted-foreground">No companies available yet.</p>;
  }

  // Level 3 — one work item at a time
  if (selectedCompany && selectedCategory) {
    const done = categoryAllItems.filter(({ job, unit }) => unitIsComplete(job, unit)).length;
    const total = categoryAllItems.length;
    const openCount = categoryAllItems.filter(
      ({ job, unit }) => !unitIsComplete(job, unit) && isSessionActive(job, unit),
    ).length;
    const viewingCompleted = browseMode === 'completed';

    return (
      <div className="space-y-4">
        <div className="flex flex-wrap items-center gap-3">
          <button
            type="button"
            onClick={() => {
              setSelectedCategory(null);
              setItemIndex(0);
              setFocusKey(null);
            }}
            className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground"
          >
            <ChevronLeft className="w-4 h-4" />
            {selectedCompany}
          </button>
          <span className="text-muted-foreground">/</span>
          <span className="text-sm font-medium">{selectedCategory}</span>
          <span className={`text-xs px-2 py-0.5 rounded-full ${viewingCompleted ? 'bg-green-100 text-green-800' : 'bg-muted text-muted-foreground'}`}>
            {viewingCompleted ? 'History' : 'Open'}
          </span>
        </div>

        <div className="bg-card border border-border rounded-lg p-6 flex flex-col md:flex-row gap-6 items-start">
          <CircleProgress completed={done} total={total} size={104} alert={categoryAllItems.some(({ job, unit }) => unitNeedsSignature(job, unit, currentUser, isSignatoryView))} />
          <div className="flex-1 min-w-0 space-y-4 w-full">
            <div className="flex flex-wrap gap-2">
              <button
                type="button"
                onClick={() => {
                  setBrowseMode('open');
                  setItemIndex(0);
                  setFocusKey(null);
                }}
                className={`inline-flex items-center gap-1.5 px-3 py-1.5 text-sm rounded-lg border ${
                  !viewingCompleted ? 'bg-primary text-primary-foreground border-primary' : 'border-border text-muted-foreground'
                }`}
              >
                Open ({openCount})
              </button>
              <button
                type="button"
                onClick={() => {
                  setBrowseMode('completed');
                  setItemIndex(0);
                  setFocusKey(null);
                }}
                className={`inline-flex items-center gap-1.5 px-3 py-1.5 text-sm rounded-lg border ${
                  viewingCompleted ? 'bg-green-600 text-white border-green-600' : 'border-border text-muted-foreground'
                }`}
              >
                <History className="w-3.5 h-3.5" />
                History ({done})
              </button>
            </div>

            {workItems.length === 0 ? (
              <div>
                <p className="font-medium text-muted-foreground">
                  {viewingCompleted
                    ? 'No completed items in this category yet.'
                    : multiQtyAddable.length > 0
                      ? 'No sessions in progress. Start one below when you need it.'
                      : 'All items in this category are complete.'}
                </p>
                {!viewingCompleted && multiQtyAddable.length > 0 && onActivateSession && (
                  <div className="mt-4 flex flex-wrap gap-2">
                    {multiQtyAddable.map(({ job, remaining }) => (
                      <button
                        key={job.id}
                        type="button"
                        disabled={activatingJobId === job.id}
                        onClick={() => void handleActivate(job)}
                        className="inline-flex items-center gap-1.5 px-3 py-2 text-sm bg-primary text-primary-foreground rounded-lg disabled:opacity-50"
                      >
                        <Plus className="w-4 h-4" />
                        {activatingJobId === job.id ? 'Starting…' : `Add ${job.service}`}
                        <span className="text-xs opacity-80">({remaining} left)</span>
                      </button>
                    ))}
                  </div>
                )}
                {!viewingCompleted && done > 0 && (
                  <button
                    type="button"
                    className="mt-3 text-sm text-primary hover:underline"
                    onClick={() => {
                      setBrowseMode('completed');
                      setItemIndex(0);
                    }}
                  >
                    Browse history
                  </button>
                )}
                <button
                  type="button"
                  className="mt-3 ml-3 text-sm text-muted-foreground hover:underline"
                  onClick={() => setSelectedCategory(null)}
                >
                  Back to categories
                </button>
              </div>
            ) : current ? (
              <>
                {!viewingCompleted && multiQtyAddable.length > 0 && onActivateSession && (
                  <div className="flex flex-wrap gap-2 pb-2 border-b border-border">
                    {multiQtyAddable.map(({ job, remaining }) => (
                      <button
                        key={job.id}
                        type="button"
                        disabled={activatingJobId === job.id}
                        onClick={() => void handleActivate(job)}
                        className="inline-flex items-center gap-1.5 px-3 py-1.5 text-sm border border-border rounded-lg hover:bg-muted disabled:opacity-50"
                      >
                        <Plus className="w-3.5 h-3.5" />
                        {activatingJobId === job.id ? 'Starting…' : `Add ${job.service}`}
                        <span className="text-xs text-muted-foreground">{remaining} left</span>
                      </button>
                    ))}
                  </div>
                )}
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0">
                    <p className="text-xs text-muted-foreground mb-1">
                      {viewingCompleted ? 'Completed' : 'Open'} item {safeIndex + 1} of {workItems.length}
                      {(current.job.totalQty ?? 1) > 1 ? ` · session #${current.unit.unitNumber}` : ''}
                    </p>
                    <h3 className="text-lg font-semibold break-words">
                      {jobDisplayTitle(current.job, current.unit)}
                    </h3>
                    {/* Bucket stays on service category — never on document title */}
                    <p className="text-xs text-muted-foreground mt-1">
                      {resolveServiceCategory(current.job.service)}
                    </p>
                  </div>
                  <span className={`text-xs px-2 py-0.5 rounded-full shrink-0 ${packageItemStatusBadgeClass(displayStatusKeyForUnit(current.job, current.unit))}`}>
                    {displayStatusLabelForUnit(current.job, current.unit)}
                  </span>
                </div>

                <div className="flex flex-wrap items-center gap-4 text-sm">
                  <div>
                    <p className="text-xs text-muted-foreground mb-1">Target date</p>
                    {isSignatoryView || !onSchedule || viewingCompleted ? (
                      <span>{formatDateDisplay(current.unit.scheduledDate) || '—'}</span>
                    ) : (
                      <DateInput
                        value={current.unit.scheduledDate}
                        onChange={(iso) => onSchedule(current.job, current.unit.unitNumber, iso)}
                      />
                    )}
                  </div>
                  <div>
                    <p className="text-xs text-muted-foreground mb-1">Form</p>
                    {renderFormActions(current.job, current.unit)}
                  </div>
                </div>

                <div className="flex flex-wrap items-center gap-2 pt-2 border-t border-border">
                  <button
                    type="button"
                    disabled={safeIndex <= 0}
                    onClick={() => {
                      setFocusKey(null);
                      setItemIndex((i) => Math.max(0, i - 1));
                    }}
                    className="inline-flex items-center gap-1 px-3 py-1.5 text-sm border border-border rounded-lg disabled:opacity-40"
                  >
                    <ChevronLeft className="w-4 h-4" /> Prev
                  </button>
                  <button
                    type="button"
                    disabled={safeIndex >= workItems.length - 1}
                    onClick={() => {
                      setFocusKey(null);
                      setItemIndex((i) => Math.min(workItems.length - 1, i + 1));
                    }}
                    className="inline-flex items-center gap-1 px-3 py-1.5 text-sm border border-border rounded-lg disabled:opacity-40"
                  >
                    Next <ChevronRight className="w-4 h-4" />
                  </button>
                  <button
                    type="button"
                    onClick={() => {
                      setFocusKey(itemKey(current));
                      onOpenPrimary(current.job, current.unit);
                    }}
                    className="inline-flex items-center gap-1 px-3 py-1.5 text-sm bg-primary text-primary-foreground rounded-lg"
                  >
                    <FileText className="w-4 h-4" /> Open form
                  </button>
                  {!isSignatoryView && onMarkDone && !viewingCompleted && current.unit.status !== 'Completed' && (
                    <button
                      type="button"
                      onClick={() => void handleDone(current.job, current.unit.unitNumber)}
                      className="inline-flex items-center gap-1 px-3 py-1.5 text-sm bg-green-600 text-white rounded-lg"
                    >
                      <Check className="w-4 h-4" /> Done
                    </button>
                  )}
                  {!isSignatoryView && onUndo && viewingCompleted && (
                    <button
                      type="button"
                      onClick={() => {
                        setFocusKey(itemKey(current));
                        void onUndo(current.job, current.unit.unitNumber);
                      }}
                      className="inline-flex items-center gap-1 px-3 py-1.5 text-sm border border-border rounded-lg"
                    >
                      <Undo2 className="w-4 h-4" /> Undo
                    </button>
                  )}
                </div>
              </>
            ) : null}
          </div>
        </div>
      </div>
    );
  }

  // Level 2 — categories for one company
  if (selectedCompany) {
    const meta = companyMeta.get(selectedCompany) ?? { completed: 0, total: 0, open: 0, needsSign: false };
    return (
      <div className="space-y-4">
        <button
          type="button"
          onClick={resetToCompanies}
          className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground"
        >
          <ChevronLeft className="w-4 h-4" /> All companies
        </button>
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h3 className="text-lg font-semibold">{selectedCompany}</h3>
            <p className="text-sm text-muted-foreground mt-1">
              {browseMode === 'completed'
                ? 'Browse completed items by category.'
                : 'Choose a category. For multi-session lines, start a session when you need it — several can be in progress at once.'}
            </p>
          </div>
          <div className="flex gap-2">
            <button
              type="button"
              onClick={() => setBrowseMode('open')}
              className={`inline-flex items-center gap-1.5 px-3 py-1.5 text-sm rounded-lg border ${
                browseMode === 'open' ? 'bg-primary text-primary-foreground border-primary' : 'border-border'
              }`}
            >
              Open ({meta.open})
            </button>
            <button
              type="button"
              onClick={() => setBrowseMode('completed')}
              className={`inline-flex items-center gap-1.5 px-3 py-1.5 text-sm rounded-lg border ${
                browseMode === 'completed' ? 'bg-green-600 text-white border-green-600' : 'border-border'
              }`}
            >
              <CheckCircle2 className="w-3.5 h-3.5" />
              Completed ({meta.completed})
            </button>
          </div>
        </div>
        {categories.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            {browseMode === 'completed' ? 'No completed items yet.' : 'No package services for this company yet.'}
          </p>
        ) : (
          <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-3">
            {categories.map((cat) => {
              const pct = cat.total > 0 ? cat.completed / cat.total : 0;
              return (
                <ProgressBorderFrame
                  key={cat.category}
                  pct={pct}
                  pendingSign={browseMode === 'open' && cat.needsSign}
                  className="aspect-square"
                >
                  <button
                    type="button"
                    onClick={() => enterCategory(cat.category)}
                    className="w-full h-full p-4 flex flex-col items-center justify-center gap-3 hover:bg-primary/5 transition-colors text-center rounded-[12px]"
                  >
                    <CircleProgress
                      completed={cat.completed}
                      total={cat.total}
                      size={84}
                      alert={browseMode === 'open' && cat.needsSign}
                    />
                    <span className="text-lg font-bold leading-snug px-1">{cat.category}</span>
                    <span className="text-base font-medium text-muted-foreground">
                      {browseMode === 'completed'
                        ? `${cat.completed} completed`
                        : cat.open > 0
                          ? `${cat.open} open · ${cat.completed} done`
                          : cat.remaining > 0
                            ? `${cat.remaining} available to start`
                            : `${cat.completed} done`}
                    </span>
                  </button>
                </ProgressBorderFrame>
              );
            })}
          </div>
        )}
      </div>
    );
  }

  // Level 1 — company squares
  return (
    <div className="space-y-3">
      <h3 className="text-sm font-medium text-muted-foreground">Companies</h3>
      <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 gap-3">
        {companies.map((company) => {
          const meta = companyMeta.get(company) ?? { completed: 0, total: 0, open: 0, needsSign: false };
          const pct = meta.total > 0 ? meta.completed / meta.total : 0;
          const stats = categoryStats(jobs, company);
          const dropdownOpen = openDropdown === company;
          return (
            <div key={company} className="relative">
              <ProgressBorderFrame
                pct={pct}
                pendingSign={meta.needsSign}
                className="aspect-square"
              >
                <button
                  type="button"
                  onClick={() => enterCompany(company, 'open')}
                  className="w-full h-full p-3.5 flex flex-col items-center justify-center gap-3 hover:bg-primary/5 transition-colors text-center rounded-[12px]"
                >
                  <div className={`w-14 h-14 rounded-2xl flex items-center justify-center ${
                    meta.needsSign ? 'bg-amber-100 text-amber-700' : 'bg-primary/10 text-primary'
                  }`}>
                    <Building2 className="w-7 h-7" />
                  </div>
                  <span className="text-lg sm:text-xl font-bold leading-snug line-clamp-3 px-1">{company}</span>
                  <span className="text-lg font-bold text-muted-foreground tabular-nums">
                    {meta.completed}/{meta.total} done
                  </span>
                </button>
              </ProgressBorderFrame>

              {/* Completed / history shortcut on the company tile */}
              {meta.completed > 0 && (
                <button
                  type="button"
                  title="View completed"
                  onClick={(e) => {
                    e.stopPropagation();
                    enterCompany(company, 'completed');
                  }}
                  className="absolute bottom-2.5 left-2.5 z-10 inline-flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg bg-green-600 text-white text-base font-bold shadow-sm hover:bg-green-700"
                >
                  <CheckCircle2 className="w-5 h-5" />
                  {meta.completed}
                </button>
              )}

              <button
                type="button"
                title="Category progress"
                onClick={(e) => {
                  e.stopPropagation();
                  setOpenDropdown((prev) => (prev === company ? null : company));
                }}
                className="absolute top-2.5 right-2.5 z-10 p-2 rounded-lg bg-background/90 border border-border text-muted-foreground hover:text-foreground"
              >
                <ChevronDown className={`w-5 h-5 transition-transform ${dropdownOpen ? 'rotate-180' : ''}`} />
              </button>
              {dropdownOpen && (
                <div className="absolute z-20 left-0 right-0 top-[calc(100%-0.5rem)] mt-1 bg-card border border-border rounded-lg shadow-lg p-2 text-left">
                  {stats.length === 0 ? (
                    <p className="text-xs text-muted-foreground px-2 py-1">No categories yet</p>
                  ) : (
                    stats.map((s) => (
                      <button
                        key={s.category}
                        type="button"
                        className="w-full flex items-center justify-between gap-2 px-2 py-2 text-sm rounded hover:bg-muted"
                        onClick={() => {
                          enterCompany(company, s.open > 0 ? 'open' : 'completed');
                          enterCategory(s.category, s.open > 0 ? 'open' : 'completed');
                          setOpenDropdown(null);
                        }}
                      >
                        <span className="truncate font-medium">{s.category}</span>
                        <span className="tabular-nums text-muted-foreground shrink-0 font-medium">
                          {s.open === 0 ? 'Done' : `${s.open} open`} · {s.completed}/{s.total}
                        </span>
                      </button>
                    ))
                  )}
                </div>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}
