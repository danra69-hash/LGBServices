import { useCallback, useEffect, useRef, useState } from 'react';
import { Download, Eye, FileText, Paperclip, Trash2 } from 'lucide-react';
import {
  ApiError,
  deleteJobItemDocument,
  downloadJobItemDocument,
  getJobItemFolders,
  uploadJobItemDocument,
  type JobItemDocumentDto,
} from '@/lib/api';

const FOLDER_LABELS: Record<string, string> = {
  moi: 'MOI documents',
  moa: 'MOA pack files',
  supporting: 'Supporting documents',
};

interface JobItemDocumentsSectionProps {
  jobId: number;
  unitNumber?: number;
  title?: string;
  folders?: Array<'moi' | 'supporting' | 'moa'>;
  /** Folders included in onCountChange; defaults to folders. */
  countFolders?: Array<'moi' | 'supporting' | 'moa'>;
  refreshKey?: number;
  showWhenEmpty?: boolean;
  onCountChange?: (count: number) => void;
  allowUpload?: boolean;
  allowDelete?: boolean;
  uploadFolder?: 'moi' | 'supporting' | 'moa';
  /** When true, list each folder separately with its own upload control. */
  groupByFolder?: boolean;
  /** Called before upload when job may need to be saved first (e.g. new MOI draft). */
  onBeforeUpload?: () => Promise<{ jobId: number; unitNumber?: number } | void>;
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
  countFolders,
  refreshKey = 0,
  showWhenEmpty = false,
  onCountChange,
  allowUpload = false,
  allowDelete = false,
  uploadFolder = 'supporting',
  groupByFolder = false,
  onBeforeUpload,
}: JobItemDocumentsSectionProps) {
  const [documents, setDocuments] = useState<JobItemDocumentDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [uploading, setUploading] = useState(false);
  const [pendingUploadNames, setPendingUploadNames] = useState<string[]>([]);
  const [deletingId, setDeletingId] = useState<number | null>(null);
  const [activeJobId, setActiveJobId] = useState(jobId);

  const foldersKey = folders.join('\0');
  const countFoldersKey = (countFolders ?? folders).join('\0');
  const onCountChangeRef = useRef(onCountChange);
  onCountChangeRef.current = onCountChange;
  const hasLoadedRef = useRef(false);

  useEffect(() => {
    setActiveJobId(jobId);
  }, [jobId]);

  useEffect(() => {
    hasLoadedRef.current = false;
  }, [activeJobId, unitNumber, foldersKey]);

  const load = useCallback(async () => {
    if (!activeJobId) {
      setDocuments([]);
      onCountChangeRef.current?.(0);
      setLoading(false);
      hasLoadedRef.current = false;
      return;
    }
    setLoading(!hasLoadedRef.current);
    setError('');
    try {
      const data = await getJobItemFolders(activeJobId, unitNumber);
      const visibleFolders = data.folders.filter((f) =>
        folders.includes(f.folder as 'moi' | 'supporting' | 'moa'));
      const docs = visibleFolders.flatMap((f) => f.documents);
      setDocuments(docs);
      const countSource = countFolders ?? folders;
      const counted = visibleFolders
        .filter((f) => countSource.includes(f.folder as 'moi' | 'supporting' | 'moa'))
        .flatMap((f) => f.documents);
      onCountChangeRef.current?.(counted.length);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to load documents.');
      setDocuments([]);
      onCountChangeRef.current?.(0);
    } finally {
      setLoading(false);
      hasLoadedRef.current = true;
    }
  }, [activeJobId, folders, countFolders, foldersKey, countFoldersKey, unitNumber]);

  useEffect(() => {
    void load();
  }, [load, refreshKey]);

  const handleUpload = async (
    files: FileList | File[],
    targetFolder: 'moi' | 'supporting' | 'moa' = uploadFolder,
  ) => {
    const fileList = Array.from(files);
    if (fileList.length === 0) return;

    setUploading(true);
    setError('');
    setPendingUploadNames(fileList.map((f) => f.name));
    try {
      let targetJobId = activeJobId;
      let targetUnit = unitNumber;
      if (onBeforeUpload) {
        const resolved = await onBeforeUpload();
        if (resolved) {
          targetJobId = resolved.jobId;
          targetUnit = resolved.unitNumber ?? targetUnit;
          setActiveJobId(resolved.jobId);
        }
      }
      if (!targetJobId) {
        throw new Error('Save the form first so attachments can be stored.');
      }
      for (const file of fileList) {
        await uploadJobItemDocument(targetJobId, targetFolder, file, targetUnit);
      }
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to upload file.');
    } finally {
      setUploading(false);
      setPendingUploadNames([]);
    }
  };

  const handleDelete = async (doc: JobItemDocumentDto) => {
    if (!window.confirm(`Remove "${doc.fileName}"?`)) return;
    setDeletingId(doc.id);
    setError('');
    try {
      await deleteJobItemDocument(activeJobId, doc.id);
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to remove document.');
    } finally {
      setDeletingId(null);
    }
  };

  const showUpload = allowUpload;
  const showSection = documents.length > 0 || showWhenEmpty || showUpload;

  const renderDocumentRow = (doc: JobItemDocumentDto) => (
    <li key={doc.id} className="flex items-center justify-between gap-2 text-sm">
      <span className="truncate" title={doc.fileName}>{doc.fileName}</span>
      <span className="flex items-center gap-1 shrink-0">
        <button
          type="button"
          title="View"
          onClick={() => void openDocument(activeJobId, doc).catch((err) => {
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
          onClick={() => void openDocument(activeJobId, doc).catch((err) => {
            setError(err instanceof ApiError ? err.message : 'Failed to download document.');
          })}
          className="p-1 hover:bg-muted rounded"
        >
          <Download className="w-3 h-3" />
        </button>
        {allowDelete && (
          <button
            type="button"
            title="Remove"
            disabled={deletingId === doc.id}
            onClick={() => void handleDelete(doc)}
            className="p-1 hover:bg-muted rounded text-destructive disabled:opacity-50"
          >
            <Trash2 className="w-3 h-3" />
          </button>
        )}
      </span>
    </li>
  );

  const renderUploadButton = (folder: 'moi' | 'supporting' | 'moa', label = 'Add files') => (
    <label className="flex items-center gap-1.5 px-3 py-1.5 text-xs border border-border rounded-lg bg-secondary text-secondary-foreground hover:bg-secondary/90 transition-colors cursor-pointer shrink-0">
      <Paperclip className="w-3.5 h-3.5" />
      <span>{uploading ? 'Uploading…' : label}</span>
      <input
        type="file"
        multiple
        className="hidden"
        disabled={uploading}
        onChange={(e) => {
          const files = e.target.files;
          if (files?.length) void handleUpload(files, folder);
          e.target.value = '';
        }}
      />
    </label>
  );

  if (loading) {
    return <p className="text-xs text-muted-foreground py-2">Loading documents…</p>;
  }

  if (!showSection) return null;

  return (
    <div className="border border-border rounded-lg p-4 bg-muted/20 space-y-3">
      <div className="flex items-center justify-between gap-2">
        <div className="flex items-center gap-2 text-sm font-medium">
          <FileText className="w-4 h-4 text-muted-foreground" />
          {title}
        </div>
        {showUpload && !groupByFolder && renderUploadButton(uploadFolder)}
      </div>

      {error && <p className="text-xs text-destructive">{error}</p>}
      {uploading && pendingUploadNames.length > 0 && (
        <p className="text-xs text-muted-foreground">
          Uploading {pendingUploadNames.join(', ')}…
        </p>
      )}

      {groupByFolder ? (
        <div className="space-y-4">
          {folders.map((folder) => {
            const folderDocs = documents.filter((doc) => doc.folder === folder);
            return (
              <div key={folder} className="space-y-2">
                <div className="flex items-center justify-between gap-2">
                  <p className="text-xs font-medium text-muted-foreground">
                    {FOLDER_LABELS[folder] ?? folder}
                  </p>
                  {showUpload && renderUploadButton(folder, 'Add files')}
                </div>
                {folderDocs.length === 0 ? (
                  <p className="text-xs text-muted-foreground">No files attached yet.</p>
                ) : (
                  <ul className="space-y-2">
                    {folderDocs.map((doc) => renderDocumentRow(doc))}
                  </ul>
                )}
              </div>
            );
          })}
        </div>
      ) : documents.length === 0 ? (
        <p className="text-xs text-muted-foreground">No files attached yet.</p>
      ) : (
        <ul className="space-y-2">
          {documents.map((doc) => renderDocumentRow(doc))}
        </ul>
      )}
    </div>
  );
}
