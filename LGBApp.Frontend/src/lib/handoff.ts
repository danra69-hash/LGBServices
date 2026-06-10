export const HANDOFF_LABELS: Record<string, string> = {
  ClientSubmitted: 'Client submitted',
  PendingPrep: 'Pending prep',
  ResoInProgress: 'Reso in progress',
  AdminReview: 'Admin review',
  ReadyForMoa: 'Ready for MOA',
  MoaCirculation: 'MOA circulation',
  Completed: 'Completed',
};

export function handoffLabel(status?: string): string {
  if (!status) return '—';
  return HANDOFF_LABELS[status] ?? status;
}
