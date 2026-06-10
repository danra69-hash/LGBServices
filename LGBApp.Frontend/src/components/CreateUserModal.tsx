import { X } from 'lucide-react';
import { useState } from 'react';
import { ROLES, roleLabel } from '@/lib/roles';
import type { CustomerResponse } from '@/lib/api';

export interface CreateUserFormData {
  name: string;
  email: string;
  mobile: string;
  password: string;
  confirmPassword: string;
  role: string;
  jobTitle?: string;
  canRecommendMoi?: boolean;
  customerId?: number;
}

interface CreateUserModalProps {
  isOpen: boolean;
  customers: CustomerResponse[];
  mode?: 'internal' | 'clientTeam';
  fixedCustomerId?: number;
  onClose: () => void;
  onSubmit: (data: Omit<CreateUserFormData, 'confirmPassword'>) => void;
}

const EXTERNAL_ROLES = [ROLES.ClientAdmin, ROLES.Client];

export function CreateUserModal({
  isOpen,
  customers,
  mode = 'internal',
  fixedCustomerId,
  onClose,
  onSubmit,
}: CreateUserModalProps) {
  const isClientTeamMode = mode === 'clientTeam';
  const [formData, setFormData] = useState<CreateUserFormData>({
    name: '',
    email: '',
    mobile: '',
    password: '',
    confirmPassword: '',
    role: isClientTeamMode ? ROLES.Client : ROLES.User,
    jobTitle: '',
    canRecommendMoi: false,
    customerId: undefined,
  });

  const isExternalRole = EXTERNAL_ROLES.includes(formData.role as (typeof EXTERNAL_ROLES)[number]);

  const resetForm = () => {
    setFormData({
      name: '',
      email: '',
      mobile: '',
      password: '',
      confirmPassword: '',
      role: isClientTeamMode ? ROLES.Client : ROLES.User,
      jobTitle: '',
      canRecommendMoi: false,
      customerId: fixedCustomerId,
    });
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (formData.password !== formData.confirmPassword) {
      alert('Passwords do not match');
      return;
    }
    const customerId = isClientTeamMode ? fixedCustomerId : formData.customerId;
    if (isExternalRole && !customerId) {
      alert('Select a customer for external users.');
      return;
    }
    const { confirmPassword: _, ...payload } = formData;
    onSubmit({
      ...payload,
      role: isClientTeamMode ? ROLES.Client : payload.role,
      customerId: isExternalRole || isClientTeamMode ? customerId : undefined,
      jobTitle: formData.jobTitle || undefined,
      canRecommendMoi: formData.canRecommendMoi || undefined,
    });
    onClose();
    resetForm();
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
      <div className="bg-card rounded-lg border border-border w-full max-w-2xl max-h-[90vh] overflow-y-auto">
        <div className="p-6 border-b border-border flex items-center justify-between">
          <h2>{isClientTeamMode ? 'Invite team member' : 'Add New User'}</h2>
          <button onClick={onClose} className="p-1 hover:bg-muted rounded transition-colors">
            <X className="w-5 h-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit}>
          <div className="p-6 space-y-4">
            <div>
              <label className="block mb-2">Name *</label>
              <input
                type="text"
                required
                value={formData.name}
                onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
              />
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div>
                <label className="block mb-2">Email *</label>
                <input
                  type="email"
                  required
                  value={formData.email}
                  onChange={(e) => setFormData({ ...formData, email: e.target.value })}
                  className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                />
              </div>

              <div>
                <label className="block mb-2">Mobile Number (Malaysia) *</label>
                <input
                  type="tel"
                  required
                  pattern="^(\+?6?01)[0-46-9]-*[0-9]{7,8}$"
                  placeholder="+60123456789"
                  value={formData.mobile}
                  onChange={(e) => setFormData({ ...formData, mobile: e.target.value })}
                  className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                />
              </div>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div>
                <label className="block mb-2">Password *</label>
                <input
                  type="password"
                  required
                  minLength={6}
                  value={formData.password}
                  onChange={(e) => setFormData({ ...formData, password: e.target.value })}
                  className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                />
              </div>

              <div>
                <label className="block mb-2">Confirm Password *</label>
                <input
                  type="password"
                  required
                  minLength={6}
                  value={formData.confirmPassword}
                  onChange={(e) => setFormData({ ...formData, confirmPassword: e.target.value })}
                  className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                />
              </div>
            </div>

            {!isClientTeamMode && (
              <div>
                <label className="block mb-2">Role *</label>
                <select
                  required
                  value={formData.role}
                  onChange={(e) =>
                    setFormData({
                      ...formData,
                      role: e.target.value,
                      customerId: EXTERNAL_ROLES.includes(e.target.value as (typeof EXTERNAL_ROLES)[number])
                        ? formData.customerId
                        : undefined,
                    })
                  }
                  className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                >
                  <option value={ROLES.Admin}>{roleLabel(ROLES.Admin)}</option>
                  <option value={ROLES.User}>{roleLabel(ROLES.User)}</option>
                  <option value={ROLES.ClientAdmin}>{roleLabel(ROLES.ClientAdmin)}</option>
                  <option value={ROLES.Client}>{roleLabel(ROLES.Client)}</option>
                </select>
              </div>
            )}

            {isClientTeamMode && (
              <p className="text-sm text-muted-foreground border border-border rounded-lg p-3">
                New users are invited as <strong>{roleLabel(ROLES.Client)}</strong> for your company. They can be assigned MOI and form tasks after they sign in.
              </p>
            )}

            {isExternalRole && !isClientTeamMode && (
              <div>
                <label className="block mb-2">Customer company *</label>
                <select
                  required
                  value={formData.customerId ?? ''}
                  onChange={(e) =>
                    setFormData({
                      ...formData,
                      customerId: e.target.value ? Number(e.target.value) : undefined,
                    })
                  }
                  className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                >
                  <option value="">Select customer…</option>
                  {customers.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.company}
                    </option>
                  ))}
                </select>
              </div>
            )}

            {!isExternalRole && !isClientTeamMode && (
              <>
                <div>
                  <label className="block mb-2">Job title</label>
                  <input
                    type="text"
                    value={formData.jobTitle ?? ''}
                    onChange={(e) => setFormData({ ...formData, jobTitle: e.target.value })}
                    placeholder="e.g. Senior Secretary"
                    className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                  />
                </div>
                <label className="flex items-center gap-2 text-sm">
                  <input
                    type="checkbox"
                    checked={Boolean(formData.canRecommendMoi)}
                    onChange={(e) => setFormData({ ...formData, canRecommendMoi: e.target.checked })}
                  />
                  Can recommend MOI (division group recommender)
                </label>
              </>
            )}
          </div>

          <div className="p-6 border-t border-border flex justify-end gap-3">
            <button
              type="button"
              onClick={onClose}
              className="px-6 py-2 border border-border rounded-lg hover:bg-muted transition-colors"
            >
              Cancel
            </button>
            <button
              type="submit"
              className="px-6 py-2 bg-primary text-primary-foreground rounded-lg hover:bg-primary/90 transition-colors"
            >
              Add User
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
