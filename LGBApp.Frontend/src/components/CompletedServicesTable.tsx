import { CheckCircle2, Search, History } from 'lucide-react';
import { useEffect, useState } from 'react';
import { ApiError, getCompletedServices, type CompletedServiceResponse } from '@/lib/api';

type CompletedService = CompletedServiceResponse;

interface CompletedServicesTableProps {
  onViewHistory?: () => void;
  refreshKey?: number;
}

export function CompletedServicesTable({ onViewHistory, refreshKey = 0 }: CompletedServicesTableProps) {
  const [searchTerm, setSearchTerm] = useState('');
  const [services, setServices] = useState<CompletedService[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const currentYear = new Date().getFullYear();

  useEffect(() => {
    setLoading(true);
    setError('');
    getCompletedServices(searchTerm || undefined, currentYear)
      .then((data) => {
        setServices(data);
      })
      .catch((err) => {
        setServices([]);
        setError(err instanceof ApiError ? err.message : 'Failed to load completed services.');
      })
      .finally(() => setLoading(false));
  }, [searchTerm, currentYear, refreshKey]);

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'Completed': return 'bg-green-100 text-green-800';
      case 'Canceled': return 'bg-red-100 text-red-800';
      default: return 'bg-gray-100 text-gray-800';
    }
  };

  return (
    <div className="bg-card rounded-lg border border-border overflow-hidden">
      <div className="p-4 border-b border-border">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <CheckCircle2 className="w-5 h-5 text-muted-foreground" />
            <h2>Completed Services ({currentYear})</h2>
          </div>
          <div className="flex items-center gap-2">
            <div className="relative">
              <Search className="w-4 h-4 absolute left-3 top-1/2 transform -translate-y-1/2 text-muted-foreground" />
              <input
                type="text"
                placeholder="Search..."
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                className="pl-9 pr-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring text-sm"
              />
            </div>
          </div>
        </div>
        {error && (
          <p className="mt-2 text-sm text-destructive">{error}</p>
        )}
      </div>

      <div className="overflow-auto" style={{ maxHeight: '400px' }}>
        <table className="w-full">
          <thead className="bg-muted/50 sticky top-0">
            <tr>
              <th className="px-4 py-3 text-left">Customer</th>
              <th className="px-4 py-3 text-left">Service</th>
              <th className="px-4 py-3 text-center">Usage</th>
              <th className="px-4 py-3 text-left">Date Requested</th>
              <th className="px-4 py-3 text-left">Date Completed</th>
              <th className="px-4 py-3 text-left">Account Holder</th>
              <th className="px-4 py-3 text-left">Job Assigned To</th>
              <th className="px-4 py-3 text-center">Status</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={8} className="px-4 py-8 text-center text-muted-foreground">
                  Loading completed services...
                </td>
              </tr>
            ) : services.length === 0 ? (
              <tr>
                <td colSpan={8} className="px-4 py-8 text-center text-muted-foreground">
                  No completed services found for {currentYear}
                </td>
              </tr>
            ) : (
              services.map((service) => (
                <tr
                  key={service.id}
                  className="border-t border-border hover:bg-muted/30 transition-colors"
                >
                  <td className="px-4 py-3 font-medium">{service.customer}</td>
                  <td className="px-4 py-3">{service.service}</td>
                  <td className="px-4 py-3 text-center">{service.usedQty}/{service.totalQty}</td>
                  <td className="px-4 py-3">{service.dateRequested}</td>
                  <td className="px-4 py-3">{service.dateCompleted}</td>
                  <td className="px-4 py-3">{service.accountHolder}</td>
                  <td className="px-4 py-3">{service.jobAssignedTo}</td>
                  <td className="px-4 py-3 text-center">
                    <span className={`px-2 py-1 rounded-full text-xs ${getStatusColor(service.status)}`}>
                      {service.status}
                    </span>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {onViewHistory && (
        <div className="p-4 border-t border-border flex justify-center">
          <button
            type="button"
            onClick={onViewHistory}
            className="flex items-center gap-2 px-6 py-2 bg-primary text-primary-foreground rounded-lg hover:bg-primary/90 transition-colors"
          >
            <History className="w-4 h-4" />
            View History
          </button>
        </div>
      )}
    </div>
  );
}

export type { CompletedService };
