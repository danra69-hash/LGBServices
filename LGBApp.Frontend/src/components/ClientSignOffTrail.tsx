import type { ClientApprovalDto } from '@/lib/api';

interface ClientSignOffTrailProps {
  title?: string;
  approvals?: ClientApprovalDto[];
}

export function ClientSignOffTrail({ title = 'Client sign-off', approvals = [] }: ClientSignOffTrailProps) {
  if (approvals.length === 0) return null;

  return (
    <div className="border border-border rounded-lg p-4 bg-muted/20 space-y-3">
      <p className="text-sm font-medium">{title}</p>
      <ul className="space-y-3">
        {approvals.map((approval, index) => {
          const isImage = approval.signatureDataUrl?.startsWith('data:image/');
          return (
            <li key={`${approval.userId}-${approval.signedAt}-${index}`} className="text-sm border border-border rounded-lg p-3 bg-background">
              <div className="flex flex-wrap items-start justify-between gap-2">
                <div>
                  <p className="font-medium">{approval.accountHolderName}</p>
                  {approval.signedAt && (
                    <p className="text-xs text-muted-foreground">Signed {approval.signedAt}</p>
                  )}
                </div>
                {approval.signatureFileName && (
                  <p className="text-xs text-muted-foreground">{approval.signatureFileName}</p>
                )}
              </div>
              {approval.comments && (
                <p className="text-sm text-muted-foreground mt-2 whitespace-pre-wrap">{approval.comments}</p>
              )}
              {isImage && (
                <img
                  src={approval.signatureDataUrl}
                  alt={`Signature — ${approval.accountHolderName}`}
                  className="mt-3 max-h-20 rounded border border-border bg-white"
                />
              )}
            </li>
          );
        })}
      </ul>
    </div>
  );
}
