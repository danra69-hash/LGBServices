import { X } from 'lucide-react';
import { useState, useEffect } from 'react';
import { PRODUCT_BUNDLED_ADD_ONS } from '@/lib/productCatalog';

interface Service {
  name: string;
  unit: string;
}

const availableServices: Service[] = [
  { name: 'Annual Return', unit: 'EACH' },
  { name: 'BO Declaration', unit: 'EACH' },
  { name: 'AR Filing to MBRS', unit: 'EACH' },
  { name: 'Resolution on annual audited account filing', unit: 'EACH' },
  { name: 'Submission of annual audited account MBRS zip file', unit: 'EACH' },
  { name: 'Prov of register Office', unit: 'EACH' },
  { name: 'Assisting Auditor on statutory Audit', unit: 'EACH' },
  { name: 'Secretarial record Checks', unit: 'EACH' },
  { name: 'Prepare Resolution', unit: 'EACH' },
  { name: 'Follow up with Reso signatory', unit: 'EACH' },
];

interface CreateProductModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (data: Record<string, unknown>) => void | Promise<void>;
  editMode?: boolean;
  initialData?: Record<string, unknown>;
}

export function CreateProductModal({ isOpen, onClose, onSubmit, editMode = false, initialData }: CreateProductModalProps) {
  const [productType, setProductType] = useState<'Package' | 'Ad-hoc'>('Package');
  const [formData, setFormData] = useState({
    packageName: '',
    unit: 'EACH',
    qtyPerYear: '',
    packagePrice: '',
    addOnPrice: '',
    unitPrice: '',
  });

  const [serviceQuantities, setServiceQuantities] = useState<Record<string, number>>({});
  const [addOnQuantities, setAddOnQuantities] = useState<Record<string, number>>({});

  useEffect(() => {
    if (editMode && initialData) {
      const services = (initialData.services as string[] | undefined) ?? [];
      const isAdHoc =
        initialData.productType === 'Ad-hoc' ||
        (services.length === 0 && Number(initialData.packagePrice ?? 0) > 0 && !Number(initialData.qtyPerYear));
      setProductType(isAdHoc ? 'Ad-hoc' : 'Package');
      setFormData({
        packageName: String(initialData.packageName ?? ''),
        unit: String(initialData.unit ?? 'EACH'),
        qtyPerYear: String(initialData.qtyPerYear ?? ''),
        packagePrice: String(initialData.packagePrice ?? ''),
        addOnPrice: String(initialData.addOnPrice ?? ''),
        unitPrice: String(initialData.unitPrice ?? initialData.packagePrice ?? ''),
      });
      setServiceQuantities((initialData.serviceQuantities as Record<string, number>) || {});
      setAddOnQuantities((initialData.addOnQuantities as Record<string, number>) || {});
    } else {
      setProductType('Package');
      setFormData({
        packageName: '',
        unit: 'EACH',
        qtyPerYear: '',
        packagePrice: '',
        addOnPrice: '',
        unitPrice: '',
      });
      setServiceQuantities({});
      setAddOnQuantities({});
    }
  }, [editMode, initialData, isOpen]);

  const handleServiceQtyChange = (serviceName: string, qty: number) => {
    setServiceQuantities((prev) => {
      const newQty = { ...prev };
      if (qty > 0) {
        newQty[serviceName] = qty;
      } else {
        delete newQty[serviceName];
      }
      return newQty;
    });
  };

  const handleAddOnQtyChange = (addOnName: string, qty: number) => {
    setAddOnQuantities((prev) => {
      const newQty = { ...prev };
      if (qty > 0) {
        newQty[addOnName] = qty;
      } else {
        delete newQty[addOnName];
      }
      return newQty;
    });
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    const baseData = editMode && initialData ? { id: initialData.id } : {};

    try {
      if (productType === 'Ad-hoc') {
        await onSubmit({
          ...baseData,
          productType: 'Ad-hoc',
          packageName: formData.packageName.trim(),
          unit: formData.unit,
          unitPrice: parseFloat(formData.unitPrice) || 0,
        });
      } else {
        const selectedServices = Object.keys(serviceQuantities).filter((key) => serviceQuantities[key] > 0);
        const selectedAddOns = Object.keys(addOnQuantities).filter((key) => addOnQuantities[key] > 0);

        await onSubmit({
          ...baseData,
          productType: 'Package',
          packageName: formData.packageName.trim(),
          unit: formData.unit,
          services: selectedServices,
          serviceQuantities,
          addOns: selectedAddOns,
          addOnQuantities,
          qtyPerYear: parseInt(formData.qtyPerYear, 10) || 0,
          packagePrice: parseFloat(formData.packagePrice) || 0,
          addOnPrice: parseFloat(formData.addOnPrice) || 0,
        });
      }
      onClose();
    } catch {
      // Parent shows the error toast; keep the modal open for corrections.
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
      <div className="bg-card rounded-lg border border-border w-full max-w-3xl max-h-[90vh] overflow-hidden flex flex-col">
        <div className="p-6 border-b border-border flex items-center justify-between">
          <h2>{editMode ? 'Edit Product' : 'Create New Product'}</h2>
          <button
            onClick={onClose}
            className="p-1 hover:bg-muted rounded transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="flex-1 overflow-y-auto">
          <div className="p-6 space-y-6">
            <div>
              <h3 className="mb-4">Type of Product</h3>
              <div className="flex gap-6">
                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="radio"
                    name="productType"
                    checked={productType === 'Package'}
                    onChange={() => setProductType('Package')}
                    className="w-4 h-4 cursor-pointer"
                  />
                  <span>Package</span>
                </label>
                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="radio"
                    name="productType"
                    checked={productType === 'Ad-hoc'}
                    onChange={() => setProductType('Ad-hoc')}
                    className="w-4 h-4 cursor-pointer"
                  />
                  <span>Ad-hoc</span>
                </label>
              </div>
            </div>

            <div className="border-t border-border pt-6">
              <h3 className="mb-4">{productType === 'Ad-hoc' ? 'Product Information' : 'Package Information'}</h3>
              <div className="space-y-4">
                <div>
                  <label className="block mb-2">{productType === 'Ad-hoc' ? 'Name of the Product *' : 'Name of the Package *'}</label>
                  <input
                    type="text"
                    required
                    value={formData.packageName}
                    onChange={(e) => setFormData({ ...formData, packageName: e.target.value })}
                    className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                    placeholder={productType === 'Ad-hoc' ? 'Enter product name' : 'Enter package name'}
                  />
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <label className="block mb-2">Unit *</label>
                    <select
                      required
                      value={formData.unit}
                      onChange={(e) => setFormData({ ...formData, unit: e.target.value })}
                      className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                    >
                      <option value="EACH">EACH</option>
                      <option value="Month">Month</option>
                      <option value="Year">Year</option>
                    </select>
                  </div>

                  {productType === 'Ad-hoc' ? (
                    <div>
                      <label className="block mb-2">Unit Price (MYR) *</label>
                      <input
                        type="number"
                        required
                        min="0"
                        step="0.01"
                        value={formData.unitPrice}
                        onChange={(e) => setFormData({ ...formData, unitPrice: e.target.value })}
                        className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                        placeholder="0.00"
                      />
                    </div>
                  ) : (
                    <div>
                      <label className="block mb-2">QTY/Year *</label>
                      <input
                        type="number"
                        required
                        min="0"
                        value={formData.qtyPerYear}
                        onChange={(e) => setFormData({ ...formData, qtyPerYear: e.target.value })}
                        className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                        placeholder="0"
                      />
                    </div>
                  )}
                </div>

                {productType === 'Package' && (
                  <div>
                    <label className="block mb-2">Package Price (MYR) *</label>
                    <input
                      type="number"
                      required
                      min="0"
                      step="0.01"
                      value={formData.packagePrice}
                      onChange={(e) => setFormData({ ...formData, packagePrice: e.target.value })}
                      className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                      placeholder="0.00"
                    />
                    <p className="text-xs text-muted-foreground mt-1">
                      Includes core services and bundled extras below (Figma catalog price).
                    </p>
                  </div>
                )}
              </div>
            </div>

            {productType === 'Package' && (
            <div className="border-t border-border pt-6">
              <h3 className="mb-4">Services</h3>
              <div className="space-y-2 max-h-64 overflow-y-auto border border-border rounded-lg p-4">
                {availableServices.map((service) => (
                  <div
                    key={service.name}
                    className="flex items-center gap-3 p-2 hover:bg-muted/30 rounded"
                  >
                    <div className="flex-1 flex items-center justify-between">
                      <span>{service.name}</span>
                      <div className="flex items-center gap-3">
                        <span className="text-sm text-muted-foreground">{service.unit}</span>
                        <input
                          type="number"
                          min="0"
                          value={serviceQuantities[service.name] || ''}
                          onChange={(e) => handleServiceQtyChange(service.name, parseInt(e.target.value, 10) || 0)}
                          className="w-20 px-2 py-1 border border-border rounded bg-input-background focus:outline-none focus:ring-2 focus:ring-ring text-sm"
                          placeholder="QTY"
                        />
                      </div>
                    </div>
                  </div>
                ))}
              </div>
              <p className="text-xs text-muted-foreground mt-2">
                Selected: {Object.keys(serviceQuantities).filter((k) => serviceQuantities[k] > 0).length} service(s)
              </p>
            </div>
            )}

            {productType === 'Package' && (
            <div className="border-t border-border pt-6">
              <h3 className="mb-4">Bundled Extras</h3>
              <p className="text-sm text-muted-foreground mb-3">
                Included in the package price (not optional customer add-ons).
              </p>
              <div className="space-y-2 max-h-64 overflow-y-auto border border-border rounded-lg p-4">
                {PRODUCT_BUNDLED_ADD_ONS.map((addOn) => (
                  <div
                    key={addOn.name}
                    className="flex items-center gap-3 p-2 hover:bg-muted/30 rounded"
                  >
                    <div className="flex-1 flex items-center justify-between">
                      <span>{addOn.name}</span>
                      <div className="flex items-center gap-3">
                        <div className="flex items-center gap-2 text-sm text-muted-foreground">
                          <span>{addOn.unit}</span>
                          {addOn.unitPrice != null && (
                            <span>• MYR {addOn.unitPrice.toFixed(2)} ref</span>
                          )}
                        </div>
                        <input
                          type="number"
                          min="0"
                          value={addOnQuantities[addOn.name] || ''}
                          onChange={(e) => handleAddOnQtyChange(addOn.name, parseInt(e.target.value, 10) || 0)}
                          className="w-20 px-2 py-1 border border-border rounded bg-input-background focus:outline-none focus:ring-2 focus:ring-ring text-sm"
                          placeholder="QTY"
                        />
                      </div>
                    </div>
                  </div>
                ))}
              </div>
              <p className="text-xs text-muted-foreground mt-2">
                Selected: {Object.keys(addOnQuantities).filter((k) => addOnQuantities[k] > 0).length} bundled extra(s)
              </p>

              <div className="mt-4">
                <label className="block mb-2">Bundled Extras Value (MYR)</label>
                <input
                  type="number"
                  min="0"
                  step="0.01"
                  value={formData.addOnPrice}
                  onChange={(e) => setFormData({ ...formData, addOnPrice: e.target.value })}
                  className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                  placeholder="0.00"
                />
              </div>
            </div>
            )}
          </div>

          <div className="p-6 border-t border-border flex justify-end gap-3">
            <button
              type="button"
              onClick={onClose}
              className="px-6 py-2 border border-border rounded-lg hover:bg-muted transition-colors"
            >
              Cancel
            </button>
            <button
              type="submit"
              className="px-6 py-2 bg-primary text-primary-foreground rounded-lg hover:bg-primary/90 transition-colors"
            >
              {editMode ? 'Update Product' : 'Create Product'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

export { availableServices };
