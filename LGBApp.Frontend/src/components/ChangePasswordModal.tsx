import { useState } from 'react';
import { KeyRound } from 'lucide-react';
import { ApiError, changePassword, setAuth } from '@/lib/api';

interface ChangePasswordModalProps {
  userName: string;
  onSuccess: () => void;
}

export function ChangePasswordModal({ userName, onSuccess }: ChangePasswordModalProps) {
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');

    if (newPassword.length < 6) {
      setError('New password must be at least 6 characters.');
      return;
    }
    if (newPassword !== confirmPassword) {
      setError('New password and confirmation do not match.');
      return;
    }

    setLoading(true);
    try {
      const response = await changePassword({
        currentPassword,
        newPassword,
        confirmPassword,
      });
      setAuth(response.token, response.user);
      onSuccess();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to change password.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 z-[200] bg-background/95 flex items-center justify-center p-4">
      <div className="bg-card border border-border rounded-lg w-full max-w-md p-8 shadow-lg">
        <div className="flex flex-col items-center mb-6">
          <div className="w-12 h-12 rounded-lg bg-primary flex items-center justify-center mb-4">
            <KeyRound className="w-6 h-6 text-primary-foreground" />
          </div>
          <h2 className="text-lg font-semibold text-center">Change your password</h2>
          <p className="text-sm text-muted-foreground text-center mt-2">
            Welcome, {userName}. For security, set a new password before using LGB Services.
          </p>
        </div>

        <form onSubmit={(e) => void handleSubmit(e)} className="space-y-4">
          {error && (
            <div className="px-4 py-3 rounded-lg bg-destructive/10 text-destructive text-sm">{error}</div>
          )}

          <div>
            <label className="block mb-2 text-sm">Current password</label>
            <input
              type="password"
              required
              autoComplete="current-password"
              value={currentPassword}
              onChange={(e) => setCurrentPassword(e.target.value)}
              className="w-full px-4 py-2 border border-border rounded-lg bg-input-background"
              placeholder="Temporary or current password"
            />
          </div>

          <div>
            <label className="block mb-2 text-sm">New password</label>
            <input
              type="password"
              required
              minLength={6}
              autoComplete="new-password"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              className="w-full px-4 py-2 border border-border rounded-lg bg-input-background"
            />
          </div>

          <div>
            <label className="block mb-2 text-sm">Confirm new password</label>
            <input
              type="password"
              required
              minLength={6}
              autoComplete="new-password"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              className="w-full px-4 py-2 border border-border rounded-lg bg-input-background"
            />
          </div>

          <button
            type="submit"
            disabled={loading}
            className="w-full px-4 py-3 bg-primary text-primary-foreground rounded-lg disabled:opacity-60"
          >
            {loading ? 'Saving…' : 'Set new password'}
          </button>
        </form>
      </div>
    </div>
  );
}
