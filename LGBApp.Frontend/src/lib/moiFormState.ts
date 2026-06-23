import type { FormResponse, JobRequestResponse } from '@/lib/api';

/** Resolve MOI form id from unit metadata or job-level links (works during MOA phase). */
export function resolveLinkedMoiFormId(job: JobRequestResponse): number | undefined {
  const unitNumber = job.activeUnitNumber ?? ((job.totalQty ?? 1) <= 1 ? 1 : undefined);
  if (unitNumber != null) {
    const unit = job.units?.find((u) => u.unitNumber === unitNumber);
    if (unit?.moiFormId) return unit.moiFormId;
    if (unit?.linkedFormKind === 'MOI' && unit.linkedFormId) return unit.linkedFormId;
  }

  if (job.linkedFormKind === 'MOI' && job.linkedFormId) return job.linkedFormId;
  if (job.linkedFormKind === 'MOA') return undefined;
  return job.linkedFormId;
}

export function mapMoiFormResponseToModalState(
  form: FormResponse,
  job?: Pick<JobRequestResponse, 'id' | 'service' | 'taskType' | 'status' | 'totalQty' | 'activeUnitNumber'>,
): Record<string, unknown> {
  const unitNumber = form.unitNumber ?? job?.activeUnitNumber ?? undefined;
  const data = form.data ?? {};
  return {
    ...data,
    id: form.id,
    jobId: form.jobId ?? job?.id,
    company: String(form.company ?? data.company ?? ''),
    typeOfDocument: String(data.typeOfDocument ?? data.service ?? job?.service ?? ''),
    formTemplateCode: form.formTemplateCode ?? data.formTemplateCode,
    financeRelated: form.financeRelated ?? data.financeRelated,
    bankSignatoryMatter: form.bankSignatoryMatter ?? data.bankSignatoryMatter,
    workflowState: form.workflowState,
    clientApprovals: form.clientApprovals,
    rejections: form.rejections,
    requiredApprovers: form.requiredApprovers,
    pendingApprovers: form.pendingApprovers,
    unitNumber,
    activeUnitNumber: unitNumber,
    totalQty: job?.totalQty,
    status: job?.status,
    service: job?.service,
    taskType: job?.taskType,
    updatedAt: form.updatedAt,
  };
}

/** Merge API / modal state into MOIFormModal field shape. */
export function hydrateMoiFormFields(
  source: Record<string, unknown> | null | undefined,
): Record<string, unknown> {
  if (!source) return {};

  const nested = (source.data && typeof source.data === 'object'
    ? source.data
    : {}) as Record<string, unknown>;
  const d = { ...nested, ...source };

  return {
    company: String(d.company ?? ''),
    documentTitle: String(d.documentTitle ?? ''),
    backgroundInfo: String(d.backgroundInfo ?? ''),
    typeOfDocument: String(d.typeOfDocument ?? d.service ?? ''),
    supportingDocument: Boolean(d.supportingDocument),
    attachedFiles: (d.attachedFiles as File[]) || [],
    approvedTemplate: Boolean(d.approvedTemplate),
    documentsExecuted: Boolean(d.documentsExecuted),
    reasonForRatification: String(d.reasonForRatification ?? ''),
    withLOA: Boolean(d.withLOA),
    approvalPersons: (d.approvalPersons as { name: string; position: string }[]) || [{ name: '', position: '' }],
    requestedBy: String(d.requestedBy ?? d.signerName ?? ''),
    requestedDate: d.requestedDate,
    approvedBy: String(d.approvedBy ?? ''),
    approvedDate: d.approvedDate,
    approvalComments: String(d.approvalComments ?? ''),
    turnaroundWeeks: String(d.turnaroundPeriod ?? d.turnaroundWeeks ?? ''),
    draftCanBeAmended: Boolean(d.draftCanBeAmended),
    urgent: Boolean(d.urgent),
    urgentReason: String(d.urgentReason ?? ''),
    requiredExecutionDate: d.requiredDateOfExecution ?? d.requiredExecutionDate,
    financeRelated: Boolean(d.financeRelated ?? source.financeRelated),
    bankSignatoryMatter: Boolean(d.bankSignatoryMatter ?? source.bankSignatoryMatter),
    formTemplateCode: String(d.formTemplateCode ?? source.formTemplateCode ?? ''),
  };
}
