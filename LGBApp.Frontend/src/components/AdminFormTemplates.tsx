import { useCallback, useEffect, useState } from 'react';
import {
  ApiError,
  createFormTemplate,
  getFormTemplates,
  updateFormTemplate,
  type FormTemplateDto,
} from '@/lib/api';

interface AdminFormTemplatesProps {
  refreshKey?: number;
}

export function AdminFormTemplates({ refreshKey = 0 }: AdminFormTemplatesProps) {
  const [templates, setTemplates] = useState<FormTemplateDto[]>([]);
  const [selected, setSelected] = useState<FormTemplateDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const data = await getFormTemplates();
      setTemplates(data);
      setSelected((prev) => prev ?? data[0] ?? null);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to load form templates.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load, refreshKey]);

  const save = async () => {
    if (!selected) return;
    setSaving(true);
    setMessage('');
    try {
      if (selected.id) {
        await updateFormTemplate(selected.id, selected);
      } else {
        const created = await createFormTemplate(selected);
        setSelected(created);
      }
      setMessage('Form template saved.');
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to save form template.');
    } finally {
      setSaving(false);
    }
  };

  const addTemplate = () => {
    setSelected({
      id: 0,
      formType: 'MOI',
      code: `CUSTOM_${Date.now()}`,
      name: 'New template',
      description: 'Memorandum of Instruction',
      addressedTo: 'Head of Legal & Secretarial Department',
      divisionLabel: 'Secretarial Division',
      issuerEntity: '',
      fields: [],
      isDefault: false,
      isActive: true,
    });
  };

  if (loading) return <p className="text-sm text-muted-foreground p-4">Loading form templates…</p>;

  return (
    <div className="bg-card border border-border rounded-lg p-6 space-y-4">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h3 className="text-lg font-medium">MOI / MOA Form Models</h3>
          <p className="text-sm text-muted-foreground mt-1">
            Override issuer, headers, and fields per entity. Customers can override via template code.
          </p>
        </div>
        <button type="button" onClick={addTemplate} className="text-sm px-3 py-1.5 border border-border rounded-lg">
          New template
        </button>
      </div>

      {error && <p className="text-sm text-destructive">{error}</p>}
      {message && <p className="text-sm text-green-600">{message}</p>}

      <div className="flex flex-wrap gap-2">
        {templates.map((t) => (
          <button
            key={t.id}
            type="button"
            onClick={() => setSelected(t)}
            className={`px-3 py-1.5 rounded-lg text-sm border ${
              selected?.id === t.id ? 'bg-primary text-primary-foreground border-primary' : 'border-border'
            }`}
          >
            {t.formType}: {t.name}
          </button>
        ))}
      </div>

      {selected && (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm mb-1">Form type</label>
            <select
              className="w-full px-3 py-2 border border-border rounded-lg"
              value={selected.formType}
              onChange={(e) => setSelected({ ...selected, formType: e.target.value })}
            >
              <option value="MOI">MOI</option>
              <option value="MOA">MOA</option>
            </select>
          </div>
          <div>
            <label className="block text-sm mb-1">Code</label>
            <input
              className="w-full px-3 py-2 border border-border rounded-lg"
              value={selected.code}
              onChange={(e) => setSelected({ ...selected, code: e.target.value })}
            />
          </div>
          <div>
            <label className="block text-sm mb-1">Name</label>
            <input
              className="w-full px-3 py-2 border border-border rounded-lg"
              value={selected.name}
              onChange={(e) => setSelected({ ...selected, name: e.target.value })}
            />
          </div>
          <div>
            <label className="block text-sm mb-1">Issuer entity</label>
            <input
              className="w-full px-3 py-2 border border-border rounded-lg"
              value={selected.issuerEntity}
              onChange={(e) => setSelected({ ...selected, issuerEntity: e.target.value })}
            />
          </div>
          <div>
            <label className="block text-sm mb-1">Description (header)</label>
            <input
              className="w-full px-3 py-2 border border-border rounded-lg"
              value={selected.description}
              onChange={(e) => setSelected({ ...selected, description: e.target.value })}
            />
          </div>
          <div>
            <label className="block text-sm mb-1">Addressed to</label>
            <input
              className="w-full px-3 py-2 border border-border rounded-lg"
              value={selected.addressedTo}
              onChange={(e) => setSelected({ ...selected, addressedTo: e.target.value })}
            />
          </div>
          <div>
            <label className="block text-sm mb-1">Division label</label>
            <input
              className="w-full px-3 py-2 border border-border rounded-lg"
              value={selected.divisionLabel}
              onChange={(e) => setSelected({ ...selected, divisionLabel: e.target.value })}
            />
          </div>
          <div className="flex items-center gap-2 pt-6">
            <input
              type="checkbox"
              checked={selected.isDefault}
              onChange={(e) => setSelected({ ...selected, isDefault: e.target.checked })}
            />
            <span className="text-sm">Default for form type</span>
          </div>
        </div>
      )}

      {selected && (
        <button
          type="button"
          disabled={saving}
          onClick={() => void save()}
          className="px-4 py-2 bg-primary text-primary-foreground rounded-lg text-sm disabled:opacity-50"
        >
          Save form template
        </button>
      )}
    </div>
  );
}
