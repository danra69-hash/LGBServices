/** MOA workflow template codes (match backend WorkflowTemplate.Code). */
export const MOA_WORKFLOW_TEMPLATES = [
  { code: '', label: 'Division default' },
  { code: 'MOA_NO_LOA', label: 'MOA — No LOA' },
  { code: 'MOA_WITH_LOA', label: 'MOA — With LOA' },
  { code: 'MOA_SWM', label: 'MOA — SWM Group' },
] as const;

export interface MoaTemplateSectionFlags {
  seniorManagerCoSec: boolean;
  managerRegulatory: boolean;
  headOfFinanceCfo: boolean;
  ceoCooGm: boolean;
  msTeh: boolean;
  boardMembers: boolean;
  loaHolders: boolean;
  dlcm: boolean;
}

const NO_LOA: MoaTemplateSectionFlags = {
  seniorManagerCoSec: true,
  managerRegulatory: false,
  headOfFinanceCfo: true,
  ceoCooGm: true,
  msTeh: true,
  boardMembers: true,
  dlcm: true,
};

const WITH_LOA: MoaTemplateSectionFlags = {
  seniorManagerCoSec: true,
  managerRegulatory: false,
  headOfFinanceCfo: true,
  ceoCooGm: false,
  msTeh: true,
  boardMembers: false,
  loaHolders: false,
  dlcm: true,
};

const SWM: MoaTemplateSectionFlags = {
  seniorManagerCoSec: true,
  managerRegulatory: true,
  headOfFinanceCfo: true,
  ceoCooGm: false,
  msTeh: true,
  boardMembers: false,
  loaHolders: true,
  dlcm: false,
};

function baseForTemplate(templateCode: string | undefined | null): MoaTemplateSectionFlags {
  const code = (templateCode ?? '').trim().toUpperCase();
  if (code === 'MOA_WITH_LOA') return { ...WITH_LOA };
  if (code === 'MOA_SWM') return { ...SWM };
  if (code === 'MOA_NO_LOA' || code === '') return { ...NO_LOA };
  return { ...NO_LOA };
}

/** Option C: template + MOA form flags determine which approval blocks appear. */
export function resolveMoaTemplateSections(
  templateCode: string | undefined | null,
  options: {
    financeRelated?: boolean;
    bankSignatoryMatter?: boolean;
    shareMovement?: boolean;
    hasLoa?: boolean;
  },
): MoaTemplateSectionFlags {
  const base = baseForTemplate(templateCode);
  return {
    seniorManagerCoSec: base.seniorManagerCoSec,
    managerRegulatory: base.managerRegulatory && (templateCode === 'MOA_SWM'),
    headOfFinanceCfo: base.headOfFinanceCfo && Boolean(options.financeRelated),
    ceoCooGm: base.ceoCooGm && Boolean(options.financeRelated || options.shareMovement),
    msTeh: base.msTeh && Boolean(options.bankSignatoryMatter),
    boardMembers: base.boardMembers && !options.hasLoa,
    loaHolders: base.loaHolders && Boolean(options.hasLoa),
    dlcm: base.dlcm,
  };
}
