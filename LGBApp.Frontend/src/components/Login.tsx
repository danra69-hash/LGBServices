import { useState } from 'react';
import { ArrowLeft, LogIn, KeyRound } from 'lucide-react';
import { ApiError, forgotPassword, login, resetPasswordWithOtp, setAuth } from '@/lib/api';

interface LoginProps {
  onSuccess?: () => void;
}

type View = 'login' | 'forgot-email' | 'forgot-code' | 'forgot-done';

export function Login({ onSuccess }: LoginProps) {
  const [view, setView] = useState<View>('login');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [code, setCode] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [info, setInfo] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const clearAlerts = () => {
    setError('');
    setInfo('');
  };

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    clearAlerts();
    setLoading(true);

    try {
      const response = await login(email, password);
      setAuth(response.token, response.user);
      onSuccess?.();
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message || 'Invalid email or password.');
      } else {
        setError('Unable to reach the server. Is the backend running on port 5003?');
      }
    } finally {
      setLoading(false);
    }
  };

  const handleRequestCode = async (e: React.FormEvent) => {
    e.preventDefault();
    clearAlerts();
    setLoading(true);
    try {
      const res = await forgotPassword(email.trim());
      setInfo(res.message);
      setView('forgot-code');
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Unable to send reset code.');
    } finally {
      setLoading(false);
    }
  };

  const handleResetPassword = async (e: React.FormEvent) => {
    e.preventDefault();
    clearAlerts();
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
      const res = await resetPasswordWithOtp({
        email: email.trim(),
        code: code.trim(),
        newPassword,
        confirmPassword,
      });
      setInfo(res.message);
      setPassword('');
      setCode('');
      setNewPassword('');
      setConfirmPassword('');
      setView('forgot-done');
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Unable to reset password.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="flex items-center justify-center min-h-full">
      <div className="w-full max-w-md">
        <div className="bg-card rounded-lg border border-border p-8">
          <div className="flex flex-col items-center mb-6">
            <div className="w-12 h-12 rounded-lg bg-primary flex items-center justify-center mb-4">
              {view === 'login' ? (
                <LogIn className="w-6 h-6 text-primary-foreground" />
              ) : (
                <KeyRound className="w-6 h-6 text-primary-foreground" />
              )}
            </div>
            <h2 className="text-center">
              {view === 'login' && 'Login to LGB Services'}
              {view === 'forgot-email' && 'Forgot password'}
              {view === 'forgot-code' && 'Enter reset code'}
              {view === 'forgot-done' && 'Password updated'}
            </h2>
            <p className="text-muted-foreground text-center mt-2 text-sm">
              {view === 'login' && 'Enter your credentials to access your account'}
              {view === 'forgot-email' && 'We will email a one-time code to reset your password'}
              {view === 'forgot-code' && `Enter the 6-digit code sent to ${email}`}
              {view === 'forgot-done' && 'You can sign in with your new password'}
            </p>
          </div>

          {(error || info) && (
            <div
              className={`mb-4 px-4 py-3 rounded-lg text-sm ${
                error ? 'bg-destructive/10 text-destructive' : 'bg-primary/10 text-foreground'
              }`}
            >
              {error || info}
            </div>
          )}

          {view === 'login' && (
            <form onSubmit={(e) => void handleLogin(e)} className="space-y-4">
              <div>
                <label htmlFor="email" className="block mb-2">
                  Email
                </label>
                <input
                  id="email"
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  className="w-full px-4 py-2 bg-input-background border border-border rounded-lg focus:outline-none focus:ring-2 focus:ring-ring transition-colors"
                  placeholder="Enter your email"
                  required
                />
              </div>

              <div>
                <div className="flex items-center justify-between mb-2">
                  <label htmlFor="password">Password</label>
                  <button
                    type="button"
                    className="text-sm text-primary hover:underline"
                    onClick={() => {
                      clearAlerts();
                      setView('forgot-email');
                    }}
                  >
                    Forgot password?
                  </button>
                </div>
                <input
                  id="password"
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  className="w-full px-4 py-2 bg-input-background border border-border rounded-lg focus:outline-none focus:ring-2 focus:ring-ring transition-colors"
                  placeholder="Enter your password"
                  required
                />
              </div>

              <button
                type="submit"
                disabled={loading}
                className="w-full px-4 py-3 bg-primary text-primary-foreground rounded-lg hover:opacity-90 transition-opacity mt-6 disabled:opacity-60"
              >
                {loading ? 'Signing in...' : 'Sign In'}
              </button>
            </form>
          )}

          {view === 'forgot-email' && (
            <form onSubmit={(e) => void handleRequestCode(e)} className="space-y-4">
              <div>
                <label htmlFor="reset-email" className="block mb-2">
                  Email
                </label>
                <input
                  id="reset-email"
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  className="w-full px-4 py-2 bg-input-background border border-border rounded-lg focus:outline-none focus:ring-2 focus:ring-ring"
                  placeholder="Account email"
                  required
                />
              </div>
              <button
                type="submit"
                disabled={loading}
                className="w-full px-4 py-3 bg-primary text-primary-foreground rounded-lg disabled:opacity-60"
              >
                {loading ? 'Sending…' : 'Send reset code'}
              </button>
              <button
                type="button"
                className="w-full inline-flex items-center justify-center gap-1 text-sm text-muted-foreground hover:text-foreground"
                onClick={() => {
                  clearAlerts();
                  setView('login');
                }}
              >
                <ArrowLeft className="w-4 h-4" /> Back to sign in
              </button>
            </form>
          )}

          {view === 'forgot-code' && (
            <form onSubmit={(e) => void handleResetPassword(e)} className="space-y-4">
              <div>
                <label htmlFor="otp" className="block mb-2">
                  6-digit code
                </label>
                <input
                  id="otp"
                  inputMode="numeric"
                  autoComplete="one-time-code"
                  maxLength={6}
                  value={code}
                  onChange={(e) => setCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
                  className="w-full px-4 py-2 bg-input-background border border-border rounded-lg tracking-[0.3em] text-center text-lg font-semibold focus:outline-none focus:ring-2 focus:ring-ring"
                  placeholder="000000"
                  required
                />
              </div>
              <div>
                <label htmlFor="new-password" className="block mb-2">
                  New password
                </label>
                <input
                  id="new-password"
                  type="password"
                  value={newPassword}
                  onChange={(e) => setNewPassword(e.target.value)}
                  className="w-full px-4 py-2 bg-input-background border border-border rounded-lg focus:outline-none focus:ring-2 focus:ring-ring"
                  required
                  minLength={6}
                />
              </div>
              <div>
                <label htmlFor="confirm-password" className="block mb-2">
                  Confirm password
                </label>
                <input
                  id="confirm-password"
                  type="password"
                  value={confirmPassword}
                  onChange={(e) => setConfirmPassword(e.target.value)}
                  className="w-full px-4 py-2 bg-input-background border border-border rounded-lg focus:outline-none focus:ring-2 focus:ring-ring"
                  required
                  minLength={6}
                />
              </div>
              <button
                type="submit"
                disabled={loading || code.length !== 6}
                className="w-full px-4 py-3 bg-primary text-primary-foreground rounded-lg disabled:opacity-60"
              >
                {loading ? 'Updating…' : 'Reset password'}
              </button>
              <button
                type="button"
                className="w-full text-sm text-primary hover:underline"
                disabled={loading}
                onClick={() => void handleRequestCode({ preventDefault() {} } as React.FormEvent)}
              >
                Resend code
              </button>
              <button
                type="button"
                className="w-full inline-flex items-center justify-center gap-1 text-sm text-muted-foreground hover:text-foreground"
                onClick={() => {
                  clearAlerts();
                  setView('forgot-email');
                }}
              >
                <ArrowLeft className="w-4 h-4" /> Change email
              </button>
            </form>
          )}

          {view === 'forgot-done' && (
            <button
              type="button"
              className="w-full px-4 py-3 bg-primary text-primary-foreground rounded-lg"
              onClick={() => {
                clearAlerts();
                setView('login');
              }}
            >
              Back to sign in
            </button>
          )}
        </div>
      </div>
    </div>
  );
}
