import { Download, Send, X, FileText } from 'lucide-react';
import { SignatureCapture, type SignaturePayload } from './SignatureCapture';
import { ClientSignOffTrail } from './ClientSignOffTrail';
import { useEffect, useMemo, useRef, useState } from 'react';
import { DateInput } from './DateInput';
import { JobItemDocumentsSection } from './JobItemDocumentsSection';
import {
  adminOverrideMoaStep,
  ApiError,
  approveMoaWorkflowStep,
  downloadMoaExportPack,
  getInternalDirectoryUsers,
  resolveFormTemplate,
  saveBlobAsFile,
  type ClientApprovalDto,
  type FormTemplateDto,
  type MoaPackChecklistDto,
  type WorkflowInstanceDto,
} from '@/lib/api';
import {
  emptyMoaFormFields,
  hydrateMoaFormFields,
  SHOW_MOA_SEQUENTIAL_WORKFLOW,
  type MoaFormFields,
} from '@/lib/moaFormState';
import { resolveMoaTemplateSections } from '@/lib/moaTemplateSections';
import { isMoaClientSignoffHandoff } from '@/lib/packageItemStatus';
import { formatPendingApproverList, signatoryHasPendingApproval, signatoryMatchesApproverName } from '@/lib/signatoryApprovers';
import { hydrateMoiFormFields } from '@/lib/moiFormState';
import { formatDateDisplay } from '@/lib/dates';

interface Customer {
  id: number;
  company: string;
  package: string;
  hasLoa?: boolean;
  moaWorkflowTemplateCode?: string;
  accountHolders: { id: number; name: string; moi: boolean; moiApproval: boolean; moa: boolean }[];
}

interface User {
  id: number;
  name: string;
}

const emptyPackChecklist = (): MoaPackChecklistDto => ({
  internalChecklistA: false,
  internalChecklistB: false,
  cleanAgreementAttached: false,
  shareholdingTableAttached: false,
  ssmRegistrationNo: '',
  ssmNewRegistrationNo: '',
  ssmEntityType: '',
  ssmStatus: '',
  ssmAsAtDate: '',
});

const MOI_SOURCE_DOC_FOLDERS = ['supporting', 'moi'] as const;
const MOA_ATTACHMENT_FOLDERS = ['moa', 'supporting'] as const;

interface MOAFormModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (data: any) => Promise<{ id: number; jobId: number; unitNumber?: number; updatedAt?: string } | void>;
  onSaveDraft?: (data: Record<string, unknown>) => Promise<{ id: number; jobId: number; unitNumber?: number; updatedAt?: string }>;
  onDirtyChange?: (dirty: boolean) => void;
  onStartWorkflow?: (payload: Record<string, unknown>) => void | Promise<void>;
  onClientApprove?: (moaFormId: number, payload: { comments: string; signatureFileName?: string; signatureDataUrl?: string }) => void;
  onClientReject?: (moaFormId: number, reason: string) => void;
  onSharonApprove?: (jobId: number) => void;
  onSharonReject?: (jobId: number, reason: string) => void;
  onSendToClient?: (jobId: number) => void;
  onSubmitForAdminReview?: (jobId: number) => void | Promise<void>;
  canSubmitForAdminReview?: boolean;
  canMarkExecutionComplete?: boolean;
  onMarkExecutionComplete?: (jobId: number) => void | Promise<void>;
  jobHandoffStatus?: string;
  viewMode?: boolean;
  initialData?: any;
  moiData?: any;
  users?: User[];
  customers: Customer[];
  userIsAdmin?: boolean;
  canApproveMoa?: boolean;
  isClientUser?: boolean;
  needsMoa?: boolean;
  isInternalSignatory?: boolean;
  currentUserName?: string;
  signatoryHolderNames?: string[];
  linkedMoiData?: Record<string, unknown> | null;
  jobId?: number;
  unitNumber?: number;
  allowMoaAttachments?: boolean;
  onViewLinkedMoi?: () => void;
}

export function MOAFormModal({
  isOpen,
  onClose,
  onSubmit,
  onSaveDraft,
  onDirtyChange,
  onStartWorkflow,
  onClientApprove,
  onClientReject,
  onSharonApprove,
  onSharonReject,
  onSendToClient,
  onSubmitForAdminReview,
  canSubmitForAdminReview = false,
  canMarkExecutionComplete = false,
  onMarkExecutionComplete,
  jobHandoffStatus = '',
  viewMode = false,
  initialData,
  moiData,
  users = [],
  customers,
  userIsAdmin = false,
  canApproveMoa = false,
  isClientUser = false,
  needsMoa = false,
  isInternalSignatory = false,
  currentUserName = '',
  signatoryHolderNames = [],
  linkedMoiData,
  jobId,
  unitNumber,
  allowMoaAttachments = false,
  onViewLinkedMoi,
}: MOAFormModalProps) {
  const [formTemplate, setFormTemplate] = useState<FormTemplateDto | null>(null);
  const [workflow, setWorkflow] = useState<WorkflowInstanceDto | null>(null);
  const sequentialWorkflowUi = SHOW_MOA_SEQUENTIAL_WORKFLOW && Boolean(workflow);
  const pendingApprovers: string[] = initialData?.pendingApprovers ?? [];
  const requiredApprovers: string[] = initialData?.requiredApprovers ?? [];
  const clientApprovals: ClientApprovalDto[] = initialData?.clientApprovals ?? [];
  const nameMatches = (n: string) => signatoryMatchesApproverName(n, currentUserName, signatoryHolderNames);
  const alreadySignedMoa = clientApprovals.some((a) => a.accountHolderName && nameMatches(a.accountHolderName));
  const isMoaSignoffPhase = isMoaClientSignoffHandoff(jobHandoffStatus);
  const canSignMoa = Boolean(
    initialData?.id
    && currentUserName
    && viewMode
    && isMoaSignoffPhase
    && !alreadySignedMoa
    && (
      (isClientUser && needsMoa && (
        signatoryHasPendingApproval(pendingApprovers, currentUserName, signatoryHolderNames)
        || requiredApprovers.some(nameMatches)
        || (requiredApprovers.length === 0 && pendingApprovers.length === 0)
      ))
      || (!isClientUser && (isInternalSignatory || canApproveMoa))
    ),
  );
  const [packChecklist, setPackChecklist] = useState<MoaPackChecklistDto>(emptyPackChecklist);
  const [packErrors, setPackErrors] = useState<string[]>([]);
  const [submitError, setSubmitError] = useState('');
  const packErrorsRef = useRef<HTMLDivElement | null>(null);
  const [stepComments, setStepComments] = useState('');
  const [clientSignComments, setClientSignComments] = useState('');
  const [rejectReason, setRejectReason] = useState('');
  const [sharonRejectReason, setSharonRejectReason] = useState('');
  const [signature, setSignature] = useState<SignaturePayload | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [documentsRefreshKey, setDocumentsRefreshKey] = useState(0);
  const [exportingPack, setExportingPack] = useState(false);
  const [exportPackError, setExportPackError] = useState('');
  const [autoSaveState, setAutoSaveState] = useState<'idle' | 'pending' | 'saving' | 'saved' | 'error'>('idle');
  const [formData, setFormData] = useState<MoaFormFields>(emptyMoaFormFields());
  const selectedCustomer = customers.find((c) => c.company === formData.company);
  const templateSections = useMemo(
    () => resolveMoaTemplateSections(selectedCustomer?.moaWorkflowTemplateCode, {
      financeRelated: formData.financeRelated,
      bankSignatoryMatter: formData.bankSignatoryMatter,
      shareMovement: formData.shareMovement,
      hasLoa: selectedCustomer?.hasLoa,
    }),
    [
      selectedCustomer?.moaWorkflowTemplateCode,
      selectedCustomer?.hasLoa,
      formData.financeRelated,
      formData.bankSignatoryMatter,
      formData.shareMovement,
      formData.company,
    ],
  );
  const [pickerUsers, setPickerUsers] = useState<User[]>(users);
  const hydratingRef = useRef(true);
  const autoSaveTimerRef = useRef<number | null>(null);
  const autoSaveInFlightRef = useRef<Promise<{ id: number; jobId: number; unitNumber?: number; updatedAt?: string }> | null>(null);
  const hydrateSessionKeyRef = useRef('');
  const savedUpdatedAtRef = useRef<string | undefined>(undefined);
  const moaFormIdRef = useRef<number | undefined>(undefined);
  const onDirtyChangeRef = useRef(onDirtyChange);
  const customersRef = useRef(customers);
  onDirtyChangeRef.current = onDirtyChange;
  customersRef.current = customers;

  useEffect(() => {
    setPickerUsers(users);
  }, [users]);

  useEffect(() => {
    if (!isOpen || viewMode || isClientUser || pickerUsers.length > 0) return;
    void getInternalDirectoryUsers()
      .then((data) => setPickerUsers(data.map((u) => ({ id: u.userId, name: u.name }))))
      .catch(() => undefined);
  }, [isOpen, viewMode, isClientUser, pickerUsers.length]);

  useEffect(() => {
    if (!isOpen) {
      hydrateSessionKeyRef.current = '';
      moaFormIdRef.current = undefined;
      return;
    }

    const source = initialData ?? moiData;
    const sessionKey = [
      source?.jobId ?? 'job',
      source?.unitNumber ?? source?.activeUnitNumber ?? 'unit',
    ].join(':');

    const nextUpdatedAt = source?.updatedAt as string | undefined;
    const nextFormId = (source?.id ?? moiData?.id) as number | undefined;
    if (nextFormId) moaFormIdRef.current = nextFormId;

    if (sessionKey === hydrateSessionKeyRef.current) {
      if (nextUpdatedAt) savedUpdatedAtRef.current = nextUpdatedAt;
      return;
    }
    hydrateSessionKeyRef.current = sessionKey;
    if (nextUpdatedAt) savedUpdatedAtRef.current = nextUpdatedAt;

    hydratingRef.current = true;
    onDirtyChangeRef.current?.(false);

    const isNewFromMoi = Boolean(
      moiData
      && !source?.packChecklist
      && !source?.submittedForAdminReviewAt
      && (source?.requestedBy || source?.typeOfDocument)
      && (source?.moiFormId == null || source?.moiFormId === source?.id),
    );

    setFormData(hydrateMoaFormFields(source, {
      customers: customersRef.current,
      serviceFallback: String(source?.service ?? ''),
      isNewFromMoi,
    }));

    if (source?.workflow) setWorkflow(source.workflow as WorkflowInstanceDto);
    else setWorkflow(null);

    if (source?.packChecklist) setPackChecklist(source.packChecklist as MoaPackChecklistDto);
    else setPackChecklist(emptyPackChecklist());

    if (source?.packValidationErrors) setPackErrors(source.packValidationErrors as string[]);
    else setPackErrors([]);

    setAutoSaveState('idle');
    window.setTimeout(() => {
      hydratingRef.current = false;
    }, 0);
  }, [
    isOpen,
    initialData?.id,
    initialData?.jobId,
    initialData?.unitNumber,
    initialData?.activeUnitNumber,
    moiData?.id,
    moiData?.jobId,
    moiData?.unitNumber,
    moiData?.activeUnitNumber,
  ]);

  const moaFormId = moaFormIdRef.current ?? (initialData?.id ?? moiData?.id) as number | undefined;

  useEffect(() => {
    if (!isOpen || hydratingRef.current || viewMode || isClientUser || sequentialWorkflowUi) return;
    onDirtyChangeRef.current?.(true);
    setAutoSaveState((prev) => (prev === 'error' ? prev : 'pending'));
  }, [formData, packChecklist, isOpen, viewMode, isClientUser, sequentialWorkflowUi]);

  const buildSubmitPayload = () => ({
    ...formData,
    id: moaFormIdRef.current ?? (initialData?.id ?? moiData?.id),
    jobId: initialData?.jobId ?? moiData?.jobId,
    unitNumber: initialData?.unitNumber ?? initialData?.activeUnitNumber ?? moiData?.unitNumber,
    activeUnitNumber: initialData?.activeUnitNumber ?? moiData?.activeUnitNumber,
    updatedAt: savedUpdatedAtRef.current ?? initialData?.updatedAt ?? moiData?.updatedAt,
    packChecklist,
  });

  useEffect(() => {
    if (!isOpen || viewMode || isClientUser || sequentialWorkflowUi || !onSaveDraft) return;
    if (hydratingRef.current || autoSaveState !== 'pending') return;

    if (autoSaveTimerRef.current != null) {
      window.clearTimeout(autoSaveTimerRef.current);
    }

    autoSaveTimerRef.current = window.setTimeout(() => {
      const savePromise = onSaveDraft(buildSubmitPayload());
      autoSaveInFlightRef.current = savePromise;
      void savePromise
        .then((result) => {
          if (result.id) moaFormIdRef.current = result.id;
          if (result.updatedAt) savedUpdatedAtRef.current = result.updatedAt;
          onDirtyChangeRef.current?.(false);
          setAutoSaveState('idle');
        })
        .catch(() => setAutoSaveState('error'))
        .finally(() => {
          if (autoSaveInFlightRef.current === savePromise) {
            autoSaveInFlightRef.current = null;
          }
        });
    }, 900);

    return () => {
      if (autoSaveTimerRef.current != null) {
        window.clearTimeout(autoSaveTimerRef.current);
      }
    };
  }, [isOpen, viewMode, isClientUser, sequentialWorkflowUi, onSaveDraft, formData, packChecklist, autoSaveState]);

  const selectedCompany = customers?.find(c => c.company === formData.company);

  useEffect(() => {
    if (!isOpen || !formData.company) return;
    resolveFormTemplate('MOA', formData.company, formData.formTemplateCode || undefined)
      .then(setFormTemplate)
      .catch(() => setFormTemplate(null));
  }, [isOpen, formData.company, formData.formTemplateCode]);

  const validatePackChecklist = (): string[] => {
    const errors: string[] = [];
    if (!packChecklist.internalChecklistA) errors.push('Internal checklist A must be completed.');
    if (!packChecklist.internalChecklistB) errors.push('Internal checklist B must be completed.');
    if (!packChecklist.cleanAgreementAttached) errors.push('Clean agreement / appointment letter attachment is required.');
    if (!packChecklist.ssmRegistrationNo.trim()) errors.push('SSM registration number is required.');
    if (!packChecklist.ssmEntityType.trim()) errors.push('SSM entity type is required.');
    if (!packChecklist.ssmStatus.trim()) errors.push('SSM status is required.');
    if (!packChecklist.ssmAsAtDate.trim()) errors.push('SSM as-at date is required.');
    if (formData.shareMovement && !packChecklist.shareholdingTableAttached) {
      errors.push('Shareholding table is required when share movement is flagged.');
    }
    return errors;
  };

  const cancelPendingAutoSave = () => {
    if (autoSaveTimerRef.current != null) {
      window.clearTimeout(autoSaveTimerRef.current);
      autoSaveTimerRef.current = null;
    }
  };

  const applySaveResult = (result?: { id: number; updatedAt?: string } | null) => {
    if (result?.id) moaFormIdRef.current = result.id;
    if (result?.updatedAt) savedUpdatedAtRef.current = result.updatedAt;
    onDirtyChangeRef.current?.(false);
    setAutoSaveState('idle');
  };

  const persistDraft = async (): Promise<{ id: number; jobId: number; unitNumber?: number; updatedAt?: string } | null> => {
    cancelPendingAutoSave();
    if (autoSaveInFlightRef.current) {
      try {
        await autoSaveInFlightRef.current;
      } catch {
        // Fall through and retry with the latest editor state.
      }
    }

    const payload = buildSubmitPayload();
    try {
      const result = onSaveDraft
        ? await onSaveDraft(payload)
        : await onSubmit(payload);
      if (!result) return null;
      applySaveResult(result);
      return result;
    } catch {
      setAutoSaveState('error');
      return null;
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    try {
      cancelPendingAutoSave();
      if (autoSaveInFlightRef.current) {
        try {
          await autoSaveInFlightRef.current;
        } catch {
          // Retry with the latest editor state below.
        }
      }

      const result = await onSubmit(buildSubmitPayload());
      if (result) {
        applySaveResult(result);
        setAutoSaveState('saved');
      }
    } catch {
      setAutoSaveState('error');
    } finally {
      setSubmitting(false);
    }
  };

  const handleSubmitForAdminReview = async () => {
    setSubmitError('');
    const errors = validatePackChecklist();
    setPackErrors(errors);
    if (errors.length > 0) {
      packErrorsRef.current?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
      return;
    }

    const jobId = initialData?.jobId ?? moiData?.jobId;
    if (!jobId || !onSubmitForAdminReview) return;

    setSubmitting(true);
    try {
      const saved = await persistDraft();
      if (!saved) {
        setSubmitError('Could not save the latest draft. Please try Save draft first, then submit again.');
        return;
      }
      await onSubmitForAdminReview(jobId);
    } catch {
      setSubmitError('Failed to submit MOA for admin approval. Complete the pack checklist and try again.');
    } finally {
      setSubmitting(false);
    }
  };

  const handleApproveStep = async () => {
    if (!workflow || !moaFormId) return;
    const updated = await approveMoaWorkflowStep(moaFormId, stepComments);
    setWorkflow(updated);
    setStepComments('');
  };

  const handleAdminOverrideStep = async (stepId: number) => {
    if (!workflow || !moaFormId) return;
    const updated = await adminOverrideMoaStep(moaFormId, stepId, 'Admin override');
    setWorkflow(updated);
  };

  const currentStep = workflow?.steps.find((s) => s.isCurrent);

  const handleClose = () => {
    setFormData(emptyMoaFormFields());
    setPackChecklist(emptyPackChecklist());
    setPackErrors([]);
    setWorkflow(null);
    onClose();
  };

  const handleMOAPersonApprovalChange = (index: number, field: 'approved' | 'date' | 'comments', value: any) => {
    const newApprovals = [...formData.moaPersonsApprovals];
    newApprovals[index] = { ...newApprovals[index], [field]: value };
    setFormData({ ...formData, moaPersonsApprovals: newApprovals });
  };

  const rejectionStageLabel = (stage?: string) => {
    if (stage === 'moa_sharon_review') return 'Head secretary review';
    if (stage === 'moa_client_approval') return 'Client sign-off';
    return 'Review';
  };


  const handleDownloadPack = async () => {
    const formId = (initialData?.id ?? moiData?.id) as number | undefined;
    if (!formId) return;
    setExportingPack(true);
    setExportPackError('');
    try {
      const blob = await downloadMoaExportPack(formId);
      saveBlobAsFile(blob, `moa-${formId}-pack.json`);
    } catch (err) {
      setExportPackError(err instanceof ApiError ? err.message : 'Failed to download pack.');
    } finally {
      setExportingPack(false);
    }
  };

  if (!isOpen) return null;

  const moiFields = linkedMoiData ? hydrateMoiFormFields(linkedMoiData) : null;
  const resolvedJobId = jobId ?? (linkedMoiData?.jobId as number | undefined) ?? (initialData?.jobId as number | undefined);
  const resolvedUnitNumber = unitNumber
    ?? (linkedMoiData?.unitNumber as number | undefined)
    ?? (linkedMoiData?.activeUnitNumber as number | undefined);
  const canManageMoaAttachments = !isClientUser && (allowMoaAttachments || !viewMode);

  const ensureJobForDocuments = async () => {
    if (resolvedJobId) {
      return { jobId: Number(resolvedJobId), unitNumber: resolvedUnitNumber };
    }
    if (!onSaveDraft) {
      throw new Error('Save the MOA draft first so attachments can be stored.');
    }
    return onSaveDraft(buildSubmitPayload());
  };

  const moaAttachmentsSection = (resolvedJobId || onSaveDraft) ? (
    <div className="border border-primary/25 rounded-lg p-4 bg-primary/5 space-y-2">
      <p className="text-xs text-muted-foreground">
        {canManageMoaAttachments
          ? 'Upload the MOA pack and any supporting documents below (multiple files per section).'
          : 'Review MOA pack and supporting documents attached for this item.'}
      </p>
      <JobItemDocumentsSection
        jobId={Number(resolvedJobId) || 0}
        unitNumber={resolvedUnitNumber}
        refreshKey={documentsRefreshKey}
        folders={[...MOA_ATTACHMENT_FOLDERS]}
        groupByFolder
        title="MOA attachments"
        showWhenEmpty
        allowUpload={canManageMoaAttachments}
        allowDelete={canManageMoaAttachments}
        uploadFolder="moa"
        onBeforeUpload={async () => {
          const resolved = await ensureJobForDocuments();
          setDocumentsRefreshKey((k) => k + 1);
          return resolved;
        }}
      />
    </div>
  ) : null;

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
      <div className="bg-card rounded-lg border border-border w-full max-w-4xl max-h-[90vh] overflow-hidden flex flex-col">
        <div className="p-6 border-b border-border flex items-center justify-between gap-4">
          <div className="min-w-0">
            <h2>Memorandum of Approval (MOA) {viewMode && '- View Mode'}</h2>
            {linkedMoiData && onViewLinkedMoi && (
              <p className="text-xs text-muted-foreground mt-1">
                Need the client MOI? Use <span className="font-medium text-primary">View MOI</span> — your MOA draft stays open underneath.
              </p>
            )}
            {!viewMode && !isClientUser && !sequentialWorkflowUi && onSaveDraft && autoSaveState === 'error' && (
              <p className="text-xs mt-1 h-4 text-destructive" aria-live="polite">
                Auto-save failed — use Save draft
              </p>
            )}
            {exportPackError && (
              <p className="text-xs text-destructive mt-1">{exportPackError}</p>
            )}
          </div>
          <div className="flex items-center gap-2 shrink-0">
            {(initialData?.id ?? moiData?.id) && (
              <button
                type="button"
                onClick={() => void handleDownloadPack()}
                disabled={exportingPack}
                title="Structured JSON export (PDF packs coming later)"
                className="inline-flex items-center gap-1.5 px-3 py-1.5 text-sm border border-border rounded-lg hover:bg-muted transition-colors disabled:opacity-50"
              >
                <Download className="w-4 h-4" />
                {exportingPack ? 'Downloading…' : 'Download pack'}
              </button>
            )}
            {linkedMoiData && onViewLinkedMoi && (
              <button
                type="button"
                onClick={onViewLinkedMoi}
                className="inline-flex items-center gap-1.5 px-3 py-1.5 text-sm border border-primary/30 bg-primary/10 text-primary rounded-lg hover:bg-primary/15 transition-colors"
              >
                <FileText className="w-4 h-4" />
                View MOI
              </button>
            )}
            <button
              type="button"
              onClick={handleClose}
              className="p-1 hover:bg-muted rounded transition-colors"
            >
              <X className="w-5 h-5" />
            </button>
          </div>
        </div>

        <form onSubmit={handleSubmit} className="flex-1 overflow-y-auto">
          <div className="p-6 space-y-6">
            {/* Fixed Fields */}
            <div className="bg-muted/30 rounded-lg p-4 space-y-2">
              <div className="flex items-center justify-between">
                <span className="text-sm text-muted-foreground">Description:</span>
                <span className="font-medium">{formTemplate?.description || 'Memorandum of Approval'}</span>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-sm text-muted-foreground">To:</span>
                <span className="font-medium">{formTemplate?.addressedTo || 'Head of Legal & Secretarial Department'}</span>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-sm text-muted-foreground">Division:</span>
                <span className="font-medium">{formTemplate?.divisionLabel || 'Secretarial Division'}</span>
              </div>
              {sequentialWorkflowUi && workflow && (
                <div className="flex items-center justify-between">
                  <span className="text-sm text-muted-foreground">Workflow:</span>
                  <span className="text-sm font-medium">{workflow.templateCode} — {workflow.status}</span>
                </div>
              )}
            </div>

            {moaAttachmentsSection}

            {linkedMoiData && moiFields && (
              <div className="border border-primary/20 rounded-lg p-4 bg-primary/5 space-y-3">
                <div className="flex items-center justify-between gap-3">
                  <div className="flex items-center gap-2 text-sm font-medium">
                    <FileText className="w-4 h-4 text-primary" />
                    Source MOI (read-only)
                  </div>
                  {onViewLinkedMoi && (
                    <button
                      type="button"
                      onClick={onViewLinkedMoi}
                      className="text-xs px-3 py-1.5 border border-border rounded-lg hover:bg-muted"
                    >
                      Open full MOI
                    </button>
                  )}
                </div>
                {moiFields.documentTitle && (
                  <div>
                    <p className="text-xs text-muted-foreground">Document title</p>
                    <p className="text-sm">{String(moiFields.documentTitle)}</p>
                  </div>
                )}
                {moiFields.backgroundInfo && (
                  <div>
                    <p className="text-xs text-muted-foreground">Background</p>
                    <p className="text-sm whitespace-pre-wrap">{String(moiFields.backgroundInfo)}</p>
                  </div>
                )}
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 text-sm">
                  {moiFields.requiredExecutionDate && (
                    <div>
                      <p className="text-xs text-muted-foreground">Required MOA signatories</p>
                      <p>{formatDateDisplay(String(moiFields.requiredExecutionDate)) || String(moiFields.requiredExecutionDate)}</p>
                    </div>
                  )}
                  {moiFields.turnaroundWeeks && (
                    <div>
                      <p className="text-xs text-muted-foreground">Turnaround</p>
                      <p>{String(moiFields.turnaroundWeeks)} week(s)</p>
                    </div>
                  )}
                </div>
                {resolvedJobId && (
                  <JobItemDocumentsSection
                    jobId={Number(resolvedJobId)}
                    unitNumber={resolvedUnitNumber}
                    folders={MOI_SOURCE_DOC_FOLDERS}
                    title="MOI supporting documents"
                    showWhenEmpty
                  />
                )}
              </div>
            )}

            {/* Company (from MOI) */}
            <div>
              <label className="block mb-2">Company</label>
              <input
                type="text"
                value={formData.company}
                className="w-full px-3 py-2 border border-border rounded-lg bg-muted/30 cursor-not-allowed"
                disabled
              />
            </div>

            {/* Type of Document (from MOI) */}
            <div>
              <label className="block mb-2">Type of Document</label>
              <input
                type="text"
                value={formData.typeOfDocument}
                className="w-full px-3 py-2 border border-border rounded-lg bg-muted/30 cursor-not-allowed"
                disabled
              />
            </div>

            {/* Project Initiator (from MOI) */}
            <div>
              <label className="block mb-2">Project Initiator</label>
              <input
                type="text"
                value={formData.projectInitiator}
                className="w-full px-3 py-2 border border-border rounded-lg bg-muted/30 cursor-not-allowed"
                disabled
              />
            </div>

            {/* Prepared By (Internal) */}
            <div>
              <label className="block mb-2">Prepared by (Internal) *</label>
              <select
                required
                value={formData.preparedByInternal}
                onChange={(e) => setFormData({ ...formData, preparedByInternal: e.target.value })}
                className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                disabled={viewMode}
              >
                <option value="">Select User</option>
                {pickerUsers.map((user) => (
                  <option key={user.id} value={user.name}>
                    {user.name}
                  </option>
                ))}
              </select>
            </div>

            {/* Vetted By (Internal) */}
            <div>
              <label className="block mb-2">Vetted by (Internal) *</label>
              <select
                required
                value={formData.vettedByInternal}
                onChange={(e) => setFormData({ ...formData, vettedByInternal: e.target.value })}
                className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                disabled={viewMode}
              >
                <option value="">Select User</option>
                {pickerUsers.map((user) => (
                  <option key={user.id} value={user.name}>
                    {user.name}
                  </option>
                ))}
              </select>
            </div>

            {/* Prepared By (External) */}
            <div>
              <label className="block mb-2">Prepared by (External)</label>
              <input
                type="text"
                value={formData.preparedByExternal}
                onChange={(e) => setFormData({ ...formData, preparedByExternal: e.target.value })}
                className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                placeholder="Enter external preparer name"
                disabled={viewMode}
              />
            </div>

            {/* Vetted By (External) */}
            <div>
              <label className="block mb-2">Vetted by (External)</label>
              <input
                type="text"
                value={formData.vettedByExternal}
                onChange={(e) => setFormData({ ...formData, vettedByExternal: e.target.value })}
                className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                placeholder="Enter external vetter name"
                disabled={viewMode}
              />
            </div>

            {!sequentialWorkflowUi && (
              <div className="border border-border rounded-lg p-4 space-y-4">
                <h4 className="text-sm font-medium">MOA pack checklist</h4>
                <p className="text-xs text-muted-foreground">Complete before starting MOA circulation (per SOP).</p>
                <label className="flex items-center gap-2 text-sm">
                  <input type="checkbox" checked={packChecklist.internalChecklistA} onChange={(e) => setPackChecklist({ ...packChecklist, internalChecklistA: e.target.checked })} disabled={viewMode} />
                  Internal checklist A
                </label>
                <label className="flex items-center gap-2 text-sm">
                  <input type="checkbox" checked={packChecklist.internalChecklistB} onChange={(e) => setPackChecklist({ ...packChecklist, internalChecklistB: e.target.checked })} disabled={viewMode} />
                  Internal checklist B
                </label>
                <label className="flex items-center gap-2 text-sm">
                  <input type="checkbox" checked={packChecklist.cleanAgreementAttached} onChange={(e) => setPackChecklist({ ...packChecklist, cleanAgreementAttached: e.target.checked })} disabled={viewMode} />
                  Clean agreement / appointment letter attached
                </label>
                {!viewMode && !isClientUser && (
                  <p className="text-xs text-muted-foreground -mt-2">
                    Upload the agreement and other pack files in MOA attachments at the top of this form.
                  </p>
                )}
                {formData.shareMovement && (
                  <label className="flex items-center gap-2 text-sm">
                    <input type="checkbox" checked={packChecklist.shareholdingTableAttached} onChange={(e) => setPackChecklist({ ...packChecklist, shareholdingTableAttached: e.target.checked })} disabled={viewMode} />
                    Shareholding table attached
                  </label>
                )}
                <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                  <input className="px-3 py-2 border border-border rounded-lg text-sm" placeholder="SSM registration no *" value={packChecklist.ssmRegistrationNo} onChange={(e) => setPackChecklist({ ...packChecklist, ssmRegistrationNo: e.target.value })} disabled={viewMode} />
                  <input className="px-3 py-2 border border-border rounded-lg text-sm" placeholder="SSM new registration no" value={packChecklist.ssmNewRegistrationNo} onChange={(e) => setPackChecklist({ ...packChecklist, ssmNewRegistrationNo: e.target.value })} disabled={viewMode} />
                  <input className="px-3 py-2 border border-border rounded-lg text-sm" placeholder="Entity type *" value={packChecklist.ssmEntityType} onChange={(e) => setPackChecklist({ ...packChecklist, ssmEntityType: e.target.value })} disabled={viewMode} />
                  <input className="px-3 py-2 border border-border rounded-lg text-sm" placeholder="SSM status *" value={packChecklist.ssmStatus} onChange={(e) => setPackChecklist({ ...packChecklist, ssmStatus: e.target.value })} disabled={viewMode} />
                  <div className="md:col-span-2">
                    <label className="block mb-1 text-sm">SSM as-at date *</label>
                    <DateInput
                      fullWidth
                      value={packChecklist.ssmAsAtDate}
                      onChange={(iso) => setPackChecklist({ ...packChecklist, ssmAsAtDate: iso })}
                      disabled={viewMode}
                      className="px-3 py-2 border border-border rounded-lg text-sm bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                    />
                  </div>
                </div>
                {packErrors.length > 0 && (
                  <ul ref={packErrorsRef} className="text-xs text-destructive list-disc pl-4">
                    {packErrors.map((err) => <li key={err}>{err}</li>)}
                  </ul>
                )}
                {SHOW_MOA_SEQUENTIAL_WORKFLOW && onStartWorkflow && !viewMode && !isClientUser && (
                  <button
                    type="button"
                    className="px-4 py-2 border border-border rounded-lg text-sm hover:bg-muted"
                    onClick={() => {
                      const errors = validatePackChecklist();
                      setPackErrors(errors);
                      if (errors.length > 0) return;
                      void onStartWorkflow(buildSubmitPayload());
                    }}
                  >
                    Start internal routing (optional)
                  </button>
                )}
                <p className="text-xs text-muted-foreground">
                  Complete the pack checklist and MOA details, then submit the draft for head secretary approval before client sign-off.
                </p>
              </div>
            )}

            {!viewMode && (
              <div className="border border-border rounded-lg p-4 space-y-3">
                <h4 className="text-sm font-medium">Routing conditions (from MOI)</h4>
                <label className="flex items-center gap-2">
                  <input type="checkbox" checked={formData.financeRelated} onChange={(e) => setFormData({ ...formData, financeRelated: e.target.checked })} />
                  <span className="text-sm">Finance / compliance affecting AFS</span>
                </label>
                <label className="flex items-center gap-2">
                  <input type="checkbox" checked={formData.bankSignatoryMatter} onChange={(e) => setFormData({ ...formData, bankSignatoryMatter: e.target.checked })} />
                  <span className="text-sm">Bank signatory matter</span>
                </label>
                <label className="flex items-center gap-2">
                  <input type="checkbox" checked={formData.shareMovement} onChange={(e) => setFormData({ ...formData, shareMovement: e.target.checked })} />
                  <span className="text-sm">Movement of shares (shareholding table required)</span>
                </label>
              </div>
            )}

            {sequentialWorkflowUi && workflow && (
              <div className="border-t border-border pt-6 space-y-4">
                <h3>Sequential approval ({workflow.templateCode})</h3>
                {workflow.steps.map((step) => (
                  <div
                    key={step.id}
                    className={`border rounded-lg p-4 ${step.isCurrent ? 'border-primary bg-primary/5' : 'border-border opacity-80'}`}
                  >
                    <div className="flex justify-between items-start gap-2">
                      <div>
                        <p className="font-medium">{step.stepOrder}. {step.displayName}</p>
                        <p className="text-sm text-muted-foreground">Assignee: {step.assigneeName}</p>
                      </div>
                      <span className="text-xs px-2 py-1 rounded bg-muted">{step.status}</span>
                    </div>
                    {step.comments && <p className="text-sm mt-2">{step.comments}</p>}
                    {userIsAdmin && step.isCurrent && step.status === 'Active' && (
                      <button
                        type="button"
                        className="mt-2 text-xs px-2 py-1 border border-border rounded"
                        onClick={() => void handleAdminOverrideStep(step.id)}
                      >
                        Admin skip step
                      </button>
                    )}
                  </div>
                ))}
                {currentStep && viewMode && (
                  <div className="flex gap-2 items-center">
                    <input
                      className="flex-1 px-3 py-2 border border-border rounded-lg text-sm"
                      placeholder="Approval comments"
                      value={stepComments}
                      onChange={(e) => setStepComments(e.target.value)}
                    />
                    <button
                      type="button"
                      onClick={() => void handleApproveStep()}
                      className="px-4 py-2 bg-primary text-primary-foreground rounded-lg text-sm"
                    >
                      Approve step
                    </button>
                  </div>
                )}
              </div>
            )}

            {/* Legacy checkbox approvals — client-only; sec uses SignatureCapture via client-approve */}
            <div className={`border-t border-border pt-6 ${sequentialWorkflowUi || !isClientUser ? 'hidden' : ''}`}>
              <h3 className="mb-4">Approved By</h3>

              {/* Senior Manager, Company Secretary */}
              {templateSections.seniorManagerCoSec && (
              <div className="border border-border rounded-lg p-4 mb-4 bg-muted/30">
                <h4 className="mb-4">Senior Manager, Company Secretary</h4>
                <div className="space-y-4">
                  <div className="flex items-center gap-4">
                    <label className="flex items-center gap-2 cursor-pointer">
                      <input
                        type="checkbox"
                        checked={formData.seniorManagerApproval.approved}
                        onChange={(e) => setFormData({
                          ...formData,
                          seniorManagerApproval: {
                            ...formData.seniorManagerApproval,
                            approved: e.target.checked
                          }
                        })}
                        className="w-4 h-4 cursor-pointer"
                        disabled={viewMode}
                      />
                      <span>Approve</span>
                    </label>
                    <div className="flex-1">
                      <DateInput
                        fullWidth
                        value={formData.seniorManagerApproval.date}
                        onChange={(iso) => setFormData({
                          ...formData,
                          seniorManagerApproval: {
                            ...formData.seniorManagerApproval,
                            date: iso,
                          },
                        })}
                        className="px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                        disabled={viewMode}
                      />
                    </div>
                  </div>
                  <div>
                    <label className="block mb-2">Comments</label>
                    <textarea
                      rows={3}
                      value={formData.seniorManagerApproval.comments}
                      onChange={(e) => setFormData({
                        ...formData,
                        seniorManagerApproval: {
                          ...formData.seniorManagerApproval,
                          comments: e.target.value
                        }
                      })}
                      className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring resize-none"
                      placeholder="Enter comments..."
                      disabled={viewMode}
                    />
                  </div>
                  <div>
                    <label className="flex items-center gap-2 cursor-pointer">
                      <input
                        type="checkbox"
                        checked={formData.seniorManagerApproval.resoForDLCM}
                        onChange={(e) => setFormData({
                          ...formData,
                          seniorManagerApproval: {
                            ...formData.seniorManagerApproval,
                            resoForDLCM: e.target.checked
                          }
                        })}
                        className="w-4 h-4 cursor-pointer"
                        disabled={viewMode}
                      />
                      <span>Reso for DLCM's signature</span>
                    </label>
                  </div>
                </div>
              </div>
              )}

              {/* Manager - Regulatory & Compliance */}
              {templateSections.managerRegulatory && (
              <div className="border border-border rounded-lg p-4 mb-4 bg-muted/30">
                <h4 className="mb-4">Manager - Regulatory & Compliance</h4>
                <div className="space-y-4">
                  <div className="flex items-center gap-4">
                    <label className="flex items-center gap-2 cursor-pointer">
                      <input
                        type="checkbox"
                        checked={formData.managerRegulatoryApproval.approved}
                        onChange={(e) => setFormData({
                          ...formData,
                          managerRegulatoryApproval: {
                            ...formData.managerRegulatoryApproval,
                            approved: e.target.checked
                          }
                        })}
                        className="w-4 h-4 cursor-pointer"
                        disabled={viewMode}
                      />
                      <span>Approve</span>
                    </label>
                    <div className="flex-1">
                      <DateInput
                        fullWidth
                        value={formData.managerRegulatoryApproval.date}
                        onChange={(iso) => setFormData({
                          ...formData,
                          managerRegulatoryApproval: {
                            ...formData.managerRegulatoryApproval,
                            date: iso,
                          },
                        })}
                        className="px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                        disabled={viewMode}
                      />
                    </div>
                  </div>
                  <div>
                    <label className="block mb-2">Comments</label>
                    <textarea
                      rows={3}
                      value={formData.managerRegulatoryApproval.comments}
                      onChange={(e) => setFormData({
                        ...formData,
                        managerRegulatoryApproval: {
                          ...formData.managerRegulatoryApproval,
                          comments: e.target.value
                        }
                      })}
                      className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring resize-none"
                      placeholder="Enter comments..."
                      disabled={viewMode}
                    />
                  </div>
                </div>
              </div>
              )}

              {/* MOA Persons from Company */}
              {formData.moaPersonsApprovals.length > 0 && (
                <div className="space-y-4">
                  <h4>Account Holders (MOA)</h4>
                  {formData.moaPersonsApprovals.map((approval, index) => (
                    <div key={index} className="border border-border rounded-lg p-4 bg-muted/30">
                      <h4 className="mb-4">{approval.name}</h4>
                      <div className="space-y-4">
                        <div className="flex items-center gap-4">
                          <label className="flex items-center gap-2 cursor-pointer">
                            <input
                              type="checkbox"
                              checked={approval.approved}
                              onChange={(e) => handleMOAPersonApprovalChange(index, 'approved', e.target.checked)}
                              className="w-4 h-4 cursor-pointer"
                              disabled={viewMode}
                            />
                            <span>Approve</span>
                          </label>
                          <div className="flex-1">
                            <DateInput
                              fullWidth
                              value={approval.date}
                              onChange={(iso) => handleMOAPersonApprovalChange(index, 'date', iso)}
                              className="px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                              disabled={viewMode}
                            />
                          </div>
                        </div>
                        <div>
                          <label className="block mb-2">Comments</label>
                          <textarea
                            rows={3}
                            value={approval.comments}
                            onChange={(e) => handleMOAPersonApprovalChange(index, 'comments', e.target.value)}
                            className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring resize-none"
                            placeholder="Enter comments..."
                            disabled={viewMode}
                          />
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>

          {jobHandoffStatus === 'AdminReview' && !canApproveMoa && !userIsAdmin && !isClientUser && (
            <div className="px-6 pb-4">
              <div className="rounded-lg border border-sky-200 bg-sky-50 px-4 py-3 text-sm text-sky-900">
                <p className="font-medium">Awaiting head secretary approval</p>
                <p className="mt-1 text-sky-800/90">
                  This MOA draft has been submitted
                  {initialData?.submittedForAdminReviewAt
                    ? ` on ${initialData.submittedForAdminReviewAt}`
                    : ''}.
                  Everyone tagged on this item can view it while approval is pending.
                </p>
              </div>
            </div>
          )}

          {initialData?.rejections?.length > 0 && !isClientUser && (
            <div className="px-6 pb-4">
              <div className="p-3 border border-amber-200 bg-amber-50 rounded-lg text-sm space-y-2">
                <p className="font-medium text-amber-900">
                  {(jobHandoffStatus === 'ResoInProgress' || jobHandoffStatus === 'PendingPrep')
                    ? 'Returned for revision'
                    : 'Rejection history'}
                </p>
                <p className="text-xs text-amber-800/90">
                  Visible to everyone tagged on this package line.
                </p>
                {initialData.rejections.map((r: {
                  userName: string;
                  reason: string;
                  rejectedAt: string;
                  stage?: string;
                }, i: number) => (
                  <div key={i} className="rounded-md border border-amber-200/80 bg-white/70 px-3 py-2 text-amber-950">
                    <p className="font-medium">
                      {r.userName}
                      <span className="font-normal text-amber-800">
                        {' · '}
                        {rejectionStageLabel(r.stage)}
                        {r.rejectedAt ? ` · ${r.rejectedAt}` : ''}
                      </span>
                    </p>
                    <p className="mt-1">{r.reason}</p>
                  </div>
                ))}
              </div>
            </div>
          )}

          {clientApprovals.length > 0 && (
            <div className="px-6 pb-2">
              <ClientSignOffTrail title="MOA sign-off record" approvals={clientApprovals} />
            </div>
          )}

          <div className="p-6 border-t border-border flex justify-between items-start gap-3">
            <div className="space-y-4">
              {(userIsAdmin || canApproveMoa) && !isClientUser && initialData?.jobId && jobHandoffStatus === 'AdminReview' && (
                <div className="space-y-2 max-w-lg">
                  <p className="text-sm font-medium">Head secretary review</p>
                  <div className="flex flex-wrap gap-2">
                    {onSharonApprove && (
                      <button
                        type="button"
                        className="px-4 py-2 bg-primary text-primary-foreground rounded-lg text-sm"
                        onClick={() => onSharonApprove(initialData.jobId)}
                      >
                        Approve MOA for client release
                      </button>
                    )}
                    {onSharonReject && (
                      <button
                        type="button"
                        className="px-4 py-2 border border-destructive text-destructive rounded-lg text-sm disabled:opacity-50"
                        disabled={!sharonRejectReason.trim()}
                        onClick={() => onSharonReject(initialData.jobId, sharonRejectReason.trim())}
                      >
                        Reject back to secretary
                      </button>
                    )}
                  </div>
                  {onSharonReject && (
                    <textarea
                      rows={2}
                      className="w-full px-3 py-2 border border-border rounded-lg text-sm"
                      placeholder="Rejection reason for secretary"
                      value={sharonRejectReason}
                      onChange={(e) => setSharonRejectReason(e.target.value)}
                    />
                  )}
                </div>
              )}
              {(userIsAdmin || canApproveMoa) && !isClientUser && initialData?.jobId && jobHandoffStatus === 'MoaSharonApproved' && onSendToClient && (
                <button
                  type="button"
                  className="px-4 py-2 bg-green-700 text-white rounded-lg text-sm"
                  onClick={() => onSendToClient(initialData.jobId)}
                >
                  Send MOA to client
                </button>
              )}
              {isClientUser && viewMode && initialData?.id && !canSignMoa && (
                <div className="text-sm text-muted-foreground border border-border rounded-lg p-3 bg-muted/30 max-w-lg">
                  {alreadySignedMoa ? (
                    <p>You have already signed this MOA.</p>
                  ) : !needsMoa ? (
                    <p>You can view this MOA for reference. Only designated MOA signatories can approve and sign.</p>
                  ) : !isMoaSignoffPhase ? (
                    <p>This MOA is not open for client sign-off yet.</p>
                  ) : pendingApprovers.length > 0 ? (
                    <p>
                      Awaiting signature from:{' '}
                      {formatPendingApproverList(pendingApprovers, currentUserName, signatoryHolderNames)}
                    </p>
                  ) : (
                    <p>Your signature is not required on this MOA.</p>
                  )}
                </div>
              )}
              {canSignMoa && onClientApprove && (
                <div className="space-y-3 max-w-lg">
                  <SignatureCapture value={signature} onChange={setSignature} />
                  <input
                    className="w-full px-3 py-2 border border-border rounded-lg text-sm"
                    placeholder="Comments (optional)"
                    value={clientSignComments}
                    onChange={(e) => setClientSignComments(e.target.value)}
                  />
                  <div className="flex flex-wrap gap-2">
                    <button
                      type="button"
                      onClick={() => {
                        if (!signature) return;
                        onClientApprove(initialData.id, {
                          comments: clientSignComments,
                          signatureFileName: signature.fileName,
                          signatureDataUrl: signature.dataUrl,
                        });
                      }}
                      disabled={!signature}
                      className="px-4 py-2 bg-primary text-primary-foreground rounded-lg text-sm disabled:opacity-50"
                    >
                      Approve MOA
                    </button>
                    {onClientReject && (
                      <button
                        type="button"
                        disabled={!rejectReason.trim()}
                        onClick={() => onClientReject(initialData.id, rejectReason.trim())}
                        className="px-4 py-2 border border-destructive text-destructive rounded-lg text-sm disabled:opacity-50"
                      >
                        Reject MOA
                      </button>
                    )}
                  </div>
                  {onClientReject && (
                    <textarea
                      rows={2}
                      className="w-full px-3 py-2 border border-border rounded-lg text-sm"
                      placeholder="Rejection reason (required to reject)"
                      value={rejectReason}
                      onChange={(e) => setRejectReason(e.target.value)}
                    />
                  )}
                </div>
              )}
              {initialData?.pendingApprovers?.length > 0 && (
                <p className="text-sm text-muted-foreground mt-2">
                  Awaiting sign-off from: {formatPendingApproverList(
                    initialData.pendingApprovers,
                    currentUserName,
                    signatoryHolderNames,
                  )}
                </p>
              )}
            </div>
            <div className="flex gap-3">
            <button
              type="button"
              onClick={handleClose}
              className="px-6 py-2 border border-border rounded-lg hover:bg-muted transition-colors"
            >
              {viewMode ? 'Close' : 'Cancel'}
            </button>
            {!viewMode && !sequentialWorkflowUi && !isClientUser && (
              <>
                <button
                  type="submit"
                  disabled={submitting}
                  className="px-6 py-2 border border-border rounded-lg hover:bg-muted transition-colors disabled:opacity-50"
                >
                  {submitting ? 'Saving…' : 'Save draft'}
                </button>
                {canSubmitForAdminReview && onSubmitForAdminReview && initialData?.jobId && (
                  <div className="flex flex-col items-end gap-1">
                    <button
                      type="button"
                      disabled={submitting}
                      onClick={() => void handleSubmitForAdminReview()}
                      className="inline-flex items-center gap-2 px-6 py-2 bg-primary text-primary-foreground rounded-lg hover:bg-primary/90 transition-colors disabled:opacity-50"
                    >
                      <Send className="h-4 w-4" />
                      {submitting ? 'Submitting…' : 'Submit for admin approval'}
                    </button>
                    {submitError && (
                      <p className="text-xs text-destructive text-right max-w-xs">{submitError}</p>
                    )}
                    <p className="text-xs text-muted-foreground">Complete the pack checklist above before submitting.</p>
                  </div>
                )}
              </>
            )}
            {sequentialWorkflowUi && !isClientUser && (
              <button type="button" onClick={handleClose} className="px-6 py-2 bg-primary text-primary-foreground rounded-lg">
                Done
              </button>
            )}
            {canMarkExecutionComplete && onMarkExecutionComplete && initialData?.jobId && (
              <button
                type="button"
                disabled={submitting}
                onClick={() => void onMarkExecutionComplete(initialData.jobId)}
                className="inline-flex items-center gap-2 px-6 py-2 bg-green-600 text-white rounded-lg hover:bg-green-700 transition-colors disabled:opacity-50"
              >
                <Send className="h-4 w-4" />
                Mark completed
              </button>
            )}
            </div>
          </div>
        </form>
      </div>
    </div>
  );
}
