import { Fragment, useCallback, useEffect, useMemo, useState } from 'react';
import { Check, ChevronDown, ChevronRight, FileText, Plus, Undo2 } from 'lucide-react';
import { JobItemFolderPanel } from './JobItemFolderPanel';
import { DateInput } from './DateInput';
import { formatDateDisplay, parseDateToIso } from '@/lib/dates';
import {
  ApiError,
  getClientJobs,
  getClientPortalSummary,
  getMOIForms,
  getMyCompany,
  issueMoiForJob,
  issueMoiJob,
  recordClientJobProgress,
  updateMoiApprovalMode,
  type ClientPortalSummaryDto,
  type JobRequestResponse,
  type JobRequestUnitDto,
  type UserResponse,
} from '@/lib/api';
import {
  canClientStartMoi,
  isMoiRejected,
  canSignatoryStartMoi,
  signatoryCanSignMoi,
  canOpenMoaForm,
  canOpenMoiForm,
  displayStatusKeyForUnit,
  displayStatusLabelForUnit,
  unitHasMoaForm,
  unitHasMoiForm,
  packageItemStatusBadgeClass,
} from '@/lib/packageItemStatus';
import { ALL_SERVICES, resolveServiceCategory, SERVICE_CATEGORY_ORDER } from '@/lib/serviceCategory';

interface ClientPortalProps {
  currentUser: UserResponse;
  onOpenForm: (job: JobRequestResponse) => void;
  refreshKey?: number;
  mode?: 'admin' | 'signatory';
}

function jobForUnit(job: JobRequestResponse, unit: JobRequestUnitDto): JobRequestResponse {
  if ((job.totalQty ?? 1) <= 1) return job;
  return {
    ...job,
    linkedFormId: unit.linkedFormId,
    linkedFormKind: unit.linkedFormKind,
    hasMoiForm: unit.hasMoiForm,
    hasMoaForm: unit.hasMoaForm,
    moiWorkflowState: unit.moiWorkflowState,
    activeUnitNumber: unit.unitNumber,
  };
}

function canOpenJobForm(
  job: JobRequestResponse,
  unit: JobRequestUnitDto,
  isSignatoryView: boolean,
  currentUser?: UserResponse,
): boolean {
  const ctx = jobForUnit(job, unit);
  if (isSignatoryView) {
    if (currentUser && signatoryCanSignMoi(job, currentUser, unit)) return true;
    if (Boolean(ctx.linkedFormId)) return true;
    if (currentUser && canSignatoryStartMoi(job, currentUser) && canClientStartMoi(job, unit)) return true;
    return Boolean(ctx.linkedFormKind === 'MOA' && ctx.linkedFormId);
  }
  if (canClientStartMoi(job, unit)) return true;
  if (unitHasMoiForm(job, unit) || unitHasMoaForm(job, unit)) return true;
  return canOpenMoiForm(job) || Boolean(ctx.linkedFormId);
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

function CategoryCounter({ label, pending, inProgress, completed, total, highlight = false, onClick }: {
  label: string;
  pending: number;
  inProgress: number;
  completed: number;
  total: number;
  highlight?: boolean;
  onClick?: () => void;
}) {
  if (total === 0) return null;
  const open = pending + inProgress;
  const Tag = onClick ? 'button' : 'div';
  return (
    <Tag
      type={onClick ? 'button' : undefined}
      onClick={onClick}
      className={`bg-card border rounded-lg p-4 text-left w-full ${
        highlight ? 'border-primary ring-1 ring-primary/30' : 'border-border'
      } ${onClick ? 'hover:border-primary/60 transition-colors cursor-pointer' : ''}`}
    >
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className="text-2xl font-semibold mt-1">{completed}<span className="text-base font-normal text-muted-foreground"> / {total}</span></p>
      <p className="text-xs mt-2 text-muted-foreground">
        {open} open · {completed} done
      </p>
      <div className="mt-2 h-1.5 bg-muted rounded-full overflow-hidden">
        <div
          className="h-full bg-green-600 rounded-full"
          style={{ width: total ? `${(completed / total) * 100}%` : '0%' }}
        />
      </div>
    </Tag>
  );
}

export function ClientPortal({ currentUser, onOpenForm, refreshKey = 0, mode = 'admin' }: ClientPortalProps) {
  const isSignatoryView = mode === 'signatory';
  const [jobs, setJobs] = useState<JobRequestResponse[]>([]);
  const [summary, setSummary] = useState<ClientPortalSummaryDto | null>(null);
  const [teamHint, setTeamHint] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [issuing, setIssuing] = useState(false);
  const [showIssue, setShowIssue] = useState(false);
  const [moiApprovalMode, setMoiApprovalMode] = useState<'AllRequired' | 'AnyOne'>('AllRequired');
  const [savingMode, setSavingMode] = useState(false);
  const [expandedUnits, setExpandedUnits] = useState<Set<string>>(new Set());
  const [activeCategory, setActiveCategory] = useState<string>(ALL_SERVICES);
  const [issueForm, setIssueForm] = useState({
    service: '',
    typeOfDocument: '',
    documentTitle: '',
    adHoc: false,
  });

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const [data, portalSummary, company] = await Promise.all([
        getClientJobs(true),
        getClientPortalSummary(),
        isSignatoryView ? Promise.resolve(null) : getMyCompany(),
      ]);
      setJobs(data);
      setSummary(portalSummary);
      if (company?.moiApprovalMode) {
        setMoiApprovalMode(company.moiApprovalMode);
      }
      if (isSignatoryView) {
        setTeamHint('');
      } else if (portalSummary.teamMembers === 0) {
        setTeamHint('Invite additional client admins under the Team tab.');
      } else {
        setTeamHint('');
      }
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to load jobs.');
    } finally {
      setLoading(false);
    }
  }, [currentUser.userId, isSignatoryView]);

  useEffect(() => {
    void load();
  }, [load, refreshKey]);

  const categoryTabs = useMemo(() => {
    const present = new Set(
      jobs
        .filter((j) => j.taskType === 'Service')
        .map((j) => resolveServiceCategory(j.service)),
    );
    return SERVICE_CATEGORY_ORDER.filter((c) => c === ALL_SERVICES || present.has(c));
  }, [jobs]);

  const filteredJobs = useMemo(() => {
    if (activeCategory === ALL_SERVICES) return jobs;
    return jobs.filter((j) => {
      if (j.taskType !== 'Service') return false;
      return resolveServiceCategory(j.service) === activeCategory;
    });
  }, [jobs, activeCategory]);

  const formActionLabel = (job: JobRequestResponse, unit: JobRequestUnitDto) => {
    const ctx = jobForUnit(job, unit);
    if (isMoiRejected(job, unit))
      return 'Revise MOI';
    if (isSignatoryView && signatoryCanSignMoi(job, currentUser, unit))
      return 'Sign MOI';
    if (canClientStartMoi(job, unit) && (!isSignatoryView || canSignatoryStartMoi(job, currentUser)))
      return 'Start MOI';
    if (ctx.linkedFormKind === 'MOA')
      return 'Sign MOA';
    return 'Open MOI';
  };

  const handleSchedule = async (job: JobRequestResponse, unitNumber: number, isoValue: string) => {
    const unit = jobUnits(job).find((u) => u.unitNumber === unitNumber);
    const savedIso = unit?.scheduledDate ? parseDateToIso(unit.scheduledDate) ?? '' : '';
    if (isoValue === savedIso) return;

    setError('');
    try {
      await recordClientJobProgress(job.id, { unitNumber, scheduledDate: isoValue });
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to save date.');
    }
  };

  const handleMarkDone = async (job: JobRequestResponse, unitNumber: number) => {
    setError('');
    try {
      await recordClientJobProgress(job.id, { unitNumber, markUnitComplete: true });
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to mark complete.');
    }
  };

  const handleUndo = async (job: JobRequestResponse, unitNumber: number) => {
    setError('');
    try {
      await recordClientJobProgress(job.id, { unitNumber, markUnitIncomplete: true });
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to undo completion.');
    }
  };

  const handleIssue = async (e: React.FormEvent) => {
    e.preventDefault();
    setIssuing(true);
    setError('');
    try {
      const job = await issueMoiJob({
        service: issueForm.service || issueForm.typeOfDocument || 'MOI',
        typeOfDocument: issueForm.typeOfDocument,
        documentTitle: issueForm.documentTitle,
        adHoc: issueForm.adHoc,
        initiationDate: new Date().toISOString().split('T')[0],
        requestedBy: currentUser.name,
      });
      setShowIssue(false);
      setIssueForm({ service: '', typeOfDocument: '', documentTitle: '', adHoc: false });
      await load();
      if (job.linkedFormId) onOpenForm(job);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to issue MOI.');
    } finally {
      setIssuing(false);
    }
  };

  const unitKey = (jobId: number, unitNumber: number) => `${jobId}-${unitNumber}`;

  const toggleExpanded = (jobId: number, unitNumber: number) => {
    const key = unitKey(jobId, unitNumber);
    setExpandedUnits((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  };

  const handleMoiApprovalModeChange = async (mode: 'AllRequired' | 'AnyOne') => {
    if (mode === moiApprovalMode) return;
    setSavingMode(true);
    setError('');
    try {
      const updated = await updateMoiApprovalMode(mode);
      setMoiApprovalMode(updated.moiApprovalMode ?? mode);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to update MOI signing policy.');
    } finally {
      setSavingMode(false);
    }
  };

  const showFormAction = (job: JobRequestResponse, unit: JobRequestUnitDto) =>
    unitHasMoiForm(job, unit)
    || unitHasMoaForm(job, unit)
    || canClientStartMoi(job, unit)
    || (isSignatoryView && signatoryCanSignMoi(job, currentUser, unit))
    || (isSignatoryView && canSignatoryStartMoi(job, currentUser) && canClientStartMoi(job, unit));

  const openJobForm = async (job: JobRequestResponse, unit: JobRequestUnitDto) => {
    if (!canOpenJobForm(job, unit, isSignatoryView, currentUser)) return;
    const ctx = jobForUnit(job, unit);
    setError('');
    try {
      if (job.taskType === 'MOA' || ctx.linkedFormKind === 'MOA') {
        if (canOpenMoaForm(job) && ctx.linkedFormId) {
          onOpenForm(ctx);
        }
        return;
      }

      if (!canOpenMoiForm(job)) return;

      if (ctx.linkedFormId || unitHasMoiForm(job, unit) || isMoiRejected(job, unit)) {
        onOpenForm(ctx);
        return;
      }

      if (canClientStartMoi(job, unit)) {
        const updated = await issueMoiForJob(job.id, {
          service: job.service,
          typeOfDocument: job.service,
          requestedBy: currentUser.name,
          unitNumber: unit.unitNumber,
        });
        await load();
        const refreshedUnit = updated.units?.find((u) => u.unitNumber === unit.unitNumber) ?? unit;
        onOpenForm(jobForUnit(updated, refreshedUnit));
        return;
      }

      const forms = await getMOIForms(job.id, unit.unitNumber);
      if (forms[0]) {
        onOpenForm({
          ...ctx,
          linkedFormId: forms[0].id,
          linkedFormKind: 'MOI',
        });
      }
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to open form.');
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h2 className="text-xl font-semibold">{isSignatoryView ? 'My documents' : 'Client portal'}</h2>
          <p className="text-sm text-muted-foreground mt-1">
            {isSignatoryView
              ? `${(currentUser.accessibleCompanies?.length ?? 0) > 1
                ? currentUser.accessibleCompanies!.map((c) => c.company).join(', ')
                : currentUser.customerName ?? 'Your company'} — each package item has its own MOI/MOA; open a line to fill or sign.`
              : `${currentUser.customerName ?? 'Your company'} — each package line has its own MOI/MOA workflow; set dates and track every item.`}
          </p>
        </div>
        {!isSignatoryView && (
          <button
            type="button"
            onClick={() => setShowIssue(true)}
            className="flex items-center gap-2 px-4 py-2 bg-primary text-primary-foreground rounded-lg text-sm"
          >
            <Plus className="w-4 h-4" />
            Issue MOI
          </button>
        )}
      </div>

      {summary?.categoryProgress && summary.categoryProgress.length > 0 && (
        <div className="space-y-3">
          <h3 className="text-sm font-medium text-muted-foreground">Package services progress</h3>
          <div className="grid grid-cols-2 md:grid-cols-4 lg:grid-cols-5 gap-4">
            {summary.categoryProgress.map((cat) => (
              <CategoryCounter
                key={cat.category}
                label={cat.category}
                pending={cat.pending}
                inProgress={cat.inProgress}
                completed={cat.completed}
                total={cat.total}
                highlight={activeCategory === cat.category}
                onClick={() => setActiveCategory(cat.category)}
              />
            ))}
          </div>
          {categoryTabs.length > 1 && (
            <div className="flex flex-wrap gap-2">
              {categoryTabs.map((tab) => (
                <button
                  key={tab}
                  type="button"
                  onClick={() => setActiveCategory(tab)}
                  className={`px-3 py-1.5 rounded-full text-xs border transition-colors ${
                    activeCategory === tab
                      ? 'bg-primary text-primary-foreground border-primary'
                      : 'bg-card border-border text-muted-foreground hover:border-primary/50'
                  }`}
                >
                  {tab}
                </button>
              ))}
            </div>
          )}
        </div>
      )}

      {!isSignatoryView && (
        <div className="bg-card border border-border rounded-lg p-4 space-y-3">
          <div>
            <h3 className="text-sm font-medium">MOI signing policy</h3>
            <p className="text-xs text-muted-foreground mt-1">
              Choose how many client approvers must sign each MOI before it is released to LGB.
              MOA always requires every listed MOA signatory.
            </p>
          </div>
          <div className="flex flex-col sm:flex-row gap-3 text-sm">
            <label className={`flex items-start gap-2 border rounded-lg px-3 py-2 cursor-pointer ${moiApprovalMode === 'AllRequired' ? 'border-primary bg-primary/5' : 'border-border'}`}>
              <input
                type="radio"
                name="moiApprovalMode"
                className="mt-1"
                checked={moiApprovalMode === 'AllRequired'}
                disabled={savingMode}
                onChange={() => void handleMoiApprovalModeChange('AllRequired')}
              />
              <span>
                <span className="font-medium">All approvers must sign</span>
                <span className="block text-xs text-muted-foreground">Every MOI approver signs before LGB intake.</span>
              </span>
            </label>
            <label className={`flex items-start gap-2 border rounded-lg px-3 py-2 cursor-pointer ${moiApprovalMode === 'AnyOne' ? 'border-primary bg-primary/5' : 'border-border'}`}>
              <input
                type="radio"
                name="moiApprovalMode"
                className="mt-1"
                checked={moiApprovalMode === 'AnyOne'}
                disabled={savingMode}
                onChange={() => void handleMoiApprovalModeChange('AnyOne')}
              />
              <span>
                <span className="font-medium">Any one approver can sign</span>
                <span className="block text-xs text-muted-foreground">One MOI approver is enough to release to LGB.</span>
              </span>
            </label>
          </div>
        </div>
      )}

      {teamHint && (
        <p className="text-sm border border-amber-200 bg-amber-50 text-amber-900 rounded-lg px-4 py-3">{teamHint}</p>
      )}
      {error && <p className="text-sm text-destructive">{error}</p>}

      {showIssue && (
        <form onSubmit={(e) => void handleIssue(e)} className="bg-card border border-border rounded-lg p-6 space-y-4">
          <h3 className="font-medium">New MOI</h3>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label className="block text-sm mb-1">Service / document type *</label>
              <input
                required
                className="w-full px-3 py-2 border border-border rounded-lg"
                value={issueForm.typeOfDocument}
                onChange={(e) => setIssueForm({ ...issueForm, typeOfDocument: e.target.value, service: e.target.value })}
              />
            </div>
            <div>
              <label className="block text-sm mb-1">Document title</label>
              <input
                className="w-full px-3 py-2 border border-border rounded-lg"
                value={issueForm.documentTitle}
                onChange={(e) => setIssueForm({ ...issueForm, documentTitle: e.target.value })}
              />
            </div>
          </div>
          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={issueForm.adHoc}
              onChange={(e) => setIssueForm({ ...issueForm, adHoc: e.target.checked })}
            />
            Ad-hoc service (not tied to package)
          </label>
          <div className="flex gap-2">
            <button type="submit" disabled={issuing} className="px-4 py-2 bg-primary text-primary-foreground rounded-lg text-sm disabled:opacity-50">
              {issuing ? 'Issuing…' : 'Issue MOI'}
            </button>
            <button type="button" onClick={() => setShowIssue(false)} className="px-4 py-2 border border-border rounded-lg text-sm">
              Cancel
            </button>
          </div>
        </form>
      )}

      <div className="bg-card border border-border rounded-lg overflow-hidden">
        {loading ? (
          <p className="p-6 text-sm text-muted-foreground">Loading jobs…</p>
        ) : filteredJobs.length === 0 ? (
          <p className="p-6 text-sm text-muted-foreground">No jobs in this category yet.</p>
        ) : (
          <table className="w-full text-sm">
            <thead className="bg-muted/50">
              <tr>
                <th className="px-4 py-3 w-8" />
                <th className="px-4 py-3 text-left">Task</th>
                <th className="px-4 py-3 text-left">Service</th>
                <th className="px-4 py-3 text-left">Target date</th>
                <th className="px-4 py-3 text-left">Status</th>
                <th className="px-4 py-3 text-left">Form</th>
                <th className="px-4 py-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody>
              {filteredJobs.map((job) => {
                const units = jobUnits(job);
                const label = job.taskType === 'Service' ? job.service : job.taskType;
                const isMultiSession = (job.totalQty ?? 1) > 1 && job.taskType === 'Service';

                const renderUnitRow = (unit: JobRequestUnitDto) => {
                  const isExpanded = expandedUnits.has(unitKey(job.id, unit.unitNumber));
                  const taskClickable = showFormAction(job, unit) || canOpenJobForm(job, unit, isSignatoryView, currentUser);
                  return (
                    <Fragment key={`${job.id}-${unit.unitNumber}`}>
                      <tr className={`border-t border-border ${isMultiSession ? 'bg-muted/10' : ''}`}>
                        <td className="px-2 py-3 align-top">
                          <button
                            type="button"
                            title={isExpanded ? 'Hide folder' : 'Show session folder'}
                            onClick={() => toggleExpanded(job.id, unit.unitNumber)}
                            className="p-1 rounded hover:bg-muted text-muted-foreground"
                          >
                            {isExpanded ? <ChevronDown className="w-4 h-4" /> : <ChevronRight className="w-4 h-4" />}
                          </button>
                        </td>
                        <td className={`px-4 py-3 ${isMultiSession ? 'pl-8' : ''}`}>
                          {taskClickable ? (
                            <button
                              type="button"
                              onClick={() => void openJobForm(job, unit)}
                              className="text-sm font-medium text-primary hover:underline text-left"
                            >
                              {isMultiSession && (
                                <span className="text-xs text-muted-foreground mr-2">#{unit.unitNumber}</span>
                              )}
                              {label}
                            </button>
                          ) : (
                            <span className="text-sm font-medium">
                              {isMultiSession && (
                                <span className="text-xs text-muted-foreground mr-2">#{unit.unitNumber}</span>
                              )}
                              {label}
                            </span>
                          )}
                        </td>
                        <td className="px-4 py-3 text-sm">
                          {isMultiSession ? `Session ${unit.unitNumber}` : 'Package item'}
                        </td>
                        <td className="px-4 py-3">
                          {isSignatoryView ? (
                            <span className="text-sm text-muted-foreground">{formatDateDisplay(unit.scheduledDate) || '—'}</span>
                          ) : (
                            <DateInput
                              value={unit.scheduledDate}
                              onChange={(iso) => void handleSchedule(job, unit.unitNumber, iso)}
                            />
                          )}
                        </td>
                        <td className="px-4 py-3">
                          <span className={`text-xs px-2 py-0.5 rounded-full ${packageItemStatusBadgeClass(displayStatusKeyForUnit(job, unit))}`}>
                            {displayStatusLabelForUnit(job, unit)}
                          </span>
                        </td>
                        <td className="px-4 py-3">
                          {showFormAction(job, unit) ? (
                            <button
                              type="button"
                              onClick={() => void openJobForm(job, unit)}
                              className="inline-flex items-center gap-1 text-sm text-primary hover:underline"
                            >
                              <FileText className="w-3.5 h-3.5" />
                              {formActionLabel(job, unit)}
                            </button>
                          ) : (
                            <span className="text-sm text-muted-foreground">—</span>
                          )}
                        </td>
                        <td className="px-4 py-3 text-right">
                          <div className="flex items-center justify-end gap-2 flex-wrap">
                            {!isSignatoryView && unit.status === 'Completed' ? (
                              <button
                                type="button"
                                title="Undo completion"
                                onClick={() => void handleUndo(job, unit.unitNumber)}
                                className="inline-flex items-center gap-1 px-2 py-1 text-xs border border-border rounded hover:bg-muted"
                              >
                                <Undo2 className="w-3 h-3" />
                                Undo
                              </button>
                            ) : !isSignatoryView ? (
                              <button
                                type="button"
                                title="Mark done"
                                onClick={() => void handleMarkDone(job, unit.unitNumber)}
                                className="inline-flex items-center gap-1 px-2 py-1 text-xs bg-green-600 text-white rounded hover:bg-green-700"
                              >
                                <Check className="w-3 h-3" />
                                Done
                              </button>
                            ) : null}
                            {showFormAction(job, unit) && (
                              <button
                                type="button"
                                onClick={() => void openJobForm(job, unit)}
                                className="inline-flex items-center gap-1 text-sm text-primary hover:underline"
                              >
                                <FileText className="w-4 h-4" />
                                {formActionLabel(job, unit)}
                              </button>
                            )}
                          </div>
                        </td>
                      </tr>
                      {isExpanded && (
                        <tr className="border-t border-border bg-muted/20">
                          <td colSpan={7} className="px-4 py-3">
                            <JobItemFolderPanel
                              job={jobForUnit(job, unit)}
                              unitNumber={unit.unitNumber}
                              onOpenMoi={() => void openJobForm(job, unit)}
                              onOpenMoa={() => void openJobForm(job, unit)}
                            />
                          </td>
                        </tr>
                      )}
                    </Fragment>
                  );
                };

                if (isMultiSession) {
                  return (
                    <Fragment key={job.id}>
                      <tr className="border-t border-border bg-muted/20">
                        <td colSpan={7} className="px-4 py-3 font-semibold text-sm">
                          {label}
                        </td>
                      </tr>
                      {units.map((unit) => renderUnitRow(unit))}
                    </Fragment>
                  );
                }

                return <Fragment key={job.id}>{units.map((unit) => renderUnitRow(unit))}</Fragment>;
              })}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
