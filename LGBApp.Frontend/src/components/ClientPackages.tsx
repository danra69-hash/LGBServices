import { useCallback, useEffect, useState } from 'react';
import { Package } from 'lucide-react';
import { ApiError, getClientPortalSummary, getMyCompany, type ClientPortalSummaryDto, type CustomerResponse } from '@/lib/api';

interface ClientPackagesProps {
  refreshKey?: number;
}

export function ClientPackages({ refreshKey = 0 }: ClientPackagesProps) {
  const [company, setCompany] = useState<CustomerResponse | null>(null);
  const [summary, setSummary] = useState<ClientPortalSummaryDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const [co, sum] = await Promise.all([getMyCompany(), getClientPortalSummary()]);
      setCompany(co);
      setSummary(sum);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to load packages.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load, refreshKey]);

  if (loading) return <p className="text-sm text-muted-foreground p-6">Loading your packages…</p>;
  if (error) return <p className="text-sm text-destructive p-6">{error}</p>;

  const packages = company?.packages ?? [];

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-semibold">{company?.company ?? summary?.companyName}</h2>
        <p className="text-sm text-muted-foreground mt-1">Your purchased LGB service packages</p>
      </div>

      {summary && (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          <div className="bg-card border border-border rounded-lg p-4">
            <p className="text-xs text-muted-foreground">Active packages</p>
            <p className="text-2xl font-semibold mt-1">{summary.activePackages}</p>
          </div>
          <div className="bg-card border border-border rounded-lg p-4">
            <p className="text-xs text-muted-foreground">Active value (MYR)</p>
            <p className="text-2xl font-semibold mt-1">
              {summary.activePackageValue.toLocaleString('en-MY', { minimumFractionDigits: 2 })}
            </p>
          </div>
          <div className="bg-card border border-border rounded-lg p-4">
            <p className="text-xs text-muted-foreground">Open tasks</p>
            <p className="text-2xl font-semibold mt-1">{summary.openJobs}</p>
          </div>
          <div className="bg-card border border-border rounded-lg p-4">
            <p className="text-xs text-muted-foreground">Completed tasks</p>
            <p className="text-2xl font-semibold mt-1">{summary.completedJobs}</p>
          </div>
        </div>
      )}

      <div className="bg-card border border-border rounded-lg overflow-hidden">
        <div className="p-4 border-b border-border flex items-center gap-2">
          <Package className="w-5 h-5 text-muted-foreground" />
          <h3>Purchased packages</h3>
        </div>
        {packages.length === 0 ? (
          <p className="p-6 text-sm text-muted-foreground">No packages on record yet.</p>
        ) : (
          <table className="w-full text-sm">
            <thead className="bg-muted/50">
              <tr>
                <th className="px-4 py-3 text-left">Package</th>
                <th className="px-4 py-3 text-left">Validity</th>
                <th className="px-4 py-3 text-left">Purchased</th>
                <th className="px-4 py-3 text-left">Expires</th>
                <th className="px-4 py-3 text-right">Value (MYR)</th>
                <th className="px-4 py-3 text-center">Status</th>
              </tr>
            </thead>
            <tbody>
              {packages.map((pkg) => (
                <tr key={pkg.id} className="border-t border-border">
                  <td className="px-4 py-3 font-medium">{pkg.packageName}</td>
                  <td className="px-4 py-3">{pkg.validity || '1 Year'}</td>
                  <td className="px-4 py-3">{pkg.purchasedDate}</td>
                  <td className="px-4 py-3">{pkg.expiryDate}</td>
                  <td className="px-4 py-3 text-right">
                    {(pkg.activeValue ?? pkg.packageValue ?? 0).toLocaleString('en-MY', { minimumFractionDigits: 2 })}
                  </td>
                  <td className="px-4 py-3 text-center">
                    <span className="text-xs px-2 py-0.5 rounded-full bg-green-100 text-green-800">{pkg.status}</span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
