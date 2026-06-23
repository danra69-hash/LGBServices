import type { FormResponse } from '@/lib/api';

/** Set true to show DB-backed sequential MOA workflow UI; false uses legacy Approved By blocks only. */
export const SHOW_MOA_SEQUENTIAL_WORKFLOW = false;

export interface MoaPersonApproval {
  name: string;
  approved: boolean;
  date: string;
  comments: string;
}

export interface MoaApprovalBlock {
  approved: boolean;
  date: string;
  comments: string;
  resoForDLCM?: boolean;
}

export interface MoaFormFields {
  company: string;
  typeOfDocument: string;
  projectInitiator: string;
  preparedByInternal: string;
  vettedByInternal: string;
  preparedByExternal: string;
  vettedByExternal: string;
  seniorManagerApproval: MoaApprovalBlock;
  managerRegulatoryApproval: MoaApprovalBlock;
  moaPersonsApprovals: MoaPersonApproval[];
  financeRelated: boolean;
  bankSignatoryMatter: boolean;
  shareMovement: boolean;
  formTemplateCode: string;
  moiFormId?: number;
}

const emptyApproval = (): MoaApprovalBlock => ({
  approved: false,
  date: '',
  comments: '',
  resoForDLCM: false,
});

const readApproval = (raw: unknown, withReso = false): MoaApprovalBlock => {
  const block = (raw && typeof raw === 'object' ? raw : {}) as Record<string, unknown>;
  return {
    approved: Boolean(block.approved),
    date: String(block.date ?? ''),
    comments: String(block.comments ?? ''),
    ...(withReso ? { resoForDLCM: Boolean(block.resoForDLCM) } : {}),
  };
};

const readPersonApprovals = (raw: unknown): MoaPersonApproval[] => {
  if (!Array.isArray(raw)) return [];
  return raw.map((item) => {
    const row = (item && typeof item === 'object' ? item : {}) as Record<string, unknown>;
    return {
      name: String(row.name ?? ''),
      approved: Boolean(row.approved),
      date: String(row.date ?? ''),
      comments: String(row.comments ?? ''),
    };
  });
};

export const emptyMoaFormFields = (): MoaFormFields => ({
  company: '',
  typeOfDocument: '',
  projectInitiator: '',
  preparedByInternal: '',
  vettedByInternal: '',
  preparedByExternal: '',
  vettedByExternal: '',
  seniorManagerApproval: emptyApproval(),
  managerRegulatoryApproval: { approved: false, date: '', comments: '' },
  moaPersonsApprovals: [],
  financeRelated: false,
  bankSignatoryMatter: false,
  shareMovement: false,
  formTemplateCode: '',
});

type AccountHolder = { name: string; moa: boolean };

/** Build editable MOA fields from API payload, saved draft, or MOI hand-off data. */
export function hydrateMoaFormFields(
  source: Record<string, unknown> | null | undefined,
  options: {
    customers?: { company: string; accountHolders?: AccountHolder[] }[];
    serviceFallback?: string;
    isNewFromMoi?: boolean;
  } = {},
): MoaFormFields {
  const base = emptyMoaFormFields();
  if (!source) return base;

  const data = (source.data && typeof source.data === 'object'
    ? source.data
    : source) as Record<string, unknown>;

  const company = String(source.company ?? data.company ?? '');
  const typeOfDocument = String(
    data.typeOfDocument ?? data.service ?? options.serviceFallback ?? '',
  );
  const projectInitiator = String(
    data.projectInitiator ?? data.requestedBy ?? source.requestedBy ?? '',
  );

  const moaFormId = source.id ?? data.id;
  const moiFormId = (source.moiFormId ?? data.moiFormId) as number | undefined;
  const isPersistedMoa = Boolean(
    source.packChecklist
    || source.submittedForAdminReviewAt
    || source.sharonApprovedAt
    || data.preparedByInternal
    || data.preparedByExternal
    || (moiFormId != null && moaFormId != null && moiFormId !== moaFormId),
  );

  const selectedCompany = options.customers?.find((c) => c.company === company);
  const moaPersons = selectedCompany?.accountHolders?.filter((h) => h.moa) ?? [];
  const savedPersonApprovals = readPersonApprovals(data.moaPersonsApprovals);

  let moaPersonsApprovals = savedPersonApprovals;
  if (moaPersonsApprovals.length === 0 && (options.isNewFromMoi || !isPersistedMoa)) {
    moaPersonsApprovals = moaPersons.map((person) => ({
      name: person.name,
      approved: false,
      date: '',
      comments: '',
    }));
  }

  return {
    company,
    typeOfDocument,
    projectInitiator,
    preparedByInternal: String(data.preparedByInternal ?? ''),
    vettedByInternal: String(data.vettedByInternal ?? ''),
    preparedByExternal: String(data.preparedByExternal ?? ''),
    vettedByExternal: String(data.vettedByExternal ?? ''),
    seniorManagerApproval: readApproval(data.seniorManagerApproval, true),
    managerRegulatoryApproval: readApproval(data.managerRegulatoryApproval),
    moaPersonsApprovals,
    financeRelated: Boolean(source.financeRelated ?? data.financeRelated),
    bankSignatoryMatter: Boolean(source.bankSignatoryMatter ?? data.bankSignatoryMatter),
    shareMovement: Boolean(source.shareMovement ?? data.shareMovement),
    formTemplateCode: String(source.formTemplateCode ?? data.formTemplateCode ?? ''),
    moiFormId: moiFormId ?? (options.isNewFromMoi && moaFormId ? Number(moaFormId) : undefined),
  };
}

export function mapMoaFormResponseToModalState(
  form: FormResponse,
  job?: { id: number; service?: string; activeUnitNumber?: number },
): Record<string, unknown> {
  const unitNumber = form.unitNumber ?? job?.activeUnitNumber ?? undefined;
  return {
    ...form.data,
    id: form.id,
    jobId: form.jobId ?? job?.id,
    moiFormId: form.moiFormId,
    company: form.company,
    typeOfDocument: String(form.data?.typeOfDocument ?? form.data?.service ?? job?.service ?? ''),
    projectInitiator: String(form.data?.projectInitiator ?? form.data?.requestedBy ?? ''),
    formTemplateCode: form.formTemplateCode,
    financeRelated: form.financeRelated,
    bankSignatoryMatter: form.bankSignatoryMatter,
    shareMovement: form.shareMovement,
    packChecklist: form.packChecklist,
    packValidationErrors: form.packValidationErrors,
    workflow: form.workflow,
    clientApprovals: form.clientApprovals,
    rejections: form.rejections,
    requiredApprovers: form.requiredApprovers,
    pendingApprovers: form.pendingApprovers,
    sharonApprovedAt: form.sharonApprovedAt,
    submittedForAdminReviewAt: form.submittedForAdminReviewAt,
    unitNumber,
    activeUnitNumber: unitNumber,
    updatedAt: form.updatedAt,
  };
}

/** Resolve session # for MOA handoff/save on single- or multi-qty jobs. */
export function resolveMoaUnitNumber(
  job?: { totalQty?: number; activeUnitNumber?: number },
  data?: { unitNumber?: unknown; activeUnitNumber?: unknown },
  formUnitNumber?: number | null,
): number | undefined {
  const fromJob = job?.activeUnitNumber;
  if (fromJob != null) return fromJob;
  const fromData = data?.unitNumber ?? data?.activeUnitNumber;
  if (typeof fromData === 'number') return fromData;
  if (formUnitNumber != null) return formUnitNumber;
  return (job?.totalQty ?? 1) <= 1 ? 1 : undefined;
}
