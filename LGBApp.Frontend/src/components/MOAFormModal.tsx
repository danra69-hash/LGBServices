import { Upload, X } from 'lucide-react';
import { useState, useEffect } from 'react';
import {
  adminOverrideMoaStep,
  approveMoaWorkflowStep,
  resolveFormTemplate,
  type FormTemplateDto,
  type MoaPackChecklistDto,
  type WorkflowInstanceDto,
} from '@/lib/api';

interface Customer {
  id: number;
  company: string;
  package: string;
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

interface MOAFormModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (data: any) => void;
  onStartWorkflow?: (moaFormId: number) => void;
  onClientApprove?: (moaFormId: number, payload: { comments: string; signatureFileName?: string; signatureDataUrl?: string }) => void;
  onClientReject?: (moaFormId: number, reason: string) => void;
  onSharonApprove?: (jobId: number) => void;
  onSharonReject?: (jobId: number, reason: string) => void;
  onSendToClient?: (jobId: number) => void;
  jobHandoffStatus?: string;
  viewMode?: boolean;
  initialData?: any;
  moiData?: any;
  users?: User[];
  customers: Customer[];
  userIsAdmin?: boolean;
  canApproveMoa?: boolean;
  isClientUser?: boolean;
}

export function MOAFormModal({
  isOpen,
  onClose,
  onSubmit,
  onStartWorkflow,
  onClientApprove,
  onClientReject,
  onSharonApprove,
  onSharonReject,
  onSendToClient,
  jobHandoffStatus = '',
  viewMode = false,
  initialData,
  moiData,
  users = [],
  customers,
  userIsAdmin = false,
  canApproveMoa = false,
  isClientUser = false,
}: MOAFormModalProps) {
  const [formTemplate, setFormTemplate] = useState<FormTemplateDto | null>(null);
  const [workflow, setWorkflow] = useState<WorkflowInstanceDto | null>(null);
  const [packChecklist, setPackChecklist] = useState<MoaPackChecklistDto>(emptyPackChecklist);
  const [packErrors, setPackErrors] = useState<string[]>([]);
  const [stepComments, setStepComments] = useState('');
  const [clientSignComments, setClientSignComments] = useState('');
  const [rejectReason, setRejectReason] = useState('');
  const [sharonRejectReason, setSharonRejectReason] = useState('');
  const [signatureFile, setSignatureFile] = useState<File | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [formData, setFormData] = useState({
    company: '',
    typeOfDocument: '',
    projectInitiator: '',
    preparedByInternal: '',
    vettedByInternal: '',
    preparedByExternal: '',
    vettedByExternal: '',
    seniorManagerApproval: {
      approved: false,
      date: '',
      comments: '',
      resoForDLCM: false
    },
    managerRegulatoryApproval: {
      approved: false,
      date: '',
      comments: ''
    },
    moaPersonsApprovals: [] as { name: string; approved: boolean; date: string; comments: string }[],
    financeRelated: false,
    bankSignatoryMatter: false,
    shareMovement: false,
    formTemplateCode: '',
    moiFormId: undefined as number | undefined,
  });

  const emptyFormData = {
    company: '',
    typeOfDocument: '',
    projectInitiator: '',
    preparedByInternal: '',
    vettedByInternal: '',
    preparedByExternal: '',
    vettedByExternal: '',
    seniorManagerApproval: {
      approved: false,
      date: '',
      comments: '',
      resoForDLCM: false,
    },
    managerRegulatoryApproval: {
      approved: false,
      date: '',
      comments: '',
    },
    moaPersonsApprovals: [] as { name: string; approved: boolean; date: string; comments: string }[],
  };

  useEffect(() => {
    if (!isOpen) return;
    if (moiData) {
      const selectedCompany = customers?.find(c => c.company === moiData.company);
      const moaPersons = selectedCompany?.accountHolders?.filter(h => h.moa) || [];

      setFormData({
        company: moiData.company || '',
        typeOfDocument: moiData.typeOfDocument || '',
        projectInitiator: moiData.requestedBy || '',
        preparedByInternal: '',
        vettedByInternal: '',
        preparedByExternal: '',
        vettedByExternal: '',
        seniorManagerApproval: {
          approved: false,
          date: '',
          comments: '',
          resoForDLCM: false
        },
        managerRegulatoryApproval: {
          approved: false,
          date: '',
          comments: ''
        },
        moaPersonsApprovals: moaPersons.map(person => ({
          name: person.name,
          approved: false,
          date: '',
          comments: ''
        })),
        financeRelated: Boolean(moiData.financeRelated),
        bankSignatoryMatter: Boolean(moiData.bankSignatoryMatter),
        shareMovement: false,
        formTemplateCode: String(moiData.formTemplateCode ?? ''),
        moiFormId: moiData.id as number | undefined,
      });
      if (moiData.workflow) setWorkflow(moiData.workflow as WorkflowInstanceDto);
      return;
    }
    if (!viewMode) {
      setFormData(emptyFormData);
    }
  }, [isOpen, moiData, customers, viewMode]);

  // Populate form with initialData when in view mode
  useEffect(() => {
    if (viewMode && initialData) {
      setFormData(initialData);
    }
  }, [viewMode, initialData]);

  const selectedCompany = customers?.find(c => c.company === formData.company);

  useEffect(() => {
    if (!isOpen || !formData.company) return;
    resolveFormTemplate('MOA', formData.company, formData.formTemplateCode || undefined)
      .then(setFormTemplate)
      .catch(() => setFormTemplate(null));
  }, [isOpen, formData.company, formData.formTemplateCode]);

  useEffect(() => {
    if (initialData?.workflow) setWorkflow(initialData.workflow);
    if (initialData?.packChecklist) setPackChecklist(initialData.packChecklist as MoaPackChecklistDto);
    if (initialData?.packValidationErrors) setPackErrors(initialData.packValidationErrors as string[]);
  }, [initialData]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    try {
      await onSubmit({
        ...formData,
        id: moaFormId,
        jobId: initialData?.jobId ?? moiData?.jobId,
        packChecklist,
      });
    } finally {
      setSubmitting(false);
    }
  };

  const moaFormId = (initialData?.id ?? moiData?.id) as number | undefined;

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
    setFormData({
      company: '',
      typeOfDocument: '',
      projectInitiator: '',
      preparedByInternal: '',
      vettedByInternal: '',
      preparedByExternal: '',
      vettedByExternal: '',
      seniorManagerApproval: {
        approved: false,
        date: '',
        comments: '',
        resoForDLCM: false
      },
      managerRegulatoryApproval: {
        approved: false,
        date: '',
        comments: ''
      },
      moaPersonsApprovals: []
    });
    onClose();
  };

  const handleMOAPersonApprovalChange = (index: number, field: 'approved' | 'date' | 'comments', value: any) => {
    const newApprovals = [...formData.moaPersonsApprovals];
    newApprovals[index] = { ...newApprovals[index], [field]: value };
    setFormData({ ...formData, moaPersonsApprovals: newApprovals });
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
      <div className="bg-card rounded-lg border border-border w-full max-w-4xl max-h-[90vh] overflow-hidden flex flex-col">
        <div className="p-6 border-b border-border flex items-center justify-between">
          <h2>Memorandum of Approval (MOA) {viewMode && '- View Mode'}</h2>
          <button
            onClick={handleClose}
            className="p-1 hover:bg-muted rounded transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
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
              {workflow && (
                <div className="flex items-center justify-between">
                  <span className="text-sm text-muted-foreground">Workflow:</span>
                  <span className="text-sm font-medium">{workflow.templateCode} — {workflow.status}</span>
                </div>
              )}
            </div>

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
                {users.map((user) => (
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
                {users.map((user) => (
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

            {!workflow && (
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
                  <input className="px-3 py-2 border border-border rounded-lg text-sm md:col-span-2" placeholder="SSM as-at date (yyyy-mm-dd) *" value={packChecklist.ssmAsAtDate} onChange={(e) => setPackChecklist({ ...packChecklist, ssmAsAtDate: e.target.value })} disabled={viewMode} />
                </div>
                {packErrors.length > 0 && (
                  <ul className="text-xs text-destructive list-disc pl-4">
                    {packErrors.map((err) => <li key={err}>{err}</li>)}
                  </ul>
                )}
                {moaFormId && onStartWorkflow && !viewMode && !isClientUser && (
                  <button
                    type="button"
                    className="px-4 py-2 border border-border rounded-lg text-sm hover:bg-muted"
                    onClick={() => onStartWorkflow(moaFormId)}
                  >
                    Start internal routing (optional)
                  </button>
                )}
                <p className="text-xs text-muted-foreground">
                  Internal secretary completes this MOA, then Sharon approves before it is sent to the client for sign-off.
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

            {workflow && (
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

            {/* Legacy parallel approvals — hidden when workflow active */}
            <div className={`border-t border-border pt-6 ${workflow ? 'hidden' : ''}`}>
              <h3 className="mb-4">Approved By</h3>

              {/* Senior Manager, Company Secretary */}
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
                      <input
                        type="date"
                        value={formData.seniorManagerApproval.date}
                        onChange={(e) => setFormData({
                          ...formData,
                          seniorManagerApproval: {
                            ...formData.seniorManagerApproval,
                            date: e.target.value
                          }
                        })}
                        className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
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

              {/* Manager - Regulatory & Compliance */}
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
                      <input
                        type="date"
                        value={formData.managerRegulatoryApproval.date}
                        onChange={(e) => setFormData({
                          ...formData,
                          managerRegulatoryApproval: {
                            ...formData.managerRegulatoryApproval,
                            date: e.target.value
                          }
                        })}
                        className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
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
                            <input
                              type="date"
                              value={approval.date}
                              onChange={(e) => handleMOAPersonApprovalChange(index, 'date', e.target.value)}
                              className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
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

          {initialData?.rejections?.length > 0 && (
            <div className="px-6 pb-4">
              <div className="p-3 border border-amber-200 bg-amber-50 rounded-lg text-sm space-y-2">
                <p className="font-medium text-amber-900">Previous rejections</p>
                {initialData.rejections.map((r: { userName: string; reason: string; rejectedAt: string }, i: number) => (
                  <p key={i} className="text-amber-900">
                    <span className="font-medium">{r.userName}</span> ({r.rejectedAt}): {r.reason}
                  </p>
                ))}
              </div>
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
              {isClientUser && viewMode && onClientApprove && initialData?.id && (
                <div className="space-y-3 max-w-lg">
                  <label className="flex items-center gap-2 text-sm cursor-pointer">
                    <Upload className="w-4 h-4 text-muted-foreground" />
                    <span>{signatureFile ? signatureFile.name : 'Attach signature (image or PDF)'}</span>
                    <input
                      type="file"
                      accept="image/*,.pdf"
                      className="hidden"
                      onChange={(e) => setSignatureFile(e.target.files?.[0] ?? null)}
                    />
                  </label>
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
                        if (!signatureFile) return;
                        const reader = new FileReader();
                        reader.onload = () => {
                          onClientApprove(initialData.id, {
                            comments: clientSignComments,
                            signatureFileName: signatureFile.name,
                            signatureDataUrl: String(reader.result ?? ''),
                          });
                        };
                        reader.readAsDataURL(signatureFile);
                      }}
                      disabled={!signatureFile}
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
                  Awaiting sign-off from: {initialData.pendingApprovers.join(', ')}
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
            {!viewMode && !workflow && !isClientUser && (
              <button
                type="submit"
                disabled={submitting}
                className="px-6 py-2 bg-primary text-primary-foreground rounded-lg hover:bg-primary/90 transition-colors disabled:opacity-50"
              >
                {submitting ? 'Saving…' : 'Save MOA'}
              </button>
            )}
            {workflow && !isClientUser && (
              <button type="button" onClick={handleClose} className="px-6 py-2 bg-primary text-primary-foreground rounded-lg">
                Done
              </button>
            )}
            </div>
          </div>
        </form>
      </div>
    </div>
  );
}
