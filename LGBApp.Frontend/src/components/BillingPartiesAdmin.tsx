import { useCallback, useEffect, useState } from 'react';
import { Plus, Trash2 } from 'lucide-react';
import {
  ApiError,
  createBillingParty,
  deleteBillingParty,
  getBillingParties,
  type BillingPartyDto,
} from '@/lib/api';

export function BillingPartiesAdmin() {
  const [items, setItems] = useState<BillingPartyDto[]>([]);
  const [name, setName] = useState('');
  const [category, setCategory] = useState('Both');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setItems(await getBillingParties(false));
      setError('');
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to load billing parties.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const handleAdd = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) return;
    try {
      await createBillingParty({ name: name.trim(), category, isActive: true, sortOrder: items.length });
      setName('');
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to add entry.');
    }
  };

  const handleDeactivate = async (id: number) => {
    try {
      await deleteBillingParty(id);
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to remove entry.');
    }
  };

  return (
    <div className="bg-card border border-border rounded-lg overflow-hidden">
      <div className="p-4 border-b border-border">
        <h3 className="font-medium">Invoice By / Charge To directory</h3>
        <p className="text-xs text-muted-foreground mt-1">
          Maintain billing entity names. Customers can select multiple entries for Invoice By and Charge To.
        </p>
      </div>

      <form onSubmit={(e) => void handleAdd(e)} className="p-4 border-b border-border flex flex-wrap gap-2 items-end">
        <div className="flex-1 min-w-[200px]">
          <label className="block text-xs mb-1">Name</label>
          <input
            value={name}
            onChange={(e) => setName(e.target.value)}
            className="w-full px-3 py-2 border border-border rounded-lg text-sm"
            placeholder="e.g. Acme Corp Sdn Bhd"
          />
        </div>
        <div>
          <label className="block text-xs mb-1">Use for</label>
          <select
            value={category}
            onChange={(e) => setCategory(e.target.value)}
            className="px-3 py-2 border border-border rounded-lg text-sm"
          >
            <option value="Both">Both</option>
            <option value="InvoiceBy">Invoice By only</option>
            <option value="ChargeTo">Charge To only</option>
          </select>
        </div>
        <button type="submit" className="flex items-center gap-1 px-3 py-2 bg-primary text-primary-foreground rounded-lg text-sm">
          <Plus className="w-4 h-4" />
          Add
        </button>
      </form>

      {error && <p className="px-4 py-2 text-sm text-destructive">{error}</p>}

      {loading ? (
        <p className="p-4 text-sm text-muted-foreground">Loading…</p>
      ) : (
        <table className="w-full text-sm">
          <thead className="bg-muted/50">
            <tr>
              <th className="px-4 py-2 text-left">Name</th>
              <th className="px-4 py-2 text-left">Category</th>
              <th className="px-4 py-2 text-center">Active</th>
              <th className="px-4 py-2 text-right">Actions</th>
            </tr>
          </thead>
          <tbody>
            {items.map((item) => (
              <tr key={item.id} className="border-t border-border">
                <td className="px-4 py-2">{item.name}</td>
                <td className="px-4 py-2">{item.category}</td>
                <td className="px-4 py-2 text-center">{item.isActive ? 'Yes' : 'No'}</td>
                <td className="px-4 py-2 text-right">
                  {item.isActive && (
                    <button
                      type="button"
                      onClick={() => void handleDeactivate(item.id)}
                      className="inline-flex items-center gap-1 text-xs text-destructive hover:underline"
                    >
                      <Trash2 className="w-3 h-3" />
                      Deactivate
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
