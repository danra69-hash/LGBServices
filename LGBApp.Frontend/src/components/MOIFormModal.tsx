import { X, Upload, Paperclip, ArrowRight } from 'lucide-react';
import { useState, useEffect } from 'react';
import { resolveFormTemplate, type FormTemplateDto } from '@/lib/api';

interface Customer {
  id: number;
  company: string;
  package: string;
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
  onSubmit: (data: any) => void;
  onConvertToMOA?: (data: any) => void;
  onAccept?: (jobId: number, assignedTo: string, comments: string) => void;
  onRecommend?: (formId: number, comments: string) => void;
  onApproveMoi?: (formId: number, comments: string) => void;
  onAdminOverride?: (formId: number, comments: string) => void;
  userIsAdmin?: boolean;
  viewMode?: boolean;
  initialData?: any;
  jobId?: number;
  jobStatus?: 'Pending' | 'In Progress' | 'Completed' | 'Canceled';
  users?: { id: number; name: string }[];
  customers: Customer[];
  products: Product[];
  serviceUsage: ServiceUsage[];
}

export function MOIFormModal({
  isOpen, onClose, onSubmit, onConvertToMOA, onAccept, onRecommend, onApproveMoi, onAdminOverride,
  userIsAdmin = false, viewMode = false, initialData, jobId, jobStatus, users = [], customers, products, serviceUsage,
}: MOIFormModalProps) {
  const [formTemplate, setFormTemplate] = useState<FormTemplateDto | null>(null);
  const [workflowState, setWorkflowState] = useState('Draft');
  const [recommendComments, setRecommendComments] = useState('');
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
  const isInProgressJob = viewMode && jobStatus === 'In Progress';

  const emptyFormData = {
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
  };

  useEffect(() => {
    if (!isOpen || !formData.company) return;
    const service = String((formData as { service?: string }).service ?? formData.typeOfDocument ?? '');
    resolveFormTemplate('MOI', formData.company, formData.formTemplateCode || undefined, service || undefined)
      .then(setFormTemplate)
      .catch(() => setFormTemplate(null));
  }, [isOpen, formData.company, formData.formTemplateCode, formData.typeOfDocument]);

  // Reset or populate when modal opens
  useEffect(() => {
    if (!isOpen) return;
    if (!viewMode) {
      setFormData(emptyFormData);
      setAcceptanceData({ assignedTo: '', comments: '' });
      return;
    }
    if (initialData) {
      const d = { ...(initialData.data as Record<string, unknown> | undefined), ...initialData };
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
        requestedBy: String(d.requestedBy ?? d.signerName ?? ''),
        requestedDate: String(d.requestedDate ?? ''),
        approvedBy: String(d.approvedBy ?? ''),
        approvedDate: String(d.approvedDate ?? ''),
        approvalComments: String(d.approvalComments ?? ''),
        turnaroundWeeks: String(d.turnaroundPeriod ?? d.turnaroundWeeks ?? ''),
        draftCanBeAmended: Boolean(d.draftCanBeAmended),
        urgent: Boolean(d.urgent),
        urgentReason: String(d.urgentReason ?? ''),
        requiredExecutionDate: String(d.requiredDateOfExecution ?? d.requiredExecutionDate ?? ''),
        financeRelated: Boolean(d.financeRelated ?? initialData.financeRelated),
        bankSignatoryMatter: Boolean(d.bankSignatoryMatter ?? initialData.bankSignatoryMatter),
        formTemplateCode: String(d.formTemplateCode ?? initialData.formTemplateCode ?? ''),
      });
      setWorkflowState(String(initialData.workflowState ?? 'Draft'));
    }
  }, [isOpen, viewMode, initialData]);

  const selectedCompany = customers?.find(c => c.company === formData.company);
  const moiPersons = selectedCompany?.accountHolders?.filter(h => h.moi) || [];

  // Get customer's package and available services
  const customerPackage = selectedCompany && products ? products.find(p => p.packageName === selectedCompany.package) : null;
  const availableServices = customerPackage?.services || [];

  // Get usage info for selected service
  const selectedServiceUsage = serviceUsage?.find(
    s => s.service === formData.typeOfDocument && selectedCompany?.company
  );

  // Get quantity for selected service from customer's package
  const serviceQty = customerPackage?.serviceQuantities?.[formData.typeOfDocument] || 0;

  const requestedPerson = selectedCompany?.accountHolders.find(h => h.name === formData.requestedBy);
  const requiresApproval = formData.requestedBy !== '' && !requestedPerson?.moi;

  const handleFileUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files) {
      const filesArray = Array.from(e.target.files);
      setFormData({ ...formData, attachedFiles: [...formData.attachedFiles, ...filesArray] });
    }
  };

  const removeFile = (index: number) => {
    const newFiles = formData.attachedFiles.filter((_, i) => i !== index);
    setFormData({ ...formData, attachedFiles: newFiles });
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
    onSubmit(formData);
    onClose();
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

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
      <div className="bg-card rounded-lg border border-border w-full max-w-4xl max-h-[90vh] overflow-hidden flex flex-col">
        <div className="p-6 border-b border-border flex items-center justify-between">
          <h2>Memorandum of Instruction (MOI) {viewMode && '- View Mode'}</h2>
          <button
            onClick={handleClose}
            className="p-1 hover:bg-muted rounded transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="flex-1 overflow-y-auto">
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
              {workflowState && (
                <div className="flex items-center justify-between pt-1">
                  <span className="text-sm text-muted-foreground">Workflow:</span>
                  <span className="text-sm font-medium px-2 py-0.5 rounded bg-primary/10">{workflowState}</span>
                </div>
              )}
            </div>

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
                  <div className="mt-3">
                    <label className="flex items-center gap-2 px-4 py-2 border border-border rounded-lg bg-secondary text-secondary-foreground hover:bg-secondary/90 transition-colors cursor-pointer inline-flex">
                      <Paperclip className="w-4 h-4" />
                      <span>Attach Files</span>
                      <input
                        type="file"
                        multiple
                        onChange={handleFileUpload}
                        className="hidden"
                      />
                    </label>

                    {formData.attachedFiles.length > 0 && (
                      <div className="mt-3 space-y-2">
                        {formData.attachedFiles.map((file, index) => (
                          <div key={index} className="flex items-center justify-between bg-muted/30 rounded px-3 py-2">
                            <div className="flex items-center gap-2">
                              <Upload className="w-4 h-4 text-muted-foreground" />
                              <span className="text-sm">{file.name}</span>
                              <span className="text-xs text-muted-foreground">
                                ({(file.size / 1024).toFixed(2)} KB)
                              </span>
                            </div>
                            <button
                              type="button"
                              onClick={() => removeFile(index)}
                              className="text-destructive hover:text-destructive/80 text-sm"
                            >
                              Remove
                            </button>
                          </div>
                        ))}
                      </div>
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
                      {(moiPersons || []).map((person) => (
                        <option key={person.id} value={person.name}>
                          {person.name}
                        </option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label className="block mb-2">Date *</label>
                    <input
                      type="date"
                      required
                      value={formData.requestedDate}
                      onChange={(e) => setFormData({ ...formData, requestedDate: e.target.value })}
                      className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                      disabled={viewMode}
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
                      <input
                        type="date"
                        required
                        value={formData.approvedDate}
                        onChange={(e) => setFormData({ ...formData, approvedDate: e.target.value })}
                        className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                        disabled={viewMode}
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
                    <input
                      type="date"
                      required
                      value={formData.requiredExecutionDate}
                      onChange={(e) => setFormData({ ...formData, requiredExecutionDate: e.target.value })}
                      className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                      disabled={viewMode}
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

          <div className="p-6 border-t border-border flex justify-between items-center gap-3">
            <div>
              {isInProgressJob && onConvertToMOA && workflowState === 'Approved' && (
                <button
                  type="button"
                  onClick={() => {
                    onConvertToMOA({ ...formData, id: initialData?.id, moiFormId: initialData?.id });
                    handleClose();
                  }}
                  className="flex items-center gap-2 px-6 py-2 bg-secondary text-secondary-foreground rounded-lg hover:bg-secondary/90 transition-colors"
                >
                  <span>Convert to MOA</span>
                  <ArrowRight className="w-4 h-4" />
                </button>
              )}
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
              {viewMode && initialData?.id && workflowState === 'PendingMoiApproval' && onApproveMoi && (
                <button
                  type="button"
                  onClick={() => onApproveMoi(initialData.id, recommendComments)}
                  className="px-4 py-2 bg-primary text-primary-foreground rounded-lg text-sm"
                >
                  Approve MOI
                </button>
              )}
              {userIsAdmin && viewMode && initialData?.id && workflowState !== 'Approved' && onAdminOverride && (
                <button
                  type="button"
                  onClick={() => onAdminOverride(initialData.id, 'Admin override')}
                  className="px-4 py-2 border border-border rounded-lg text-sm"
                >
                  Admin override
                </button>
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
              {!viewMode && (
                <button
                  type="submit"
                  className="px-6 py-2 bg-primary text-primary-foreground rounded-lg hover:bg-primary/90 transition-colors"
                >
                  Submit MOI
                </button>
              )}
              {isPendingJob && (
                <button
                  type="button"
                  onClick={handleAccept}
                  disabled={!acceptanceData.assignedTo}
                  className="px-6 py-2 bg-primary text-primary-foreground rounded-lg hover:bg-primary/90 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  Accept
                </button>
              )}
            </div>
          </div>
        </form>
      </div>
    </div>
  );
}
