import { FileText, Edit, Trash2, Eye, X } from 'lucide-react';
import { useState } from 'react';

interface FormTemplate {
  id: number;
  name: string;
  description: string;
  createdDate: string;
  updatedDate: string;
  status: 'Active' | 'Inactive';
}

const defaultForms: FormTemplate[] = [
  {
    id: 1,
    name: 'MOI',
    description: 'Memorandum of Instruction',
    createdDate: '2026-05-14',
    updatedDate: '2026-05-14',
    status: 'Active',
  },
  {
    id: 2,
    name: 'MOA',
    description: 'Memorandum of Approval',
    createdDate: '2026-05-18',
    updatedDate: '2026-05-18',
    status: 'Active',
  },
];

interface FormsManagementProps {
  onViewMOI: () => void;
  onViewMOA: () => void;
}

const emptyEditForm = {
  description: '',
  status: 'Active' as FormTemplate['status'],
};

export function FormsManagement({ onViewMOI, onViewMOA }: FormsManagementProps) {
  const [forms, setForms] = useState<FormTemplate[]>(defaultForms);
  const [editingForm, setEditingForm] = useState<FormTemplate | null>(null);
  const [editForm, setEditForm] = useState(emptyEditForm);

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'Active':
        return 'bg-green-100 text-green-800';
      case 'Inactive':
        return 'bg-gray-100 text-gray-800';
      default:
        return 'bg-gray-100 text-gray-800';
    }
  };

  const handleView = (form: FormTemplate) => {
    if (form.name === 'MOI') onViewMOI();
    else if (form.name === 'MOA') onViewMOA();
  };

  const handleEdit = (form: FormTemplate) => {
    setEditingForm(form);
    setEditForm({ description: form.description, status: form.status });
  };

  const handleSaveEdit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!editingForm) return;
    const today = new Date().toISOString().split('T')[0];
    setForms((prev) =>
      prev.map((f) =>
        f.id === editingForm.id
          ? { ...f, description: editForm.description, status: editForm.status, updatedDate: today }
          : f,
      ),
    );
    setEditingForm(null);
  };

  const handleDelete = (form: FormTemplate) => {
    if (form.name === 'MOI' || form.name === 'MOA') {
      window.alert('MOI and MOA are system forms and cannot be deleted.');
      return;
    }
    if (!window.confirm(`Delete form "${form.name}"?`)) return;
    setForms((prev) => prev.filter((f) => f.id !== form.id));
  };

  return (
    <>
      <div className="bg-card rounded-lg border border-border overflow-hidden">
        <div className="p-4 border-b border-border">
          <div className="flex items-center gap-2">
            <FileText className="w-5 h-5 text-muted-foreground" />
            <h2>Forms Management</h2>
          </div>
          <p className="text-sm text-muted-foreground mt-1">
            Use the eye icon to open MOI or MOA. Edit updates description and status.
          </p>
        </div>

        <div className="overflow-auto">
          <table className="w-full">
            <thead className="bg-muted/50">
              <tr>
                <th className="px-4 py-3 text-left">Form Name</th>
                <th className="px-4 py-3 text-left">Description</th>
                <th className="px-4 py-3 text-left">Created Date</th>
                <th className="px-4 py-3 text-left">Last Updated</th>
                <th className="px-4 py-3 text-center">Status</th>
                <th className="px-4 py-3 text-center">Actions</th>
              </tr>
            </thead>
            <tbody>
              {forms.map((form) => (
                <tr
                  key={form.id}
                  className="border-t border-border hover:bg-muted/30 transition-colors"
                >
                  <td className="px-4 py-3 font-medium">{form.name}</td>
                  <td className="px-4 py-3">{form.description}</td>
                  <td className="px-4 py-3">{form.createdDate}</td>
                  <td className="px-4 py-3">{form.updatedDate}</td>
                  <td className="px-4 py-3 text-center">
                    <span className={`px-2 py-1 rounded-full text-xs ${getStatusColor(form.status)}`}>
                      {form.status}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex items-center justify-center gap-2">
                      <button
                        type="button"
                        onClick={() => handleView(form)}
                        className="p-1 hover:bg-muted rounded transition-colors"
                        title="View / fill form"
                      >
                        <Eye className="w-4 h-4" />
                      </button>
                      <button
                        type="button"
                        onClick={() => handleEdit(form)}
                        className="p-1 hover:bg-muted rounded transition-colors"
                        title="Edit form"
                      >
                        <Edit className="w-4 h-4" />
                      </button>
                      <button
                        type="button"
                        onClick={() => handleDelete(form)}
                        className="p-1 hover:bg-destructive/10 text-destructive rounded transition-colors"
                        title="Delete form"
                      >
                        <Trash2 className="w-4 h-4" />
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {editingForm && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
          <div className="bg-card rounded-lg border border-border w-full max-w-md">
            <div className="p-4 border-b border-border flex items-center justify-between">
              <h3>Edit {editingForm.name}</h3>
              <button
                type="button"
                onClick={() => setEditingForm(null)}
                className="p-1 hover:bg-muted rounded"
              >
                <X className="w-5 h-5" />
              </button>
            </div>
            <form onSubmit={handleSaveEdit} className="p-4 space-y-3">
              <div>
                <label className="block text-sm mb-1">Description</label>
                <input
                  type="text"
                  required
                  value={editForm.description}
                  onChange={(e) => setEditForm({ ...editForm, description: e.target.value })}
                  className="w-full px-3 py-2 border border-border rounded-lg"
                />
              </div>
              <select
                value={editForm.status}
                onChange={(e) =>
                  setEditForm({ ...editForm, status: e.target.value as FormTemplate['status'] })
                }
                className="w-full px-3 py-2 border border-border rounded-lg"
              >
                <option value="Active">Active</option>
                <option value="Inactive">Inactive</option>
              </select>
              <div className="flex justify-end gap-2 pt-2">
                <button
                  type="button"
                  onClick={() => setEditingForm(null)}
                  className="px-4 py-2 border rounded-lg"
                >
                  Cancel
                </button>
                <button type="submit" className="px-4 py-2 bg-primary text-primary-foreground rounded-lg">
                  Save
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </>
  );
}
