import { useCallback, useEffect, useState } from 'react';
import { FileText, Plus } from 'lucide-react';
import { ClientCompanyWorkbench } from './ClientCompanyWorkbench';
import {
  ApiError,
  activateClientSession,
  chooseJobWorkflow,
  getClientJobs,
  getClientPortalSummary,
  getMOIForms,
  getMyCompany,
  issueMoiForJob,
  issueMoiJob,
  recordClientJobProgress,
  updateMoiApprovalMode,
  type JobRequestResponse,
  type JobRequestUnitDto,
  type UserResponse,
} from '@/lib/api';
import {
  canClientStartMoi,
  isMoiRejected,
  canSignatoryStartMoi,
  signatoryCanSignMoi,
  canClientViewMoa,
  canClientViewMoi,
  canOpenMoiForm,
  isMoaClientSignoffPhase,
  signatoryCanSignMoa,
  unitHasMoaForm,
  unitHasMoiForm,
} from '@/lib/packageItemStatus';

interface ClientPortalProps {
  currentUser: UserResponse;
  onOpenMoiForm: (job: JobRequestResponse) => void;
  onOpenMoaForm: (job: JobRequestResponse) => void;
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
  if (isSignatoryView) {
    if (currentUser && signatoryCanSignMoi(job, currentUser, unit)) return true;
    if (canClientViewMoi(job, unit)) return true;
    if (currentUser && canSignatoryStartMoi(job, currentUser, unit) && canClientStartMoi(job, unit)) return true;
    if (signatoryCanSignMoa(job, currentUser ?? { name: '' }, unit)) return true;
    if (canClientViewMoa(job, unit)) return true;
    return false;
  }
  if (canClientStartMoi(job, unit)) return true;
  if (canClientViewMoi(job, unit) || canClientViewMoa(job, unit)) return true;
  if (unitHasMoiForm(job, unit) || unitHasMoaForm(job, unit)) return true;
  return canOpenMoiForm(job);
}

export function ClientPortal({ currentUser, onOpenMoiForm, onOpenMoaForm, refreshKey = 0, mode = 'admin' }: ClientPortalProps) {
  const isSignatoryView = mode === 'signatory';
  const [jobs, setJobs] = useState<JobRequestResponse[]>([]);
  const [teamHint, setTeamHint] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [issuing, setIssuing] = useState(false);
  const [showIssue, setShowIssue] = useState(false);
  const [moiApprovalMode, setMoiApprovalMode] = useState<'AllRequired' | 'AnyOne'>('AllRequired');
  const [savingMode, setSavingMode] = useState(false);
  const [issueForm, setIssueForm] = useState({
    service: '',
    typeOfDocument: '',
    documentTitle: '',
    adHoc: false,
  });
  const [workflowChoice, setWorkflowChoice] = useState<{
    job: JobRequestResponse;
    unit: JobRequestUnitDto;
  } | null>(null);
  const [bypassNote, setBypassNote] = useState('');
  const [savingChoice, setSavingChoice] = useState(false);

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

  const unitWorkflowMode = (job: JobRequestResponse, unit: JobRequestUnitDto) =>
    unit.workflowMode || job.workflowMode || '';

  const moiActionLabel = (job: JobRequestResponse, unit: JobRequestUnitDto) => {
    if (unitWorkflowMode(job, unit) === 'AdminBypass') return 'Sent to LGB';
    if (isMoiRejected(job, unit)) return 'Revise MOI';
    if (isSignatoryView && signatoryCanSignMoi(job, currentUser, unit)) return 'Sign MOI';
    const moiState = unit.moiWorkflowState ?? job.moiWorkflowState ?? '';
    if (unitHasMoiForm(job, unit) && moiState === 'Draft') return 'Continue MOI';
    if (canClientStartMoi(job, unit) && (!isSignatoryView || canSignatoryStartMoi(job, currentUser, unit))) {
      return unitWorkflowMode(job, unit) === 'MoiMoa' ? 'Start MOI' : 'Start / choose path';
    }
    return 'View MOI';
  };

  const moaActionLabel = (job: JobRequestResponse, unit: JobRequestUnitDto) => {
    if (signatoryCanSignMoa(job, currentUser, unit) || (!isSignatoryView && currentUser.needsMoa && isMoaClientSignoffPhase(job, unit))) {
      return 'Sign MOA';
    }
    return 'View MOA';
  };

  const showMoiAction = (job: JobRequestResponse, unit: JobRequestUnitDto) => {
    if (unitWorkflowMode(job, unit) === 'AdminBypass') return true;
    return canClientViewMoi(job, unit)
      || canClientStartMoi(job, unit)
      || (isSignatoryView && signatoryCanSignMoi(job, currentUser, unit))
      || (isSignatoryView && canSignatoryStartMoi(job, currentUser, unit) && canClientStartMoi(job, unit));
  };

  const showMoaAction = (job: JobRequestResponse, unit: JobRequestUnitDto) =>
    unitWorkflowMode(job, unit) !== 'AdminBypass'
    && unitHasMoaForm(job, unit)
    && (signatoryCanSignMoa(job, currentUser, unit)
      || canClientViewMoa(job, unit)
      || (!isSignatoryView && currentUser.needsMoa && isMoaClientSignoffPhase(job, unit)));

  const startMoiAfterChoice = async (job: JobRequestResponse, unit: JobRequestUnitDto) => {
    const updated = await issueMoiForJob(job.id, {
      service: job.service,
      typeOfDocument: job.service,
      requestedBy: currentUser.name,
      unitNumber: unit.unitNumber,
    });
    await load();
    const refreshedUnit = updated.units?.find((u) => u.unitNumber === unit.unitNumber) ?? unit;
    onOpenMoiForm(jobForUnit(updated, refreshedUnit));
  };

  const openMoiForm = async (job: JobRequestResponse, unit: JobRequestUnitDto) => {
    if (unitWorkflowMode(job, unit) === 'AdminBypass') {
      setError(unit.adminBypassNote || job.adminBypassNote || 'This task was sent to LGB without MOI/MOA.');
      return;
    }
    if (!canOpenJobForm(job, unit, isSignatoryView, currentUser)) return;
    const ctx = jobForUnit(job, unit);
    setError('');
    try {
      if (ctx.linkedFormId || unit.moiFormId || unitHasMoiForm(job, unit) || isMoiRejected(job, unit)) {
        onOpenMoiForm({
          ...ctx,
          linkedFormId: unit.moiFormId ?? (ctx.linkedFormKind === 'MOI' ? ctx.linkedFormId : undefined),
          linkedFormKind: 'MOI',
          hasMoiForm: true,
        });
        return;
      }

      if (canClientStartMoi(job, unit) && (!isSignatoryView || canSignatoryStartMoi(job, currentUser, unit))) {
        // D1: ask whether MOI/MOA is needed before first issue
        if (!unitWorkflowMode(job, unit)) {
          setBypassNote('');
          setWorkflowChoice({ job, unit });
          return;
        }
        await startMoiAfterChoice(job, unit);
        return;
      }

      const forms = await getMOIForms(job.id, unit.unitNumber);
      const linkedFormId = forms[0]?.id ?? unit.moiFormId ?? unit.linkedFormId;
      if (linkedFormId) {
        onOpenMoiForm({
          ...ctx,
          linkedFormId,
          linkedFormKind: 'MOI',
          hasMoiForm: true,
        });
      }
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to open MOI.');
    }
  };

  const confirmWorkflowMoiMoa = async () => {
    if (!workflowChoice) return;
    setSavingChoice(true);
    setError('');
    try {
      const { job, unit } = workflowChoice;
      await chooseJobWorkflow(job.id, { mode: 'MoiMoa', unitNumber: unit.unitNumber });
      setWorkflowChoice(null);
      await startMoiAfterChoice(job, unit);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to start MOI path.');
    } finally {
      setSavingChoice(false);
    }
  };

  const confirmWorkflowBypass = async () => {
    if (!workflowChoice) return;
    if (bypassNote.trim().length < 8) {
      setError('Please describe what LGB needs to do (at least 8 characters).');
      return;
    }
    setSavingChoice(true);
    setError('');
    try {
      const { job, unit } = workflowChoice;
      await chooseJobWorkflow(job.id, {
        mode: 'AdminBypass',
        unitNumber: unit.unitNumber,
        note: bypassNote.trim(),
      });
      setWorkflowChoice(null);
      setBypassNote('');
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to send request to LGB.');
    } finally {
      setSavingChoice(false);
    }
  };

  const renderFormActions = (job: JobRequestResponse, unit: JobRequestUnitDto) => {
    const ctx = jobForUnit(job, unit);
    const actions: { key: string; label: string; onClick: () => void; muted?: boolean }[] = [];

    if (showMoiAction(job, unit)) {
      actions.push({
        key: 'moi',
        label: moiActionLabel(job, unit),
        onClick: () => void openMoiForm(job, unit),
        muted: unitWorkflowMode(job, unit) === 'AdminBypass',
      });
    }
    if (showMoaAction(job, unit)) {
      actions.push({
        key: 'moa',
        label: moaActionLabel(job, unit),
        onClick: () => onOpenMoaForm(ctx),
      });
    }

    if (actions.length === 0) return <span className="text-sm text-muted-foreground">—</span>;

    return (
      <div className="flex flex-col items-start gap-1">
        {actions.map((action) => (
          <button
            key={action.key}
            type="button"
            onClick={action.onClick}
            className={`inline-flex items-center gap-1 text-sm hover:underline ${
              action.muted ? 'text-muted-foreground' : 'text-primary'
            }`}
          >
            <FileText className="w-3.5 h-3.5" />
            {action.label}
          </button>
        ))}
      </div>
    );
  };

  const handleSchedule = async (job: JobRequestResponse, unitNumber: number, isoValue: string) => {
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

  const handleActivateSession = async (job: JobRequestResponse) => {
    setError('');
    try {
      const updated = await activateClientSession(job.id);
      setJobs((prev) => prev.map((j) => (j.id === updated.id ? updated : j)));
      return updated;
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to start session.');
      throw err;
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
      if (job.linkedFormId) onOpenMoiForm(job);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to issue MOI.');
    } finally {
      setIssuing(false);
    }
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

  const openMoaForm = (job: JobRequestResponse, unit: JobRequestUnitDto) => {
    if (!canOpenJobForm(job, unit, isSignatoryView, currentUser)) return;
    onOpenMoaForm(jobForUnit(job, unit));
  };

  const openPrimaryForm = (job: JobRequestResponse, unit: JobRequestUnitDto) => {
    if (showMoaAction(job, unit) && signatoryCanSignMoa(job, currentUser, unit)) {
      openMoaForm(job, unit);
      return;
    }
    if (showMoiAction(job, unit)) {
      void openMoiForm(job, unit);
      return;
    }
    if (showMoaAction(job, unit)) {
      openMoaForm(job, unit);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h2 className="text-xl font-semibold">{isSignatoryView ? 'My documents' : 'Client portal'}</h2>
          <p className="text-sm text-muted-foreground mt-1">
            {isSignatoryView
              ? 'Pick a company, work one item at a time, and use History for completed work. Yellow borders mean something needs a signature.'
              : 'Open a company tile to work by category. Yellow borders mean a signature is pending. Green badge opens completed history.'}
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
                placeholder="e.g. Resolution for new director appointment"
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

      <ClientCompanyWorkbench
        jobs={jobs}
        currentUser={currentUser}
        isSignatoryView={isSignatoryView}
        loading={loading}
        onOpenPrimary={openPrimaryForm}
        renderFormActions={renderFormActions}
        onSchedule={isSignatoryView ? undefined : (job, unitNumber, iso) => void handleSchedule(job, unitNumber, iso)}
        onMarkDone={isSignatoryView ? undefined : (job, unitNumber) => void handleMarkDone(job, unitNumber)}
        onUndo={isSignatoryView ? undefined : (job, unitNumber) => void handleUndo(job, unitNumber)}
        onActivateSession={(job) => handleActivateSession(job)}
      />

      {workflowChoice && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="w-full max-w-md rounded-xl bg-card border border-border p-5 shadow-lg space-y-4">
            <div>
              <h3 className="text-lg font-semibold">How should LGB handle this?</h3>
              <p className="text-sm text-muted-foreground mt-1">
                {workflowChoice.job.service}
                {(workflowChoice.job.totalQty ?? 1) > 1
                  ? ` — session ${workflowChoice.unit.unitNumber}`
                  : ''}
              </p>
            </div>
            <p className="text-sm text-foreground/80">
              Choose MOI/MOA when this needs the formal instruction and resolution workflow.
              Or send a note straight to LGB if that process is not needed.
            </p>
            <label className="block text-sm space-y-1">
              <span className="text-muted-foreground">Note for LGB (required for the bypass option)</span>
              <textarea
                className="w-full min-h-[88px] px-3 py-2 border border-border rounded-lg text-sm"
                value={bypassNote}
                onChange={(e) => setBypassNote(e.target.value)}
                placeholder="e.g. Please update registered address — documents attached by email."
              />
            </label>
            <div className="flex flex-col gap-2">
              <button
                type="button"
                disabled={savingChoice}
                onClick={() => void confirmWorkflowMoiMoa()}
                className="px-4 py-2 bg-primary text-primary-foreground rounded-lg text-sm disabled:opacity-50"
              >
                {savingChoice ? 'Working…' : 'Needs MOI / MOA'}
              </button>
              <button
                type="button"
                disabled={savingChoice}
                onClick={() => void confirmWorkflowBypass()}
                className="px-4 py-2 border border-border rounded-lg text-sm disabled:opacity-50"
              >
                No MOI/MOA — send note to LGB
              </button>
              <button
                type="button"
                disabled={savingChoice}
                onClick={() => {
                  setWorkflowChoice(null);
                  setBypassNote('');
                }}
                className="px-4 py-2 text-sm text-muted-foreground hover:underline"
              >
                Cancel
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
