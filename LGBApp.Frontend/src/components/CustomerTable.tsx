import { Users, Plus } from 'lucide-react';
import { useState } from 'react';
import type { CustomerResponse } from '@/lib/api';

type Customer = CustomerResponse;

function formatPackages(customer: Customer): string {
  const pkgs = customer.packages?.filter((p) => p.status === 'Active') ?? [];
  if (pkgs.length === 0) return customer.package || '—';
  if (pkgs.length === 1) return pkgs[0].packageName;
  return `${pkgs[0].packageName} +${pkgs.length - 1}`;
}

interface CustomerTableProps {
  customers: Customer[];
  onSelectCustomer: (customer: Customer) => void;
  selectedCustomer: Customer | null;
  onCreateNew: () => void;
}

export function CustomerTable({ customers, onSelectCustomer, selectedCustomer, onCreateNew }: CustomerTableProps) {
  const [statusFilter, setStatusFilter] = useState<'all' | 'Active' | 'Non-Active'>('all');

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'Active': return 'bg-green-100 text-green-800';
      case 'Non-Active': return 'bg-gray-100 text-gray-800';
      default: return 'bg-gray-100 text-gray-800';
    }
  };

  const filteredCustomers = customers
    .filter(customer => statusFilter === 'all' || customer.status === statusFilter)
    .sort((a, b) => {
      if (a.status === 'Active' && b.status === 'Non-Active') return -1;
      if (a.status === 'Non-Active' && b.status === 'Active') return 1;
      return 0;
    });

  return (
    <div className="bg-card rounded-lg border border-border overflow-hidden">
      <div className="p-4 border-b border-border flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Users className="w-5 h-5 text-muted-foreground" />
          <h2>Customers</h2>
        </div>
        <div className="flex items-center gap-2">
          <div className="flex gap-1 border border-border rounded-lg p-1">
            <button
              onClick={() => setStatusFilter('all')}
              className={`px-3 py-1 rounded text-sm transition-colors ${
                statusFilter === 'all'
                  ? 'bg-primary text-primary-foreground'
                  : 'hover:bg-muted'
              }`}
            >
              All
            </button>
            <button
              onClick={() => setStatusFilter('Active')}
              className={`px-3 py-1 rounded text-sm transition-colors ${
                statusFilter === 'Active'
                  ? 'bg-primary text-primary-foreground'
                  : 'hover:bg-muted'
              }`}
            >
              Active Only
            </button>
            <button
              onClick={() => setStatusFilter('Non-Active')}
              className={`px-3 py-1 rounded text-sm transition-colors ${
                statusFilter === 'Non-Active'
                  ? 'bg-primary text-primary-foreground'
                  : 'hover:bg-muted'
              }`}
            >
              Non-Active Only
            </button>
          </div>
          <button
            onClick={onCreateNew}
            className="flex items-center gap-2 px-4 py-2 bg-primary text-primary-foreground rounded-lg hover:bg-primary/90 transition-colors"
          >
            <Plus className="w-4 h-4" />
            Create New
          </button>
        </div>
      </div>
      <div className="overflow-auto" style={{ maxHeight: '600px' }}>
        <table className="w-full">
          <thead className="bg-muted/50 sticky top-0">
            <tr>
              <th className="px-4 py-3 text-left">Company</th>
              <th className="px-4 py-3 text-left">Invoice by</th>
              <th className="px-4 py-3 text-left">Charge to</th>
              <th className="px-4 py-3 text-left">Package</th>
              <th className="px-4 py-3 text-right">Active Value</th>
              <th className="px-4 py-3 text-center">COSEC</th>
              <th className="px-4 py-3 text-left">MOI</th>
              <th className="px-4 py-3 text-left">MOA</th>
              <th className="px-4 py-3 text-left">Status</th>
            </tr>
          </thead>
          <tbody>
            {filteredCustomers.length === 0 ? (
              <tr>
                <td colSpan={9} className="px-4 py-12 text-center text-muted-foreground">
                  No customers yet. Click <strong>Create New</strong> to add your first customer.
                </td>
              </tr>
            ) : filteredCustomers.map((customer) => (
              <tr
                key={customer.id}
                onClick={() => onSelectCustomer(customer)}
                className={`border-t border-border cursor-pointer hover:bg-muted/30 transition-colors ${
                  selectedCustomer?.id === customer.id ? 'bg-muted/50' : ''
                }`}
              >
                <td className="px-4 py-3">{customer.company || ''}</td>
                <td className="px-4 py-3">{customer.invoiceBy || ''}</td>
                <td className="px-4 py-3">{customer.chargeTo || ''}</td>
                <td className="px-4 py-3">{formatPackages(customer)}</td>
                <td className="px-4 py-3 text-right">MYR {(customer.value || customer.packageValue || 0).toLocaleString()}</td>
                <td className="px-4 py-3 text-center">
                  <input
                    type="checkbox"
                    checked={customer.cosec || false}
                    readOnly
                    className="w-4 h-4 cursor-pointer"
                    onClick={(e) => e.stopPropagation()}
                  />
                </td>
                <td className="px-4 py-3">{(customer.moi || []).join(', ')}</td>
                <td className="px-4 py-3">{(customer.moa || []).join(', ')}</td>
                <td className="px-4 py-3">
                  <span className={`px-2 py-1 rounded-full text-xs ${getStatusColor(customer.status)}`}>
                    {customer.status || 'Active'}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
