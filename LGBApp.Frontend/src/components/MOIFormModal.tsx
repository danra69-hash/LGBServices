import { SignatureCapture, type SignaturePayload } from './SignatureCapture';
import { ClientSignOffTrail } from './ClientSignOffTrail';
import { useState, useEffect, useMemo, useRef } from 'react';
import { JobItemDocumentsSection } from './JobItemDocumentsSection';
import { ApiError, downloadMoiExportPack, resolveFormTemplate, saveBlobAsFile, type ClientApprovalDto, type FormTemplateDto } from '@/lib/api';
import { parseDateToIso } from '@/lib/dates';
import { DateInput } from './DateInput';
import { moiWorkflowStateLabel } from '@/lib/packageItemStatus';
import {
  formatPendingApproverList,
  signatoryApproverNames,
  signatoryHasPendingApproval,
  signatoryMatchesApproverName,
} from '@/lib/signatoryApprovers';
import { hydrateMoiFormFields } from '@/lib/moiFormState';
import { Download } from 'lucide-react';

const toIsoDate = (value: unknown): string => {
  const raw = String(value ?? '').trim();
  if (!raw) return '';
  return parseDateToIso(raw) ?? raw;
};

interface Customer {
  id: number;
  company: string;
  package: string;
  packageNames?: string[];
  accountHolders: { id: number; name: string; moi: boolean; moiApproval: boolean; moa: boolean }[];
}

interface Product {
  id: number;
  packageName: string;
  services: string[];
  serviceQuantities: Record<string, number>;
}

interface ServiceUsage {
  service: string;
  used: number;
  total: number;
}

interface MOIFormModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (data: any) => void | Promise<void>;
  onSaveDraft?: (data: Record<string, unknown>) => Promise<{ id: number; jobId: number; unitNumber?: number }>;
  onAccept?: (jobId: number, assignedTo: string, comments: string) => void;
  onRecommend?: (formId: number, comments: string) => void;
  onSubmitForApproval?: (formId: number, formData?: Record<string, unknown>) => void;
  onClientApprove?: (formId: number, payload: { comments: string; signatureFileName?: string; signatureDataUrl?: string }) => void;
  onClientReject?: (formId: number, reason: string) => void;
  onApproveIntake?: () => void | Promise<void>;
  onRejectIntake?: (reason: string) => void | Promise<void>;
  canApproveIntake?: boolean;
  awaitingIntakeApproval?: boolean;
  currentUserName?: string;
  signatoryHolderNames?: string[];
  needsMoiApproval?: boolean;
  onAdminOverride?: (formId: number, comments: string) => void;
  userIsAdmin?: boolean;
  isClientUser?: boolean;
  isMoiApprovalTask?: boolean;
  viewMode?: boolean;
  elevated?: boolean;
  closeLabel?: string;
  initialData?: any;
  jobId?: number;
  jobStatus?: 'Pending' | 'In Progress' | 'Completed' | 'Canceled';
  users?: { id: number; name: string }[];
  customers: Customer[];
  products: Product[];
  serviceUsage: ServiceUsage[];
}

export function MOIFormModal({
  isOpen, onClose, onSubmit, onSaveDraft, onAccept, onRecommend, onSubmitForApproval, onClientApprove, onClientReject,
  onApproveIntake, onRejectIntake, canApproveIntake = false, awaitingIntakeApproval = false, onAdminOverride,
  userIsAdmin = false, isClientUser = false, isMoiApprovalTask = false,
  viewMode = false, elevated = false, closeLabel, initialData, jobId, jobStatus, users = [], customers, products, serviceUsage,
  currentUserName = '',
  signatoryHolderNames = [],
  needsMoiApproval = false,
}: MOIFormModalProps) {
  const [formTemplate, setFormTemplate] = useState<FormTemplateDto | null>(null);
  const [workflowState, setWorkflowState] = useState('Draft');
  const [exportingPack, setExportingPack] = useState(false);
  const [exportPackError, setExportPackError] = useState('');
  const [recommendComments, setRecommendComments] = useState('');
  const [rejectReason, setRejectReason] = useState('');
  const [intakeRejectReason, setIntakeRejectReason] = useState('');
  const [intakeActionPending, setIntakeActionPending] = useState(false);
  const [signature, setSignature] = useState<SignaturePayload | null>(null);
  const [documentsRefreshKey, setDocumentsRefreshKey] = useState(0);
  const [documentCount, setDocumentCount] = useState(0);
  const [documentSubmitError, setDocumentSubmitError] = useState('');
  const [footerError, setFooterError] = useState('');
  const [actionPending, setActionPending] = useState(false);
  const formRef = useRef<HTMLFormElement>(null);
  const hydrateSessionKeyRef = useRef('');
  const pendingApprovers: string[] = initialData?.pendingApprovers ?? [];
  const requiredApprovers: string[] = initialData?.requiredApprovers ?? [];
  const clientApprovals: ClientApprovalDto[] = initialData?.clientApprovals ?? [];
  const nameMatches = (n: string) => signatoryMatchesApproverName(n, currentUserName, signatoryHolderNames);
  const alreadySigned = clientApprovals.some((a) => a.accountHolderName && nameMatches(a.accountHolderName));
  const canSignMoi = Boolean(
    isClientUser
    && needsMoiApproval
    && initialData?.id
    && currentUserName
    && workflowState === 'PendingClientMoiApproval'
    && !alreadySigned
    && (
      signatoryHasPendingApproval(pendingApprovers, currentUserName, signatoryHolderNames)
      || requiredApprovers.some(nameMatches)
    ),
  );

  const [formData, setFormData] = useState({
    company: '',
    documentTitle: '',
    backgroundInfo: '',
    typeOfDocument: '',
    supportingDocument: false,
    attachedFiles: [] as File[],
    approvedTemplate: false,
    documentsExecuted: false,
    reasonForRatification: '',
    withLOA: false,
    approvalPersons: [{ name: '', position: '' }] as { name: string; position: string }[],
    requestedBy: '',
    requestedDate: '',
    approvedBy: '',
    approvedDate: '',
    approvalComments: '',
    turnaroundWeeks: '',
    draftCanBeAmended: false,
    urgent: false,
    urgentReason: '',
    requiredExecutionDate: '',
    financeRelated: false,
    bankSignatoryMatter: false,
    formTemplateCode: '',
  });

  const [acceptanceData, setAcceptanceData] = useState({
    assignedTo: '',
    comments: '',
  });

  // Determine if this is a pending job
  const isPendingJob = viewMode && jobStatus === 'Pending';
  const showIntakeReview = Boolean(
    viewMode
    && canApproveIntake
    && onApproveIntake
    && onRejectIntake
    && (awaitingIntakeApproval || workflowState === 'PendingAdminIntake'),
  );
  const isMoiRejectedState = workflowState === 'MoiRejected';
  const hasRejectionHistory = (initialData?.rejections?.length ?? 0) > 0;
  const showClientRejectionBanner = isClientUser && !viewMode
    && (isMoiRejectedState || (hasRejectionHistory && workflowState === 'Draft'));
  const latestRejection = initialData?.rejections?.length
    ? initialData.rejections[initialData.rejections.length - 1]
    : null;
  const canClientResubmit = isClientUser
    && (workflowState === 'Draft' || isMoiRejectedState)
    && !isMoiApprovalTask;

  useEffect(() => {
    if (!isOpen) {
      setDocumentCount(0);
      setDocumentSubmitError('');
      return;
    }
    if (!formData.company) return;
    const service = String((formData as { service?: string }).service ?? formData.typeOfDocument ?? '');
    resolveFormTemplate('MOI', formData.company, formData.formTemplateCode || undefined, service || undefined)
      .then(setFormTemplate)
      .catch(() => setFormTemplate(null));
  }, [isOpen, formData.company, formData.formTemplateCode, formData.typeOfDocument]);

  // Reset or populate when modal opens (not on every auto-save metadata tick)
  useEffect(() => {
    if (!isOpen) {
      hydrateSessionKeyRef.current = '';
      setFooterError('');
      return;
    }
    if (!initialData) return;

    const sessionKey = [
      initialData.id ?? 'new',
      initialData.jobId ?? 'job',
      initialData.unitNumber ?? initialData.activeUnitNumber ?? 'unit',
    ].join(':');

    if (hydrateSessionKeyRef.current === sessionKey) {
      setWorkflowState(String(initialData.workflowState ?? 'Draft'));
      return;
    }

    hydrateSessionKeyRef.current = sessionKey;
    const d = hydrateMoiFormFields(initialData);
    const today = new Date().toISOString().slice(0, 10);
    setFormData({
      company: String(d.company ?? ''),
      documentTitle: String(d.documentTitle ?? ''),
      backgroundInfo: String(d.backgroundInfo ?? ''),
      typeOfDocument: String(d.typeOfDocument ?? ''),
      supportingDocument: Boolean(d.supportingDocument),
      attachedFiles: (d.attachedFiles as File[]) || [],
      approvedTemplate: Boolean(d.approvedTemplate),
      documentsExecuted: Boolean(d.documentsExecuted),
      reasonForRatification: String(d.reasonForRatification ?? ''),
      withLOA: Boolean(d.withLOA),
      approvalPersons: (d.approvalPersons as { name: string; position: string }[]) || [{ name: '', position: '' }],
      requestedBy: String(d.requestedBy ?? ''),
      requestedDate: toIsoDate(d.requestedDate) || (!viewMode ? today : ''),
      approvedBy: String(d.approvedBy ?? ''),
      approvedDate: toIsoDate(d.approvedDate),
      approvalComments: String(d.approvalComments ?? ''),
      turnaroundWeeks: String(d.turnaroundWeeks ?? ''),
      draftCanBeAmended: Boolean(d.draftCanBeAmended),
      urgent: Boolean(d.urgent),
      urgentReason: String(d.urgentReason ?? ''),
      requiredExecutionDate: toIsoDate(d.requiredExecutionDate),
      financeRelated: Boolean(d.financeRelated),
      bankSignatoryMatter: Boolean(d.bankSignatoryMatter),
      formTemplateCode: String(d.formTemplateCode ?? ''),
    });
    setWorkflowState(String(initialData.workflowState ?? 'Draft'));
    if (!viewMode) {
      setAcceptanceData({ assignedTo: '', comments: '' });
    }
  }, [isOpen, viewMode, initialData?.id, initialData?.jobId, initialData?.unitNumber, initialData?.activeUnitNumber, initialData?.workflowState]);

  useEffect(() => {
    if (!isOpen || viewMode) return;
    const sole = (customers || []).length === 1 ? customers![0] : null;
    if (sole && !formData.company) {
      setFormData((prev) => ({ ...prev, company: sole.company }));
    }
  }, [isOpen, viewMode, customers, formData.company]);

  const selectedCompany = customers?.find(c => c.company === formData.company);
  const moiPersons = selectedCompany?.accountHolders?.filter(h => h.moi) || [];
  const requestedByOptions = useMemo(() => {
    const options = [...moiPersons];
    const ensureName = (name: string) => {
      const trimmed = name.trim();
      if (!trimmed || options.some((person) => person.name === trimmed)) return;
      options.push({ id: -options.length, name: trimmed, moi: false, moiApproval: false, moa: false });
    };
    ensureName(formData.requestedBy);
    ensureName(String(initialData?.requestedBy ?? ''));
    if (isClientUser && currentUserName) ensureName(currentUserName);
    return options;
  }, [moiPersons, formData.requestedBy, initialData?.requestedBy, isClientUser, currentUserName]);

  const packageNames = useMemo(() => {
    if (!selectedCompany) return [];
    const names = selectedCompany.packageNames?.length
      ? selectedCompany.packageNames
      : selectedCompany.package
        ? [selectedCompany.package]
        : [];
    return [...new Set(names.filter(Boolean))];
  }, [selectedCompany]);

  const customerPackage = selectedCompany && products
    ? products.find((p) => packageNames.includes(p.packageName)) ?? products.find((p) => p.packageName === selectedCompany.package)
    : null;

  const availableServices = useMemo(() => {
    const services = new Set<string>();
    if (initialData?.service) services.add(String(initialData.service));
    if (initialData?.typeOfDocument) services.add(String(initialData.typeOfDocument));
    if (formData.typeOfDocument) services.add(formData.typeOfDocument);
    if (products?.length) {
      products.forEach((product) => {
        if (packageNames.includes(product.packageName)) {
          product.services?.forEach((service) => services.add(service));
        }
      });
    }
    customerPackage?.services?.forEach((service) => services.add(service));
    return [...services];
  }, [products, packageNames, customerPackage, formData.typeOfDocument, initialData?.service, initialData?.typeOfDocument]);

  // Get usage info for selected service
  const selectedServiceUsage = serviceUsage?.find(
    s => s.service === formData.typeOfDocument && selectedCompany?.company
  );

  // Get quantity for selected service from customer's package
  const serviceQty = customerPackage?.serviceQuantities?.[formData.typeOfDocument] || 0;

  const requestedPerson = selectedCompany?.accountHolders.find(h => h.name === formData.requestedBy);
  const requiresApproval = formData.requestedBy !== '' && !requestedPerson?.moi;

  const resolvedJobId = jobId ?? initialData?.jobId;
  const resolvedUnitNumber = initialData?.unitNumber
    ?? initialData?.activeUnitNumber
    ?? ((initialData?.totalQty ?? 1) <= 1 ? 1 : undefined);
  const itemLabel = resolvedUnitNumber ? ` (session #${resolvedUnitNumber})` : '';

  const buildFormPayload = () => ({
    ...formData,
    id: initialData?.id,
    jobId: resolvedJobId,
    company: formData.company || String(initialData?.company ?? ''),
    unitNumber: resolvedUnitNumber,
    activeUnitNumber: resolvedUnitNumber,
    workflowState,
    updatedAt: initialData?.updatedAt,
  });

  const ensureJobForDocuments = async () => {
    if (resolvedJobId) {
      return { jobId: Number(resolvedJobId), unitNumber: resolvedUnitNumber };
    }
    if (!onSaveDraft) {
      throw new Error('Save the MOI first so attachments can be stored.');
    }
    return onSaveDraft(buildFormPayload());
  };

  const handleSubmitForApprovalClick = () => {
    if (!onSubmitForApproval || actionPending) return;
    setFooterError('');
    if (formData.supportingDocument && documentCount === 0) {
      const message = 'Attach at least one supporting document before submitting for approval.';
      setDocumentSubmitError(message);
      setFooterError(message);
      return;
    }
    if (!formRef.current?.reportValidity()) {
      setFooterError('Please complete all required fields before submitting.');
      return;
    }
    setDocumentSubmitError('');
    setActionPending(true);
    void Promise.resolve(onSubmitForApproval(initialData?.id ?? 0, buildFormPayload()))
      .finally(() => setActionPending(false));
  };

  const handleSaveClick = () => {
    if (actionPending) return;
    setFooterError('');
    if (!formRef.current?.reportValidity()) {
      setFooterError('Please complete all required fields before saving.');
      return;
    }
    setActionPending(true);
    void Promise.resolve(onSubmit(buildFormPayload()))
      .finally(() => setActionPending(false));
  };

  const handleAddApprovalPerson = () => {
    setFormData({
      ...formData,
      approvalPersons: [...formData.approvalPersons, { name: '', position: '' }]
    });
  };

  const handleRemoveApprovalPerson = (index: number) => {
    const newApprovalPersons = formData.approvalPersons.filter((_, i) => i !== index);
    setFormData({
      ...formData,
      approvalPersons: newApprovalPersons.length > 0 ? newApprovalPersons : [{ name: '', position: '' }]
    });
  };

  const handleApprovalPersonChange = (index: number, field: 'name' | 'position', value: string) => {
    const newApprovalPersons = [...formData.approvalPersons];
    newApprovalPersons[index][field] = value;
    setFormData({ ...formData, approvalPersons: newApprovalPersons });
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    handleSaveClick();
  };

  const handleAccept = () => {
    if (onAccept && jobId && acceptanceData.assignedTo) {
      onAccept(jobId, acceptanceData.assignedTo, acceptanceData.comments);
      setAcceptanceData({ assignedTo: '', comments: '' });
    }
  };

  const handleClose = () => {
    setFormData({
      company: '',
      documentTitle: '',
      backgroundInfo: '',
      typeOfDocument: '',
      supportingDocument: false,
      attachedFiles: [],
      approvedTemplate: false,
      documentsExecuted: false,
      reasonForRatification: '',
      withLOA: false,
      approvalPersons: [{ name: '', position: '' }],
      requestedBy: '',
      requestedDate: '',
      approvedBy: '',
      approvedDate: '',
      approvalComments: '',
      turnaroundWeeks: '',
      draftCanBeAmended: false,
      urgent: false,
      urgentReason: '',
      requiredExecutionDate: '',
    });
    setAcceptanceData({ assignedTo: '', comments: '' });
    onClose();
  };


  const handleDownloadPack = async () => {
    const formId = initialData?.id as number | undefined;
    if (!formId) return;
    setExportingPack(true);
    setExportPackError('');
    try {
      const blob = await downloadMoiExportPack(formId);
      saveBlobAsFile(blob, `moi-${formId}-pack.json`);
    } catch (err) {
      setExportPackError(err instanceof ApiError ? err.message : 'Failed to download pack.');
    } finally {
      setExportingPack(false);
    }
  };

  if (!isOpen) return null;

  const dismissLabel = closeLabel ?? (viewMode ? 'Close' : 'Cancel');

  return (
    <div className={`fixed inset-0 bg-black/50 flex items-center justify-center p-4 ${elevated ? 'z-[60]' : 'z-50'}`}>
      <div className="bg-card rounded-lg border border-border w-full max-w-4xl max-h-[90vh] overflow-hidden flex flex-col">
        <div className="p-6 border-b border-border flex items-center justify-between gap-3">
          <div className="min-w-0">
            <h2>Memorandum of Instruction (MOI) {viewMode && '- View Mode'}</h2>
            {exportPackError && (
              <p className="text-xs text-destructive mt-1">{exportPackError}</p>
            )}
          </div>
          <div className="flex items-center gap-2 shrink-0">
          {initialData?.id && (
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
          <button
            type="button"
            onClick={handleClose}
            className="px-3 py-1.5 text-sm border border-border rounded-lg hover:bg-muted transition-colors"
          >
            {dismissLabel}
          </button>
          </div>
        </div>

        {showIntakeReview && (
          <div className="px-6 py-4 border-b border-amber-200 bg-amber-50 text-amber-950">
            <p className="font-semibold">MOI intake review</p>
            <p className="text-sm mt-1 text-amber-900/80">
              The client has submitted this MOI. Sign off to accept it, then assign the secretarial team to start resolution / MOA prep. Or reject to send it back with a reason.
            </p>
          </div>
        )}

        {showClientRejectionBanner && (
          <div className="px-6 py-4 border-b border-red-200 bg-red-50 text-red-950">
            <p className="font-semibold">MOI rejected</p>
            <p className="text-sm mt-1 text-red-900/80">
              Please update the form below and submit again when you are ready.
            </p>
            {latestRejection && (
              <p className="text-sm mt-2 text-red-900">
                <span className="font-medium">{latestRejection.userName}</span>
                {latestRejection.rejectedAt ? ` (${latestRejection.rejectedAt})` : ''}: {latestRejection.reason}
              </p>
            )}
          </div>
        )}

        <form ref={formRef} onSubmit={handleSubmit} className="flex-1 overflow-y-auto">
          <div className="p-6 space-y-6">
            {jobId && initialData?.signerName && (
              <div className="bg-primary/5 border border-primary/20 rounded-lg px-4 py-3 text-sm">
                <span className="text-muted-foreground">Send to external signer: </span>
                <span className="font-medium">{initialData.signerName}</span>
                {(initialData.signerEmail || initialData.signerPhone) && (
                  <span className="text-muted-foreground">
                    {' '}({[initialData.signerEmail, initialData.signerPhone].filter(Boolean).join(' · ')})
                  </span>
                )}
              </div>
            )}
            {/* Fixed Fields */}
            <div className="bg-muted/30 rounded-lg p-4 space-y-2">
              <div className="flex items-center justify-between">
                <span className="text-sm text-muted-foreground">Description:</span>
                <span className="font-medium">{formTemplate?.description || 'Memorandum of Instruction'}</span>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-sm text-muted-foreground">To:</span>
                <span className="font-medium">{formTemplate?.addressedTo || 'Head of Legal & Secretarial Department'}</span>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-sm text-muted-foreground">Division:</span>
                <span className="font-medium">{formTemplate?.divisionLabel || 'Secretarial Division'}</span>
              </div>
              {formTemplate?.issuerEntity && (
                <div className="flex items-center justify-between">
                  <span className="text-sm text-muted-foreground">Issuer:</span>
                  <span className="font-medium">{formTemplate.issuerEntity}</span>
                </div>
              )}
              {resolvedUnitNumber && (
                <div className="flex items-center justify-between">
                  <span className="text-sm text-muted-foreground">Session:</span>
                  <span className="font-medium">#{resolvedUnitNumber}</span>
                </div>
              )}
              {workflowState && (
                <div className="flex items-center justify-between pt-1">
                  <span className="text-sm text-muted-foreground">Workflow:</span>
                  <span className="text-sm font-medium px-2 py-0.5 rounded bg-primary/10">
                    {moiWorkflowStateLabel(workflowState)}
                  </span>
                </div>
              )}
            </div>

            {(resolvedJobId || onSaveDraft) && (
              <JobItemDocumentsSection
                jobId={Number(resolvedJobId) || 0}
                unitNumber={resolvedUnitNumber}
                refreshKey={documentsRefreshKey}
                showWhenEmpty={formData.supportingDocument || !viewMode}
                onCountChange={setDocumentCount}
                countFolders={formData.supportingDocument ? ['supporting'] : undefined}
                folders={formData.supportingDocument ? ['supporting', 'moi'] : ['moi', 'supporting']}
                title={viewMode ? 'Documents for review' : 'Uploaded documents'}
                allowUpload={!viewMode}
                allowDelete={!viewMode}
                uploadFolder={formData.supportingDocument ? 'supporting' : 'moi'}
                onBeforeUpload={async () => {
                  const resolved = await ensureJobForDocuments();
                  setDocumentsRefreshKey((k) => k + 1);
                  return resolved;
                }}
              />
            )}

            {/* User Input Fields */}
            <div className="space-y-4">
              <div>
                <label className="block mb-2">Company *</label>
                <select
                  required
                  value={formData.company}
                  onChange={(e) => setFormData({ ...formData, company: e.target.value })}
                  className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                  disabled={viewMode}
                >
                  <option value="">Select Company</option>
                  {(customers || []).map((customer) => (
                    <option key={customer.id} value={customer.company}>
                      {customer.company}
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <label className="block mb-2">Type of Document *</label>
                <select
                  required
                  value={formData.typeOfDocument}
                  onChange={(e) => setFormData({ ...formData, typeOfDocument: e.target.value })}
                  className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                  disabled={!selectedCompany || viewMode}
                >
                  <option value="">Select Type of Document</option>
                  {(availableServices || []).map((service, index) => (
                    <option key={index} value={service}>
                      {service}
                    </option>
                  ))}
                </select>
                {!selectedCompany && (
                  <p className="text-sm text-muted-foreground mt-2">
                    Please select a company first
                  </p>
                )}
                {selectedCompany && formData.typeOfDocument && selectedServiceUsage && (
                  <div className="mt-2 p-3 bg-muted/30 rounded-lg">
                    <p className="text-sm">
                      <span className="text-muted-foreground">Package: </span>
                      <span className="font-medium">{selectedCompany.package}</span>
                    </p>
                    <p className="text-sm">
                      <span className="text-muted-foreground">Usage: </span>
                      <span className="font-medium">
                        {selectedServiceUsage.used}/{selectedServiceUsage.total}
                      </span>
                      <span className="text-muted-foreground ml-2">
                        ({selectedServiceUsage.total - selectedServiceUsage.used} remaining)
                      </span>
                    </p>
                  </div>
                )}
              </div>

              <div>
                <label className="block mb-2">Document Title *</label>
                <input
                  type="text"
                  required
                  value={formData.documentTitle}
                  onChange={(e) => setFormData({ ...formData, documentTitle: e.target.value })}
                  className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                  placeholder="Enter document title"
                  disabled={viewMode}
                />
              </div>

              <div>
                <label className="block mb-2">Background Info *</label>
                <textarea
                  required
                  rows={4}
                  value={formData.backgroundInfo}
                  onChange={(e) => setFormData({ ...formData, backgroundInfo: e.target.value })}
                  className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring resize-none"
                  placeholder="Enter background information"
                  disabled={viewMode}
                />
              </div>

              {/* Supporting Document */}
              <div className="border border-border rounded-lg p-4">
                <label className="flex items-center gap-2 cursor-pointer mb-3">
                  <input
                    type="checkbox"
                    checked={formData.supportingDocument}
                    onChange={(e) => setFormData({ ...formData, supportingDocument: e.target.checked, attachedFiles: e.target.checked ? formData.attachedFiles : [] })}
                    className="w-4 h-4 cursor-pointer"
                    disabled={viewMode}
                  />
                  <span>Supporting Document: YES</span>
                </label>

                {formData.supportingDocument && (
                  <div className="mt-3 space-y-2">
                    <p className="text-xs text-muted-foreground">
                      Use the documents section above to add or remove files for this session{itemLabel}.
                    </p>
                    {documentSubmitError && (
                      <p className="text-xs text-destructive">{documentSubmitError}</p>
                    )}
                    {documentCount > 0 && (
                      <p className="text-xs text-green-700">{documentCount} file{documentCount === 1 ? '' : 's'} attached</p>
                    )}
                  </div>
                )}
              </div>

              {/* Approved Template */}
              <div>
                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={formData.approvedTemplate}
                    onChange={(e) => setFormData({ ...formData, approvedTemplate: e.target.checked })}
                    className="w-4 h-4 cursor-pointer"
                    disabled={viewMode}
                  />
                  <span>Whether the agreement/letter is based on approved template by Management: YES</span>
                </label>
              </div>

              {/* Documents Executed */}
              <div className="border border-border rounded-lg p-4">
                <label className="flex items-center gap-2 cursor-pointer mb-3">
                  <input
                    type="checkbox"
                    checked={formData.documentsExecuted}
                    onChange={(e) => setFormData({ ...formData, documentsExecuted: e.target.checked, reasonForRatification: e.target.checked ? formData.reasonForRatification : '' })}
                    className="w-4 h-4 cursor-pointer"
                    disabled={viewMode}
                  />
                  <span>Whether agreement / relevant documents have been executed: YES</span>
                </label>

                {formData.documentsExecuted && (
                  <div className="mt-3">
                    <label className="block mb-2">Reason for Ratification *</label>
                    <textarea
                      required
                      rows={3}
                      value={formData.reasonForRatification}
                      onChange={(e) => setFormData({ ...formData, reasonForRatification: e.target.value })}
                      className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring resize-none"
                      placeholder="Enter reason for ratification"
                      disabled={viewMode}
                    />
                  </div>
                )}
              </div>

              <div className="border border-border rounded-lg p-4 space-y-3">
                <h4 className="text-sm font-medium">MOA routing conditions</h4>
                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={formData.financeRelated}
                    onChange={(e) => setFormData({ ...formData, financeRelated: e.target.checked })}
                    className="w-4 h-4"
                    disabled={viewMode}
                  />
                  <span className="text-sm">Involves financing, payments, or compliance affecting audited financial statements</span>
                </label>
                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={formData.bankSignatoryMatter}
                    onChange={(e) => setFormData({ ...formData, bankSignatoryMatter: e.target.checked })}
                    className="w-4 h-4"
                    disabled={viewMode}
                  />
                  <span className="text-sm">Involves opening, closing, or changing bank signatories</span>
                </label>
              </div>

              {/* With LOA */}
              <div className="border border-border rounded-lg p-4">
                <label className="flex items-center gap-2 cursor-pointer mb-3">
                  <input
                    type="checkbox"
                    checked={formData.withLOA}
                    onChange={(e) => setFormData({ ...formData, withLOA: e.target.checked })}
                    className="w-4 h-4 cursor-pointer"
                    disabled={viewMode}
                  />
                  <span>With Limits of Authority (LOA): YES</span>
                </label>

                {formData.withLOA && (
                  <div className="mt-3 space-y-4">
                    <label className="block">Last Point of Approval</label>
                    {formData.approvalPersons.map((person, index) => (
                      <div key={index} className="border border-border rounded-lg p-4 bg-muted/30">
                        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                          <div>
                            <label className="block mb-2">Name *</label>
                            <input
                              type="text"
                              required
                              value={person.name}
                              onChange={(e) => handleApprovalPersonChange(index, 'name', e.target.value)}
                              className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                              placeholder="Enter name"
                              disabled={viewMode}
                            />
                          </div>
                          <div>
                            <label className="block mb-2">Position *</label>
                            <input
                              type="text"
                              required
                              value={person.position}
                              onChange={(e) => handleApprovalPersonChange(index, 'position', e.target.value)}
                              className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                              placeholder="Enter position"
                              disabled={viewMode}
                            />
                          </div>
                        </div>
                        {!viewMode && formData.approvalPersons.length > 1 && (
                          <button
                            type="button"
                            onClick={() => handleRemoveApprovalPerson(index)}
                            className="mt-3 text-destructive hover:text-destructive/80 transition-colors"
                          >
                            Remove
                          </button>
                        )}
                      </div>
                    ))}
                    {!viewMode && (
                      <button
                        type="button"
                        onClick={handleAddApprovalPerson}
                        className="w-full px-4 py-2 border border-border rounded-lg bg-secondary text-secondary-foreground hover:bg-secondary/90 transition-colors"
                      >
                        + Add More
                      </button>
                    )}
                  </div>
                )}
              </div>

              {/* Requested By */}
              <div className="border border-border rounded-lg p-4">
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <label className="block mb-2">Requested by *</label>
                    <select
                      required
                      value={formData.requestedBy}
                      onChange={(e) => setFormData({ ...formData, requestedBy: e.target.value, approvedBy: '', approvedDate: '', approvalComments: '' })}
                      className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                      disabled={viewMode}
                    >
                      <option value="">Select Person</option>
                      {requestedByOptions.map((person) => (
                        <option key={`${person.id}-${person.name}`} value={person.name}>
                          {person.name}
                        </option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label className="block mb-2">Date *</label>
                    <DateInput
                      required
                      fullWidth
                      value={formData.requestedDate}
                      onChange={(iso) => setFormData({ ...formData, requestedDate: iso })}
                      disabled={viewMode}
                      className="px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                    />
                  </div>
                </div>
              </div>

              {/* Approved By - Only shown if Requested By person doesn't have MOI */}
              {requiresApproval && (
                <div className="border border-border rounded-lg p-4 bg-yellow-50 dark:bg-yellow-950/20">
                  <p className="text-sm text-muted-foreground mb-4">
                    The selected person does not have MOI authority. Approval is required.
                  </p>
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-4">
                    <div>
                      <label className="block mb-2">Approved by *</label>
                      <select
                        required
                        value={formData.approvedBy}
                        onChange={(e) => setFormData({ ...formData, approvedBy: e.target.value })}
                        className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                        disabled={viewMode}
                      >
                        <option value="">Select Person</option>
                        {(moiPersons || []).filter(p => p.name !== formData.requestedBy).map((person) => (
                          <option key={person.id} value={person.name}>
                            {person.name}
                          </option>
                        ))}
                      </select>
                    </div>
                    <div>
                      <label className="block mb-2">Date *</label>
                      <DateInput
                        required
                        fullWidth
                        value={formData.approvedDate}
                        onChange={(iso) => setFormData({ ...formData, approvedDate: iso })}
                        disabled={viewMode}
                        className="px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                      />
                    </div>
                  </div>
                  <div>
                    <label className="block mb-2">Comments</label>
                    <textarea
                      rows={3}
                      value={formData.approvalComments}
                      onChange={(e) => setFormData({ ...formData, approvalComments: e.target.value })}
                      className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring resize-none"
                      placeholder="Enter approval comments..."
                      disabled={viewMode}
                    />
                  </div>
                </div>
              )}

              {/* Turnaround Period */}
              <div className="border-t border-border pt-6">
                <h3 className="mb-4">Turnaround Period</h3>

                <div className="space-y-4">
                  <div>
                    <label className="block mb-2">Turnaround Period *</label>
                    <div className="flex items-center gap-2">
                      <input
                        type="number"
                        required
                        min="1"
                        value={formData.turnaroundWeeks}
                        onChange={(e) => setFormData({ ...formData, turnaroundWeeks: e.target.value })}
                        className="w-32 px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                        placeholder="0"
                        disabled={viewMode}
                      />
                      <span className="text-muted-foreground">week(s)</span>
                    </div>
                  </div>

                  <div>
                    <label className="flex items-center gap-2 cursor-pointer">
                      <input
                        type="checkbox"
                        checked={formData.draftCanBeAmended}
                        onChange={(e) => setFormData({ ...formData, draftCanBeAmended: e.target.checked })}
                        className="w-4 h-4 cursor-pointer"
                        disabled={viewMode}
                      />
                      <span>If draft resolution is provided, please indicate if draft can be amended</span>
                    </label>
                  </div>

                  <div className="border border-border rounded-lg p-4">
                    <label className="flex items-center gap-2 cursor-pointer mb-3">
                      <input
                        type="checkbox"
                        checked={formData.urgent}
                        onChange={(e) => setFormData({ ...formData, urgent: e.target.checked, urgentReason: e.target.checked ? formData.urgentReason : '' })}
                        className="w-4 h-4 cursor-pointer"
                        disabled={viewMode}
                      />
                      <span>Urgent</span>
                    </label>

                    {formData.urgent && (
                      <div className="mt-3">
                        <label className="block mb-2">Reason *</label>
                        <textarea
                          required
                          rows={3}
                          value={formData.urgentReason}
                          onChange={(e) => setFormData({ ...formData, urgentReason: e.target.value })}
                          className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring resize-none"
                          placeholder="Enter reason for urgency..."
                          disabled={viewMode}
                        />
                      </div>
                    )}
                  </div>

                  <div>
                    <label className="block mb-2">Required Date of Execution *</label>
                    <DateInput
                      required
                      fullWidth
                      value={formData.requiredExecutionDate}
                      onChange={(iso) => setFormData({ ...formData, requiredExecutionDate: iso })}
                      disabled={viewMode}
                      className="px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                    />
                  </div>
                </div>
              </div>

              {/* Acceptance Section - Only shown for pending jobs */}
              {isPendingJob && (
                <div className="border-t border-border pt-6">
                  <h3 className="mb-4">Accept Job Request</h3>
                  <div className="space-y-4 bg-muted/30 rounded-lg p-4">
                    <div>
                      <label className="block mb-2">Assigned to *</label>
                      <select
                        value={acceptanceData.assignedTo}
                        onChange={(e) => setAcceptanceData({ ...acceptanceData, assignedTo: e.target.value })}
                        className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                      >
                        <option value="">Select User</option>
                        {users.map((user) => (
                          <option key={user.id} value={user.name}>
                            {user.name}
                          </option>
                        ))}
                      </select>
                    </div>
                    <div>
                      <label className="block mb-2">Comments</label>
                      <textarea
                        rows={3}
                        value={acceptanceData.comments}
                        onChange={(e) => setAcceptanceData({ ...acceptanceData, comments: e.target.value })}
                        className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring resize-none"
                        placeholder="Enter any comments (optional)..."
                      />
                    </div>
                  </div>
                </div>
              )}
            </div>
          </div>

          {clientApprovals.length > 0 && (viewMode || workflowState === 'Approved') && (
            <div className="px-6 pb-2">
              <ClientSignOffTrail title="MOI sign-off record" approvals={clientApprovals} />
            </div>
          )}

          <div className="p-6 border-t border-border flex justify-between items-center gap-3">
            <div>
              {viewMode && workflowState === 'PendingPrep' && (
                <p className="text-sm text-muted-foreground border border-border rounded-lg p-3">
                  Client has submitted this MOI. Internal secretary: complete the form and save to send for recommendation.
                </p>
              )}
              {viewMode && initialData?.id && workflowState === 'PendingRecommendation' && onRecommend && (
                <div className="flex items-center gap-2">
                  <input
                    className="px-3 py-2 border border-border rounded-lg text-sm"
                    placeholder="Recommendation comments"
                    value={recommendComments}
                    onChange={(e) => setRecommendComments(e.target.value)}
                  />
                  <button
                    type="button"
                    onClick={() => onRecommend(initialData.id, recommendComments)}
                    className="px-4 py-2 bg-primary text-primary-foreground rounded-lg text-sm"
                  >
                    Recommend
                  </button>
                </div>
              )}
              {viewMode && initialData?.id && onClientApprove && canSignMoi && (
                <div className="space-y-3 max-w-lg">
                  <p className="text-sm text-muted-foreground">
                    Review the MOI below, draw your signature (or upload an image/PDF), and sign to continue.
                  </p>
                  <SignatureCapture value={signature} onChange={setSignature} />
                  <input
                    className="w-full px-3 py-2 border border-border rounded-lg text-sm"
                    placeholder="Comments (optional)"
                    value={recommendComments}
                    onChange={(e) => setRecommendComments(e.target.value)}
                  />
                  <div className="flex flex-wrap gap-2">
                    <button
                      type="button"
                      onClick={() => {
                        if (!signature) return;
                        onClientApprove(initialData.id, {
                          comments: recommendComments,
                          signatureFileName: signature.fileName,
                          signatureDataUrl: signature.dataUrl,
                        });
                      }}
                      disabled={!signature}
                      className="px-4 py-2 bg-primary text-primary-foreground rounded-lg text-sm disabled:opacity-50"
                    >
                      Sign MOI
                    </button>
                    {onClientReject && (
                      <button
                        type="button"
                        onClick={() => {
                          if (!rejectReason.trim()) return;
                          onClientReject(initialData.id, rejectReason.trim());
                        }}
                        disabled={!rejectReason.trim()}
                        className="px-4 py-2 border border-destructive text-destructive rounded-lg text-sm disabled:opacity-50"
                      >
                        Reject MOI
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
              {initialData?.rejections?.length > 0 && (
                <div className="mt-4 p-3 border border-amber-200 bg-amber-50 rounded-lg text-sm space-y-2">
                  <p className="font-medium text-amber-900">Previous rejections</p>
                  {initialData.rejections.map((r: { userName: string; reason: string; rejectedAt: string }, i: number) => (
                    <p key={i} className="text-amber-900">
                      <span className="font-medium">{r.userName}</span> ({r.rejectedAt}): {r.reason}
                    </p>
                  ))}
                </div>
              )}
              {viewMode && workflowState === 'PendingClientMoiApproval' && pendingApprovers.length > 0 && !canSignMoi && (
                <p className="text-sm text-muted-foreground">
                  Awaiting signature from: {formatPendingApproverList(pendingApprovers, currentUserName, signatoryHolderNames)}
                </p>
              )}
              {userIsAdmin && viewMode && initialData?.id && workflowState !== 'Approved' && onAdminOverride && !showIntakeReview && (
                <button
                  type="button"
                  onClick={() => onAdminOverride(initialData.id, 'Admin override')}
                  className="px-3 py-1.5 text-xs text-muted-foreground border border-border rounded-lg hover:bg-muted"
                  title="Skip normal workflow and mark MOI approved (admin only)"
                >
                  Admin override
                </button>
              )}
            </div>
            <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
              {showIntakeReview ? (
                <div className="flex-1 space-y-3 w-full">
                  <label className="block text-sm font-medium">Rejection reason (required to reject)</label>
                  <textarea
                    rows={2}
                    className="w-full px-3 py-2 border border-border rounded-lg text-sm bg-input-background"
                    placeholder="Explain what the client needs to fix…"
                    value={intakeRejectReason}
                    onChange={(e) => setIntakeRejectReason(e.target.value)}
                  />
                  <div className="flex flex-wrap gap-3">
                    <button
                      type="button"
                      disabled={intakeActionPending}
                      onClick={() => {
                        void (async () => {
                          setIntakeActionPending(true);
                          try {
                            await onApproveIntake?.();
                            handleClose();
                          } finally {
                            setIntakeActionPending(false);
                          }
                        })();
                      }}
                      className="px-6 py-2.5 bg-green-600 text-white rounded-lg font-medium hover:bg-green-700 transition-colors disabled:opacity-50"
                    >
                      MOI sign-off
                    </button>
                    <button
                      type="button"
                      disabled={intakeActionPending || !intakeRejectReason.trim()}
                      onClick={() => {
                        void (async () => {
                          setIntakeActionPending(true);
                          try {
                            await onRejectIntake?.(intakeRejectReason.trim());
                            handleClose();
                          } finally {
                            setIntakeActionPending(false);
                          }
                        })();
                      }}
                      className="px-6 py-2.5 border-2 border-destructive text-destructive rounded-lg font-medium hover:bg-destructive/10 transition-colors disabled:opacity-50"
                    >
                      Reject MOI
                    </button>
                    <button
                      type="button"
                      onClick={handleClose}
                      className="px-6 py-2.5 border border-border rounded-lg hover:bg-muted transition-colors"
                    >
                      {dismissLabel}
                    </button>
                  </div>
                </div>
              ) : (
                <div className="flex flex-col items-end gap-2 ml-auto">
                  {(footerError || documentSubmitError) && (
                    <p className="text-sm text-destructive text-right max-w-md">
                      {footerError || documentSubmitError}
                    </p>
                  )}
                  <div className="flex gap-3">
                  <button
                    type="button"
                    onClick={handleClose}
                    className="px-6 py-2 border border-border rounded-lg hover:bg-muted transition-colors"
                    disabled={actionPending}
                  >
                    {dismissLabel}
                  </button>
                  {!viewMode && (
                    <button
                      type="button"
                      onClick={handleSaveClick}
                      disabled={actionPending}
                      className="px-6 py-2 bg-primary text-primary-foreground rounded-lg hover:bg-primary/90 transition-colors disabled:opacity-50"
                    >
                      {actionPending ? 'Saving…' : 'Save MOI'}
                    </button>
                  )}
                  {!viewMode && canClientResubmit && onSubmitForApproval && (
                    <button
                      type="button"
                      onClick={handleSubmitForApprovalClick}
                      disabled={actionPending}
                      className="px-6 py-2 bg-green-600 text-white rounded-lg hover:bg-green-700 transition-colors disabled:opacity-50"
                    >
                      {actionPending
                        ? 'Submitting…'
                        : (isMoiRejectedState || hasRejectionHistory ? 'Resubmit for approval' : 'Submit for approval')}
                    </button>
                  )}
                  {isPendingJob && (
                    <button
                      type="button"
                      onClick={handleAccept}
                      disabled={!acceptanceData.assignedTo}
                      className="px-6 py-2 bg-primary text-primary-foreground rounded-lg hover:bg-primary/90 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                    >
                      Accept job
                    </button>
                  )}
                  </div>
                </div>
              )}
            </div>
          </div>
        </form>
      </div>
    </div>
  );
}
