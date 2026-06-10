import { Package, Plus, Edit, Trash2 } from 'lucide-react';
import { useEffect, useState } from 'react';
import { ApiError, deleteProduct, getProducts, type ProductResponse } from '@/lib/api';
import { availableServices } from './CreateProductModal';

interface ProductsTableProps {
  onCreateNew: () => void;
  onEdit?: (product: ProductResponse) => void;
  onDelete?: (product: ProductResponse) => void;
  refreshKey?: number;
}

export function ProductsTable({ onCreateNew, onEdit, onDelete, refreshKey = 0 }: ProductsTableProps) {
  const [products, setProducts] = useState<ProductResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const loadProducts = () => {
    setLoading(true);
    getProducts()
      .then(setProducts)
      .catch((err) => {
        setError(err instanceof ApiError ? err.message : 'Failed to load products.');
      })
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    loadProducts();
  }, [refreshKey]);

  const handleDelete = async (product: ProductResponse) => {
    if (!window.confirm(`Delete product "${product.packageName}"? This cannot be undone.`)) return;
    try {
      await deleteProduct(product.id);
      onDelete?.(product);
      setProducts((prev) => prev.filter((p) => p.id !== product.id));
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to delete product.');
    }
  };

  return (
    <div className="bg-card rounded-lg border border-border overflow-hidden">
      <div className="p-4 border-b border-border flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Package className="w-5 h-5 text-muted-foreground" />
          <h2>Products</h2>
        </div>
        <button
          onClick={onCreateNew}
          className="flex items-center gap-2 px-4 py-2 bg-primary text-primary-foreground rounded-lg hover:bg-primary/90 transition-colors"
        >
          <Plus className="w-4 h-4" />
          Create New
        </button>
      </div>

      {error && (
        <div className="px-4 py-3 text-sm text-destructive bg-destructive/10 border-b border-border">
          {error}
        </div>
      )}

      <div className="overflow-auto">
        <table className="w-full">
          <thead className="bg-muted/50 sticky top-0">
            <tr>
              <th className="px-4 py-3 text-left">Name of the Package</th>
              <th className="px-4 py-3 text-left">Services</th>
              <th className="px-4 py-3 text-left">Unit</th>
              <th className="px-4 py-3 text-right">QTY/Year</th>
              <th className="px-4 py-3 text-right">Package Price</th>
              <th className="px-4 py-3 text-left">Bundled Extras</th>
              <th className="px-4 py-3 text-right">Extras QTY</th>
              <th className="px-4 py-3 text-right">Extras Value</th>
              <th className="px-4 py-3 text-center">Actions</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={9} className="px-4 py-8 text-center text-muted-foreground">
                  Loading products...
                </td>
              </tr>
            ) : products.length === 0 ? (
              <tr>
                <td colSpan={9} className="px-4 py-8 text-center text-muted-foreground">
                  No products yet. Create your first package.
                </td>
              </tr>
            ) : (
              products.map((product) => {
                const services = product.services ?? [];
                const serviceQuantities = product.serviceQuantities ?? {};
                const addOns = product.addOns ?? [];
                const packagePrice = Number(product.packagePrice ?? 0);

                return (
                <tr
                  key={product.id}
                  className="border-t border-border hover:bg-muted/30 transition-colors"
                >
                  <td className="px-4 py-3 font-medium">{product.packageName}</td>
                  <td className="px-4 py-3">
                    <div className="max-w-xs">
                      {services.length > 0 ? (
                        <ul className="text-sm space-y-1">
                          {services.map((service, idx) => (
                            <li key={idx} className="text-muted-foreground">
                              • {service}{' '}
                              <span className="font-medium">
                                (Qty: {serviceQuantities[service] || 0})
                              </span>
                            </li>
                          ))}
                        </ul>
                      ) : (
                        <span className="text-muted-foreground text-sm">No services</span>
                      )}
                    </div>
                  </td>
                  <td className="px-4 py-3">{product.unit}</td>
                  <td className="px-4 py-3 text-right">{product.qtyPerYear ?? 0}</td>
                  <td className="px-4 py-3 text-right">
                    MYR {packagePrice.toLocaleString('en-MY', { minimumFractionDigits: 2 })}
                  </td>
                  <td className="px-4 py-3">
                    <div className="max-w-xs">
                      {addOns.length > 0 ? (
                        <ul className="text-sm space-y-1">
                          {addOns.map((addOn, idx) => (
                            <li key={idx} className="text-muted-foreground">
                              • {addOn}{' '}
                              <span className="font-medium">
                                (Qty: {product.addOnQuantities?.[addOn] ?? 0})
                              </span>
                            </li>
                          ))}
                        </ul>
                      ) : (
                        <span className="text-muted-foreground text-sm">None</span>
                      )}
                    </div>
                  </td>
                  <td className="px-4 py-3 text-right">{product.addOnsQty ?? 0}</td>
                  <td className="px-4 py-3 text-right">
                    MYR {Number(product.addOnPrice ?? 0).toLocaleString('en-MY', { minimumFractionDigits: 2 })}
                  </td>
                  <td className="px-4 py-3 text-center">
                    <div className="flex items-center justify-center gap-1">
                      <button
                        type="button"
                        onClick={() => onEdit?.(product)}
                        className="p-1 hover:bg-muted rounded transition-colors"
                        title="Edit Package"
                      >
                        <Edit className="w-4 h-4" />
                      </button>
                      <button
                        type="button"
                        onClick={() => handleDelete(product)}
                        className="p-1 hover:bg-destructive/10 text-destructive rounded transition-colors"
                        title="Delete Package"
                      >
                        <Trash2 className="w-4 h-4" />
                      </button>
                    </div>
                  </td>
                </tr>
              );
              })
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}

export { availableServices };
export type { ProductResponse as Product };
