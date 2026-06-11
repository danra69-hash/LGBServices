import { Link2, RefreshCw } from 'lucide-react';
import { useCallback, useEffect, useState } from 'react';
import {
  ApiError,
  getSignatoryOverlaps,
  linkSignatoryByEmail,
  type SignatoryOverlapDto,
} from '@/lib/api';

interface AdminSignatoryDedupProps {
  refreshKey?: number;
}

function roleFlags(company: SignatoryOverlapDto['companies'][number]) {
  const flags: string[] = [];
  if (company.needsMoi) flags.push('MOI');
  if (company.needsMoiApproval) flags.push('MOI approval');
  if (company.needsMoa) flags.push('MOA');
  return flags.join(', ') || '—';
}

export function AdminSignatoryDedup({ refreshKey = 0 }: AdminSignatoryDedupProps) {
  const [overlaps, setOverlaps] = useState<SignatoryOverlapDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [linkingEmail, setLinkingEmail] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      setOverlaps(await getSignatoryOverlaps());
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to load signatory overlaps.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load, refreshKey]);

  const handleLink = async (email: string) => {
    setLinkingEmail(email);
    setMessage('');
    setError('');
    try {
      const result = await linkSignatoryByEmail(email);
      setMessage(result.message);
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to link signatory.');
    } finally {
      setLinkingEmail(null);
    }
  };

  const needsAttention = overlaps.filter((o) => !o.isLinked);

  return (
    <section className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
      <div className="mb-4 flex flex-wrap items-start justify-between gap-3">
        <div>
          <h3 className="text-lg font-semibold text-slate-900">Cross-company signatories</h3>
          <p className="mt-1 max-w-3xl text-sm text-slate-600">
            Detects the same email on account holders across multiple companies. Link them to one
            signatory login so a director signing MOAs for entities A, B, C and D does not need
            duplicate accounts.
          </p>
        </div>
        <button
          type="button"
          onClick={() => void load()}
          className="inline-flex items-center gap-2 rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-700 hover:bg-slate-50"
        >
          <RefreshCw className="h-4 w-4" />
          Refresh
        </button>
      </div>

      {loading && <p className="text-sm text-slate-500">Loading overlaps…</p>}
      {error && <p className="mb-3 text-sm text-red-600">{error}</p>}
      {message && <p className="mb-3 text-sm text-emerald-700">{message}</p>}

      {!loading && overlaps.length === 0 && (
        <p className="text-sm text-slate-500">No cross-company signatory overlaps found.</p>
      )}

      {!loading && overlaps.length > 0 && (
        <div className="space-y-4">
          {needsAttention.length > 0 && (
            <p className="text-sm font-medium text-amber-800">
              {needsAttention.length} email(s) need linking to a single login.
            </p>
          )}

          {overlaps.map((overlap) => (
            <div key={overlap.email} className="rounded-lg border border-slate-200 p-4">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div>
                  <p className="font-medium text-slate-900">{overlap.primaryName}</p>
                  <p className="text-sm text-slate-600">{overlap.email}</p>
                  <p className="mt-1 text-xs text-slate-500">
                    {overlap.companyCount} companies
                    {overlap.isLinked
                      ? ` · linked (user #${overlap.linkedUserId})`
                      : ' · not linked'}
                  </p>
                </div>
                {!overlap.isLinked && (
                  <button
                    type="button"
                    disabled={linkingEmail === overlap.email}
                    onClick={() => void handleLink(overlap.email)}
                    className="inline-flex items-center gap-2 rounded-lg bg-slate-900 px-3 py-2 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-60"
                  >
                    <Link2 className="h-4 w-4" />
                    {linkingEmail === overlap.email ? 'Linking…' : 'Link to one login'}
                  </button>
                )}
              </div>

              <div className="mt-3 overflow-x-auto">
                <table className="min-w-full text-left text-sm">
                  <thead>
                    <tr className="border-b border-slate-200 text-slate-500">
                      <th className="py-2 pr-4 font-medium">Company</th>
                      <th className="py-2 pr-4 font-medium">Holder name</th>
                      <th className="py-2 pr-4 font-medium">Roles</th>
                      <th className="py-2 font-medium">User</th>
                    </tr>
                  </thead>
                  <tbody>
                    {overlap.companies.map((company) => (
                      <tr key={company.accountHolderId} className="border-b border-slate-100">
                        <td className="py-2 pr-4">{company.company}</td>
                        <td className="py-2 pr-4">{company.holderName}</td>
                        <td className="py-2 pr-4">{roleFlags(company)}</td>
                        <td className="py-2">
                          {company.userId ? `#${company.userId}` : '—'}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          ))}
        </div>
      )}
    </section>
  );
}
