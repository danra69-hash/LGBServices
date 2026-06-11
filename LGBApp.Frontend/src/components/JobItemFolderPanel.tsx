import { useCallback, useEffect, useState } from 'react';
import { Download, Eye, FileText, FolderOpen, Trash2, Upload } from 'lucide-react';
import {
  ApiError,
  deleteJobItemDocument,
  downloadJobItemDocument,
  getJobItemFolders,
  uploadJobItemDocument,
  type JobItemFolderDto,
  type JobItemFoldersResponse,
  type JobRequestResponse,
} from '@/lib/api';
import { jobHasMoaForm, jobHasMoiForm } from '@/lib/packageItemStatus';

const FOLDER_LABELS: Record<string, string> = {
  moi: 'MOI',
  moa: 'MOA',
  supporting: 'Supporting documents',
};

interface JobItemFolderPanelProps {
  job: JobRequestResponse;
  unitNumber?: number;
  onOpenMoi?: () => void;
  onOpenMoa?: () => void;
}

export function JobItemFolderPanel({ job, unitNumber, onOpenMoi, onOpenMoa }: JobItemFolderPanelProps) {
  const [data, setData] = useState<JobItemFoldersResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [uploading, setUploading] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const folders = await getJobItemFolders(job.id, unitNumber);
      setData(folders);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to load folders.');
    } finally {
      setLoading(false);
    }
  }, [job.id, unitNumber]);

  useEffect(() => {
    void load();
  }, [load]);

  const handleUpload = async (folder: 'moi' | 'moa' | 'supporting', file: File) => {
    setUploading(folder);
    setError('');
    try {
      await uploadJobItemDocument(job.id, folder, file, unitNumber);
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Upload failed.');
    } finally {
      setUploading(null);
    }
  };

  const handleOpen = async (documentId: number, fileName: string, contentType: string) => {
    try {
      const blob = await downloadJobItemDocument(job.id, documentId);
      const url = URL.createObjectURL(blob);
      const canPreview = contentType.startsWith('image/')
        || contentType === 'application/pdf'
        || fileName.toLowerCase().endsWith('.pdf');
      if (canPreview) {
        window.open(url, '_blank', 'noopener,noreferrer');
        window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
        return;
      }
      const a = document.createElement('a');
      a.href = url;
      a.download = fileName;
      a.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to open file.');
    }
  };

  const handleDelete = async (documentId: number) => {
    setError('');
    try {
      await deleteJobItemDocument(job.id, documentId);
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Delete failed.');
    }
  };

  const showFolder = (folder: JobItemFolderDto) => {
    if (folder.folder === 'moi') return jobHasMoiForm(job) || Boolean(data?.moiFormId);
    if (folder.folder === 'moa') return jobHasMoaForm(job) || Boolean(data?.moaFormId);
    return true;
  };

  if (loading) {
    return <p className="text-xs text-muted-foreground py-2">Loading item folders…</p>;
  }

  return (
    <div className="space-y-3 py-1">
      <div className="flex items-center gap-2 text-sm font-medium text-muted-foreground">
        <FolderOpen className="w-4 h-4" />
        Item folder — {job.service}
      </div>
      {error && <p className="text-xs text-destructive">{error}</p>}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
        {(data?.folders ?? []).filter(showFolder).map((folder) => (
          <div key={folder.folder} className="border border-border rounded-lg p-3 bg-background">
            <div className="flex items-center justify-between gap-2 mb-2">
              <p className="text-sm font-medium">{FOLDER_LABELS[folder.folder] ?? folder.folder}</p>
              {folder.folder === 'moi' && (jobHasMoiForm(job) || data?.moiFormId) && onOpenMoi && (
                <button
                  type="button"
                  onClick={onOpenMoi}
                  className="text-xs text-primary hover:underline inline-flex items-center gap-1"
                >
                  <FileText className="w-3 h-3" />
                  View form
                </button>
              )}
              {folder.folder === 'moa' && (jobHasMoaForm(job) || data?.moaFormId) && onOpenMoa && (
                <button
                  type="button"
                  onClick={onOpenMoa}
                  className="text-xs text-primary hover:underline inline-flex items-center gap-1"
                >
                  <FileText className="w-3 h-3" />
                  View form
                </button>
              )}
            </div>
            {folder.documents.length === 0 ? (
              <p className="text-xs text-muted-foreground mb-2">No files yet</p>
            ) : (
              <ul className="space-y-1 mb-2">
                {folder.documents.map((doc) => (
                  <li key={doc.id} className="flex items-center justify-between gap-2 text-xs">
                    <span className="truncate" title={doc.fileName}>{doc.fileName}</span>
                    <span className="flex items-center gap-1 shrink-0">
                      <button
                        type="button"
                        title="View"
                        onClick={() => void handleOpen(doc.id, doc.fileName, doc.contentType)}
                        className="p-1 hover:bg-muted rounded"
                      >
                        <Eye className="w-3 h-3" />
                      </button>
                      <button
                        type="button"
                        title="Download"
                        onClick={() => void handleOpen(doc.id, doc.fileName, doc.contentType)}
                        className="p-1 hover:bg-muted rounded"
                      >
                        <Download className="w-3 h-3" />
                      </button>
                      <button
                        type="button"
                        title="Remove"
                        onClick={() => void handleDelete(doc.id)}
                        className="p-1 hover:bg-muted rounded text-destructive"
                      >
                        <Trash2 className="w-3 h-3" />
                      </button>
                    </span>
                  </li>
                ))}
              </ul>
            )}
            <label className="inline-flex items-center gap-1 text-xs text-primary cursor-pointer hover:underline">
              <Upload className="w-3 h-3" />
              {uploading === folder.folder ? 'Uploading…' : 'Add file'}
              <input
                type="file"
                className="hidden"
                disabled={uploading !== null}
                onChange={(e) => {
                  const file = e.target.files?.[0];
                  if (file) void handleUpload(folder.folder as 'moi' | 'moa' | 'supporting', file);
                  e.target.value = '';
                }}
              />
            </label>
          </div>
        ))}
      </div>
    </div>
  );
}
