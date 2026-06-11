import { useCallback, useEffect, useState } from 'react';
import { Download, Eye, FileText } from 'lucide-react';
import {
  ApiError,
  downloadJobItemDocument,
  getJobItemFolders,
  type JobItemDocumentDto,
} from '@/lib/api';

interface JobItemDocumentsSectionProps {
  jobId: number;
  unitNumber?: number;
  title?: string;
  folders?: Array<'moi' | 'supporting' | 'moa'>;
}

async function openDocument(jobId: number, doc: JobItemDocumentDto) {
  const blob = await downloadJobItemDocument(jobId, doc.id);
  const url = URL.createObjectURL(blob);
  const canPreview = doc.contentType.startsWith('image/')
    || doc.contentType === 'application/pdf'
    || doc.fileName.toLowerCase().endsWith('.pdf');
  if (canPreview) {
    window.open(url, '_blank', 'noopener,noreferrer');
    window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
    return;
  }
  const a = document.createElement('a');
  a.href = url;
  a.download = doc.fileName;
  a.click();
  URL.revokeObjectURL(url);
}

export function JobItemDocumentsSection({
  jobId,
  unitNumber,
  title = 'Attached documents',
  folders = ['moi', 'supporting'],
}: JobItemDocumentsSectionProps) {
  const [documents, setDocuments] = useState<JobItemDocumentDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const data = await getJobItemFolders(jobId, unitNumber);
      const docs = data.folders
        .filter((f) => folders.includes(f.folder as 'moi' | 'supporting' | 'moa'))
        .flatMap((f) => f.documents);
      setDocuments(docs);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to load documents.');
      setDocuments([]);
    } finally {
      setLoading(false);
    }
  }, [folders, jobId, unitNumber]);

  useEffect(() => {
    void load();
  }, [load]);

  if (loading) {
    return <p className="text-xs text-muted-foreground py-2">Loading documents…</p>;
  }

  if (error) {
    return <p className="text-xs text-destructive py-2">{error}</p>;
  }

  if (documents.length === 0) {
    return null;
  }

  return (
    <div className="border border-border rounded-lg p-4 bg-muted/20 space-y-2">
      <div className="flex items-center gap-2 text-sm font-medium">
        <FileText className="w-4 h-4 text-muted-foreground" />
        {title}
      </div>
      <ul className="space-y-2">
        {documents.map((doc) => (
          <li key={doc.id} className="flex items-center justify-between gap-2 text-sm">
            <span className="truncate" title={doc.fileName}>{doc.fileName}</span>
            <span className="flex items-center gap-1 shrink-0">
              <button
                type="button"
                title="View"
                onClick={() => void openDocument(jobId, doc).catch((err) => {
                  setError(err instanceof ApiError ? err.message : 'Failed to open document.');
                })}
                className="inline-flex items-center gap-1 px-2 py-1 text-xs border border-border rounded hover:bg-muted"
              >
                <Eye className="w-3 h-3" />
                View
              </button>
              <button
                type="button"
                title="Download"
                onClick={() => void openDocument(jobId, doc).catch((err) => {
                  setError(err instanceof ApiError ? err.message : 'Failed to download document.');
                })}
                className="p-1 hover:bg-muted rounded"
              >
                <Download className="w-3 h-3" />
              </button>
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}
