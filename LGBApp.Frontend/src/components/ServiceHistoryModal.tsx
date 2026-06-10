import { X, Search } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { ApiError, getCompletedServices, type CompletedServiceResponse } from '@/lib/api';

interface ServiceHistoryModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export function ServiceHistoryModal({ isOpen, onClose }: ServiceHistoryModalProps) {
  const [searchTerm, setSearchTerm] = useState('');
  const [yearFilter, setYearFilter] = useState<string>('all');
  const [services, setServices] = useState<CompletedServiceResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const currentYear = new Date().getFullYear();

  useEffect(() => {
    if (!isOpen) return;
    setLoading(true);
    setError('');
    getCompletedServices()
      .then(setServices)
      .catch((err) => {
        setServices([]);
        setError(err instanceof ApiError ? err.message : 'Failed to load service history.');
      })
      .finally(() => setLoading(false));
  }, [isOpen]);

  const historicalServices = useMemo(
    () => services.filter((s) => new Date(s.dateCompleted).getFullYear() < currentYear),
    [services, currentYear],
  );

  const availableYears = useMemo(
    () =>
      Array.from(
        new Set(historicalServices.map((s) => new Date(s.dateCompleted).getFullYear())),
      ).sort((a, b) => b - a),
    [historicalServices],
  );

  const filteredServices = useMemo(() => {
    const term = searchTerm.toLowerCase();
    return historicalServices.filter((service) => {
      const serviceYear = new Date(service.dateCompleted).getFullYear();
      const matchesYear = yearFilter === 'all' || serviceYear === parseInt(yearFilter, 10);
      const matchesSearch =
        term === '' ||
        service.customer.toLowerCase().includes(term) ||
        service.service.toLowerCase().includes(term) ||
        service.accountHolder.toLowerCase().includes(term) ||
        service.jobAssignedTo.toLowerCase().includes(term);
      return matchesYear && matchesSearch;
    });
  }, [historicalServices, searchTerm, yearFilter]);

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'Completed':
        return 'bg-green-100 text-green-800';
      case 'Canceled':
        return 'bg-red-100 text-red-800';
      default:
        return 'bg-gray-100 text-gray-800';
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
      <div className="bg-card rounded-lg border border-border w-full max-w-6xl max-h-[90vh] overflow-hidden flex flex-col">
        <div className="p-6 border-b border-border flex items-center justify-between">
          <h2>Service History</h2>
          <button
            onClick={onClose}
            className="p-1 hover:bg-muted rounded transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="p-4 border-b border-border flex items-center gap-4">
          <div className="relative flex-1">
            <Search className="w-4 h-4 absolute left-3 top-1/2 transform -translate-y-1/2 text-muted-foreground" />
            <input
              type="text"
              placeholder="Search services..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              className="w-full pl-9 pr-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
            />
          </div>
          <select
            value={yearFilter}
            onChange={(e) => setYearFilter(e.target.value)}
            className="px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
          >
            <option value="all">All Years</option>
            {availableYears.map((year) => (
              <option key={year} value={year}>
                {year}
              </option>
            ))}
          </select>
        </div>

        {error && (
          <div className="px-4 py-3 text-sm text-destructive bg-destructive/10 border-b border-border">
            {error}
          </div>
        )}

        <div className="flex-1 overflow-auto">
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
                    Loading history...
                  </td>
                </tr>
              ) : filteredServices.length === 0 ? (
                <tr>
                  <td colSpan={8} className="px-4 py-8 text-center text-muted-foreground">
                    No historical services found
                  </td>
                </tr>
              ) : (
                filteredServices.map((service) => (
                  <tr
                    key={service.id}
                    className="border-t border-border hover:bg-muted/30 transition-colors"
                  >
                    <td className="px-4 py-3 font-medium">{service.customer}</td>
                    <td className="px-4 py-3">{service.service}</td>
                    <td className="px-4 py-3 text-center">
                      {service.usedQty}/{service.totalQty}
                    </td>
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

        <div className="p-4 border-t border-border flex justify-end">
          <button
            onClick={onClose}
            className="px-6 py-2 border border-border rounded-lg hover:bg-muted transition-colors"
          >
            Close
          </button>
        </div>
      </div>
    </div>
  );
}
