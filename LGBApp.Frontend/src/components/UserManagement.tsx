import { Users, Plus, Edit, Trash2, X } from 'lucide-react';
import { useCallback, useEffect, useState } from 'react';
import {
  ApiError,
  deleteUser,
  getCustomers,
  getUsers,
  updateUser,
  type CustomerResponse,
  type UserResponse,
} from '@/lib/api';
import { ROLES, roleLabel } from '@/lib/roles';

interface UserManagementProps {
  onCreateUser: () => void;
  refreshKey?: number;
  mode?: 'internal' | 'clientTeam';
  title?: string;
  description?: string;
}

const EXTERNAL_ROLES = [ROLES.ClientAdmin, ROLES.Client];

interface EditFormState {
  name: string;
  email: string;
  mobile: string;
  role: string;
  jobTitle: string;
  canRecommendMoi: boolean;
  customerId?: number;
}

export function UserManagement({
  onCreateUser,
  refreshKey = 0,
  mode = 'internal',
  title = 'User Management',
  description,
}: UserManagementProps) {
  const isClientTeamMode = mode === 'clientTeam';
  const [users, setUsers] = useState<UserResponse[]>([]);
  const [customers, setCustomers] = useState<CustomerResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [editingUser, setEditingUser] = useState<UserResponse | null>(null);
  const [editForm, setEditForm] = useState<EditFormState>({
    name: '',
    email: '',
    mobile: '',
    role: ROLES.User,
    jobTitle: '',
    canRecommendMoi: false,
    customerId: undefined,
  });
  const [saving, setSaving] = useState(false);

  const loadUsers = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const userData = await getUsers();
      setUsers(userData);
      if (!isClientTeamMode) {
        const customerData = await getCustomers();
        setCustomers(customerData);
      }
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) {
        setError('Sign in as Admin to manage users.');
      } else if (err instanceof ApiError && err.status === 403) {
        setError('Admin role required to view users.');
      } else {
        setError('Could not load users. Make sure the backend is running.');
      }
    } finally {
      setLoading(false);
    }
  }, [isClientTeamMode]);

  useEffect(() => {
    void loadUsers();
  }, [loadUsers, refreshKey]);

  const handleEdit = (user: UserResponse) => {
    setEditingUser(user);
    setEditForm({
      name: user.name,
      email: user.email,
      mobile: user.mobile,
      role: user.role,
      jobTitle: user.jobTitle ?? '',
      canRecommendMoi: Boolean(user.canRecommendMoi),
      customerId: user.customerId,
    });
  };

  const isExternalRole = EXTERNAL_ROLES.includes(editForm.role as (typeof EXTERNAL_ROLES)[number]);

  const handleSaveEdit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editingUser) return;
    if (isExternalRole && !editForm.customerId) {
      setError('Select a customer for external users.');
      return;
    }
    setSaving(true);
    try {
      await updateUser(editingUser.userId, {
        email: editForm.email,
        name: editForm.name,
        mobile: editForm.mobile,
        role: isClientTeamMode ? ROLES.Client : editForm.role,
        jobTitle: isExternalRole || isClientTeamMode ? undefined : editForm.jobTitle || undefined,
        canRecommendMoi: isExternalRole || isClientTeamMode ? undefined : editForm.canRecommendMoi,
        customerId: isExternalRole || isClientTeamMode ? editForm.customerId : undefined,
      });
      setEditingUser(null);
      setError('');
      await loadUsers();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to update user.');
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async (user: UserResponse) => {
    if (!window.confirm(`Delete user ${user.name}?`)) return;
    try {
      await deleteUser(user.userId);
      setError('');
      await loadUsers();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to delete user.');
    }
  };

  const customerName = (customerId?: number) =>
    customers.find((c) => c.id === customerId)?.company ?? (customerId ? `Customer #${customerId}` : '—');

  return (
    <>
      <div className="bg-card rounded-lg border border-border overflow-hidden">
        <div className="p-4 border-b border-border flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Users className="w-5 h-5 text-muted-foreground" />
            <div>
              <h2>{title}</h2>
              {description && <p className="text-xs text-muted-foreground mt-0.5">{description}</p>}
            </div>
          </div>
          <button
            onClick={onCreateUser}
            className="flex items-center gap-2 px-4 py-2 bg-primary text-primary-foreground rounded-lg hover:bg-primary/90 transition-colors"
          >
            <Plus className="w-4 h-4" />
            {isClientTeamMode ? 'Invite member' : 'Add User'}
          </button>
        </div>

        {error && (
          <div className="px-4 py-3 text-sm text-destructive bg-destructive/10 border-b border-border">
            {error}
          </div>
        )}

        <div className="overflow-auto">
          <table className="w-full">
            <thead className="bg-muted/50">
              <tr>
                <th className="px-4 py-3 text-left">Name</th>
                <th className="px-4 py-3 text-left">Email</th>
                <th className="px-4 py-3 text-left">Mobile</th>
                <th className="px-4 py-3 text-left">Role</th>
                {!isClientTeamMode && <th className="px-4 py-3 text-left">Customer</th>}
                <th className="px-4 py-3 text-center">Actions</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan={isClientTeamMode ? 5 : 6} className="px-4 py-8 text-center text-muted-foreground">
                    Loading users...
                  </td>
                </tr>
              ) : users.length === 0 ? (
                <tr>
                  <td colSpan={isClientTeamMode ? 5 : 6} className="px-4 py-8 text-center text-muted-foreground">
                    {isClientTeamMode ? 'No team members yet. Invite your first user.' : 'No users found.'}
                  </td>
                </tr>
              ) : (
                users.map((user) => (
                  <tr
                    key={user.userId}
                    className="border-t border-border hover:bg-muted/30 transition-colors"
                  >
                    <td className="px-4 py-3 font-medium">{user.name}</td>
                    <td className="px-4 py-3">{user.email}</td>
                    <td className="px-4 py-3">{user.mobile || '—'}</td>
                    <td className="px-4 py-3">{roleLabel(user.role)}</td>
                    {!isClientTeamMode && (
                      <td className="px-4 py-3">{user.customerName ?? customerName(user.customerId)}</td>
                    )}
                    <td className="px-4 py-3">
                      <div className="flex items-center justify-center gap-2">
                        <button
                          type="button"
                          onClick={() => handleEdit(user)}
                          className="p-1 hover:bg-muted rounded transition-colors"
                        >
                          <Edit className="w-4 h-4" />
                        </button>
                        <button
                          type="button"
                          onClick={() => void handleDelete(user)}
                          className="p-1 hover:bg-destructive/10 text-destructive rounded transition-colors"
                        >
                          <Trash2 className="w-4 h-4" />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {editingUser && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
          <div className="bg-card rounded-lg border border-border w-full max-w-md max-h-[90vh] overflow-y-auto">
            <div className="p-4 border-b border-border flex items-center justify-between">
              <h3>Edit User</h3>
              <button type="button" onClick={() => setEditingUser(null)} className="p-1 hover:bg-muted rounded">
                <X className="w-5 h-5" />
              </button>
            </div>
            <form onSubmit={(e) => void handleSaveEdit(e)} className="p-4 space-y-3">
              <input
                type="text"
                required
                value={editForm.name}
                onChange={(e) => setEditForm({ ...editForm, name: e.target.value })}
                className="w-full px-3 py-2 border border-border rounded-lg"
                placeholder="Name"
              />
              <input
                type="email"
                required
                value={editForm.email}
                onChange={(e) => setEditForm({ ...editForm, email: e.target.value })}
                className="w-full px-3 py-2 border border-border rounded-lg"
                placeholder="Email"
              />
              <input
                type="tel"
                value={editForm.mobile}
                onChange={(e) => setEditForm({ ...editForm, mobile: e.target.value })}
                className="w-full px-3 py-2 border border-border rounded-lg"
                placeholder="Mobile"
              />
              {!isClientTeamMode ? (
                <select
                  value={editForm.role}
                  onChange={(e) =>
                    setEditForm({
                      ...editForm,
                      role: e.target.value,
                      customerId: EXTERNAL_ROLES.includes(e.target.value as (typeof EXTERNAL_ROLES)[number])
                        ? editForm.customerId
                        : undefined,
                    })
                  }
                  className="w-full px-3 py-2 border border-border rounded-lg"
                >
                  <option value={ROLES.Admin}>{roleLabel(ROLES.Admin)}</option>
                  <option value={ROLES.User}>{roleLabel(ROLES.User)}</option>
                  <option value={ROLES.ClientAdmin}>{roleLabel(ROLES.ClientAdmin)}</option>
                  <option value={ROLES.Client}>{roleLabel(ROLES.Client)}</option>
                </select>
              ) : (
                <input type="text" disabled value={roleLabel(ROLES.Client)} className="w-full px-3 py-2 border border-border rounded-lg bg-muted/30" />
              )}

              {isExternalRole && !isClientTeamMode ? (
                <select
                  required
                  value={editForm.customerId ?? ''}
                  onChange={(e) =>
                    setEditForm({
                      ...editForm,
                      customerId: e.target.value ? Number(e.target.value) : undefined,
                    })
                  }
                  className="w-full px-3 py-2 border border-border rounded-lg"
                >
                  <option value="">Select customer…</option>
                  {customers.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.company}
                    </option>
                  ))}
                </select>
              ) : (
                <>
                  <input
                    type="text"
                    value={editForm.jobTitle}
                    onChange={(e) => setEditForm({ ...editForm, jobTitle: e.target.value })}
                    className="w-full px-3 py-2 border border-border rounded-lg"
                    placeholder="Job title"
                  />
                  <label className="flex items-center gap-2 text-sm">
                    <input
                      type="checkbox"
                      checked={editForm.canRecommendMoi}
                      onChange={(e) => setEditForm({ ...editForm, canRecommendMoi: e.target.checked })}
                    />
                    Can recommend MOI
                  </label>
                </>
              )}

              <div className="flex justify-end gap-2 pt-2">
                <button type="button" onClick={() => setEditingUser(null)} className="px-4 py-2 border rounded-lg">
                  Cancel
                </button>
                <button type="submit" disabled={saving} className="px-4 py-2 bg-primary text-primary-foreground rounded-lg">
                  {saving ? 'Saving...' : 'Save'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </>
  );
}
