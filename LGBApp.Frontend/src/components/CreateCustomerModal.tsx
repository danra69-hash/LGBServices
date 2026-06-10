import { X, Plus, Trash2 } from 'lucide-react';
import { useEffect, useState } from 'react';
import { getDivisionGroups, type CustomerResponse, type DivisionGroupDto, type ProductResponse } from '@/lib/api';
import {
  ADD_ON_CATALOG,
  ADD_ON_UNIT_PRICE,
  type AddOnLine,
  addOnLineSubtotal,
  buildCustomerAddOnLines,
  buildPricingFromProduct,
  computePackageValue,
  inferValidity,
  scaledBasePackagePrice,
} from '@/lib/packagePricing';

interface AccountHolder {
  id: number;
  name: string;
  email: string;
  phone: string;
  moi: boolean;
  moiApproval: boolean;
  moa: boolean;
}

interface PackageRow {
  key: number;
  productId: number | '';
  packageName: string;
  packageDetail: string;
  packageValue: string;
  validity: string;
  purchasedDate: string;
  status: string;
  basePackagePrice: number;
  addOnLines: AddOnLine[];
}

function formatProductDetail(product: ProductResponse): string {
  const lines: string[] = [];
  if (product.services?.length) {
    lines.push(
      'Services: ' +
        product.services
          .map((s) => `${s} (qty ${product.serviceQuantities?.[s] ?? 0})`)
          .join(', '),
    );
  }
  return lines.join('\n');
}

function productBasePrice(product: ProductResponse): number {
  return Number(product.packagePrice ?? 0);
}

function recalcPackageValue(row: PackageRow): PackageRow {
  const value = computePackageValue({
    validity: row.validity,
    basePackagePrice: row.basePackagePrice,
    addOnLines: row.addOnLines,
  });
  return { ...row, packageValue: String(value) };
}

function todayIso(): string {
  return new Date().toISOString().split('T')[0];
}

interface CreateCustomerModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (data: Record<string, unknown>) => Promise<void>;
  editMode?: boolean;
  initialData?: CustomerResponse | null;
  products?: ProductResponse[];
}

const emptyForm = {
  companyName: '',
  divisionGroupCode: '',
  hasLoa: false,
  loaHolders: '' as string,
  moiFormTemplateCode: '',
  moaFormTemplateCode: '',
  cosec: false,
  contactName: '',
  email: '',
  mobile: '',
  invoiceBy: '',
  chargeTo: '',
  status: 'Active' as 'Active' | 'Non-Active',
};

const defaultPackage = (): PackageRow => ({
  key: Date.now(),
  productId: '',
  packageName: '',
  packageDetail: '',
  packageValue: '',
  validity: '1 Year',
  purchasedDate: todayIso(),
  status: 'Active',
  basePackagePrice: 0,
  addOnLines: buildCustomerAddOnLines(),
});

export function CreateCustomerModal({
  isOpen,
  onClose,
  onSubmit,
  editMode = false,
  initialData = null,
  products = [],
}: CreateCustomerModalProps) {
  const [formData, setFormData] = useState(emptyForm);
  const [packages, setPackages] = useState<PackageRow[]>([defaultPackage()]);
  const [accountHolders, setAccountHolders] = useState<AccountHolder[]>([
    { id: 1, name: '', email: '', phone: '', moi: false, moiApproval: false, moa: false },
  ]);
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState('');
  const [divisionGroups, setDivisionGroups] = useState<DivisionGroupDto[]>([]);

  useEffect(() => {
    if (!isOpen) return;
    void getDivisionGroups().then(setDivisionGroups).catch(() => setDivisionGroups([]));

    if (editMode && initialData) {
      setFormData({
        companyName: initialData.company,
        divisionGroupCode: initialData.divisionGroupCode ?? '',
        hasLoa: initialData.hasLoa ?? false,
        loaHolders: (initialData.loaHolders ?? []).join(', '),
        moiFormTemplateCode: initialData.moiFormTemplateCode ?? '',
        moaFormTemplateCode: initialData.moaFormTemplateCode ?? '',
        cosec: initialData.cosec,
        contactName: initialData.name,
        email: initialData.email,
        mobile: initialData.phone,
        invoiceBy: initialData.invoiceBy,
        chargeTo: initialData.chargeTo,
        status: initialData.status,
      });
      const pkgList =
        initialData.packages?.length > 0
          ? initialData.packages.map((p) => {
              const matched = products.find((pr) => pr.packageName === p.packageName);
              const validity =
                p.validity || inferValidity(p.purchasedDate, p.expiryDate);
              const pricing = p.pricing;
              const addOnLines = buildCustomerAddOnLines(pricing?.addOnLines);
              const basePackagePrice =
                pricing?.basePackagePrice ?? Number(matched?.packagePrice ?? p.packageValue ?? 0);

              return recalcPackageValue({
                key: p.id,
                productId: matched?.id ?? '',
                packageName: p.packageName,
                packageDetail: p.packageDetail ?? (matched ? formatProductDetail(matched) : ''),
                packageValue: String(p.packageValue),
                validity,
                purchasedDate: p.purchasedDate || todayIso(),
                status: p.status,
                basePackagePrice,
                addOnLines,
              });
            })
          : [
              recalcPackageValue({
                key: 1,
                productId: products.find((pr) => pr.packageName === initialData.package)?.id ?? '',
                packageName: initialData.package,
                packageDetail: '',
                packageValue: String(initialData.packageValue),
                validity: inferValidity(initialData.purchasedDate, initialData.expiryDate),
                purchasedDate: initialData.purchasedDate || todayIso(),
                status: 'Active',
                basePackagePrice: Number(initialData.packageValue ?? 0),
                addOnLines: buildCustomerAddOnLines(),
              }),
            ];
      setPackages(pkgList);
      const moiSet = new Set(initialData.moi ?? []);
      const moiApprovalSet = new Set(initialData.moiApproval ?? []);
      const moaSet = new Set(initialData.moa ?? []);
      setAccountHolders(
        initialData.accountHolders.length > 0
          ? initialData.accountHolders.map((h) => ({
              id: h.id,
              name: h.name,
              email: h.email,
              phone: h.phone,
              moi: moiSet.has(h.name),
              moiApproval: moiApprovalSet.has(h.name),
              moa: moaSet.has(h.name),
            }))
          : [{ id: 1, name: '', email: '', phone: '', moi: false, moiApproval: false, moa: false }],
      );
    } else {
      setFormData(emptyForm);
      setPackages([defaultPackage()]);
      setAccountHolders([{ id: 1, name: '', email: '', phone: '', moi: false, moiApproval: false, moa: false }]);
    }
    setSubmitError('');
  }, [isOpen, editMode, initialData, products]);

  const handleAddAccountHolder = () => {
    const newId = Math.max(...accountHolders.map((h) => h.id), 0) + 1;
    setAccountHolders([
      ...accountHolders,
      { id: newId, name: '', email: '', phone: '', moi: false, moiApproval: false, moa: false },
    ]);
  };

  const handleRemoveAccountHolder = (id: number) => {
    if (accountHolders.length > 1) {
      setAccountHolders(accountHolders.filter((h) => h.id !== id));
    }
  };

  const handleAccountHolderChange = (id: number, field: string, value: string | boolean) => {
    setAccountHolders(accountHolders.map((h) => (h.id === id ? { ...h, [field]: value } : h)));
  };

  const handleAddPackage = () => {
    setPackages([...packages, defaultPackage()]);
  };

  const handleRemovePackage = (key: number) => {
    if (packages.length > 1) {
      setPackages(packages.filter((p) => p.key !== key));
    }
  };

  const updatePackage = (key: number, updater: (row: PackageRow) => PackageRow) => {
    setPackages(packages.map((p) => (p.key === key ? updater(p) : p)));
  };

  const handlePackageChange = (key: number, field: keyof PackageRow, value: string) => {
    updatePackage(key, (row) => {
      const next = { ...row, [field]: value };
      if (field === 'validity') {
        return recalcPackageValue(next);
      }
      return next;
    });
  };

  const handleAddOnQtyChange = (key: number, addOnName: string, raw: string) => {
    updatePackage(key, (row) => {
      const addOnLines = row.addOnLines.map((line) =>
        line.name === addOnName
          ? { ...line, qty: Math.max(0, parseInt(raw, 10) || 0), unitPrice: ADD_ON_UNIT_PRICE }
          : line,
      );
      return recalcPackageValue({ ...row, addOnLines });
    });
  };

  const handleProductSelect = (key: number, productId: string) => {
    if (!productId) {
      updatePackage(key, (row) => ({
        ...row,
        productId: '',
        packageName: '',
        packageDetail: '',
        packageValue: '',
        basePackagePrice: 0,
        addOnLines: buildCustomerAddOnLines(),
      }));
      return;
    }
    const product = products.find((pr) => pr.id === Number(productId));
    if (!product) return;
    updatePackage(key, (row) => {
      const pricing = buildPricingFromProduct(product, row.validity);
      return recalcPackageValue({
        ...row,
        productId: product.id,
        packageName: product.packageName,
        packageDetail: formatProductDetail(product),
        basePackagePrice: pricing.basePackagePrice,
        addOnLines: buildCustomerAddOnLines(row.addOnLines),
      });
    });
  };

  const resetForm = () => {
    setFormData(emptyForm);
    setPackages([defaultPackage()]);
    setAccountHolders([{ id: 1, name: '', email: '', phone: '', moi: false, moiApproval: false, moa: false }]);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    setSubmitError('');
    const dateCreated = new Date().toISOString().split('T')[0];
    try {
      await onSubmit({
        ...formData,
        packages,
        accountHolders,
        dateCreated,
        customerId: editMode ? initialData?.id : undefined,
      });
      onClose();
      resetForm();
    } catch (err) {
      setSubmitError(err instanceof Error ? err.message : 'Failed to save customer.');
    } finally {
      setSubmitting(false);
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
      <div className="bg-card rounded-lg border border-border w-full max-w-4xl max-h-[90vh] overflow-hidden flex flex-col">
        <div className="p-6 border-b border-border flex items-center justify-between">
          <h2>{editMode ? 'Edit Customer' : 'Create New Customer'}</h2>
          <button type="button" onClick={onClose} className="p-1 hover:bg-muted rounded transition-colors">
            <X className="w-5 h-5" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="flex-1 overflow-y-auto">
          <div className="p-6 space-y-8">
            <div>
              <h3 className="mb-4">Customer Detail</h3>
              <div className="space-y-4">
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <label className="block mb-2">Company Name *</label>
                    <input
                      type="text"
                      required
                      value={formData.companyName}
                      onChange={(e) => setFormData({ ...formData, companyName: e.target.value })}
                      className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                    />
                  </div>
                  <div>
                    <label className="block mb-2">Contact Name *</label>
                    <input
                      type="text"
                      required
                      value={formData.contactName}
                      onChange={(e) => setFormData({ ...formData, contactName: e.target.value })}
                      className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                    />
                  </div>
                </div>

                {editMode && (
                  <div>
                    <label className="block mb-2">Status</label>
                    <select
                      value={formData.status}
                      onChange={(e) =>
                        setFormData({ ...formData, status: e.target.value as 'Active' | 'Non-Active' })
                      }
                      className="w-full px-3 py-2 border border-border rounded-lg bg-input-background"
                    >
                      <option value="Active">Active</option>
                      <option value="Non-Active">Non-Active</option>
                    </select>
                  </div>
                )}

                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={formData.cosec}
                    onChange={(e) => setFormData({ ...formData, cosec: e.target.checked })}
                    className="w-4 h-4"
                  />
                  <span>COSEC</span>
                </label>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <label className="block mb-2">Division group</label>
                    <select
                      value={formData.divisionGroupCode}
                      onChange={(e) => setFormData({ ...formData, divisionGroupCode: e.target.value })}
                      className="w-full px-3 py-2 border border-border rounded-lg bg-input-background"
                    >
                      <option value="">Select division group</option>
                      {divisionGroups.map((g) => (
                        <option key={g.code} value={g.code}>{g.name}</option>
                      ))}
                    </select>
                  </div>
                  <div className="flex items-end">
                    <label className="flex items-center gap-2 cursor-pointer pb-2">
                      <input
                        type="checkbox"
                        checked={formData.hasLoa}
                        onChange={(e) => setFormData({ ...formData, hasLoa: e.target.checked })}
                        className="w-4 h-4"
                      />
                      <span>Company has LOA</span>
                    </label>
                  </div>
                </div>

                {formData.hasLoa && (
                  <div>
                    <label className="block mb-2">LOA holders (comma-separated)</label>
                    <input
                      type="text"
                      value={formData.loaHolders}
                      onChange={(e) => setFormData({ ...formData, loaHolders: e.target.value })}
                      className="w-full px-3 py-2 border border-border rounded-lg bg-input-background"
                      placeholder="e.g. John Doe, Jane Smith"
                    />
                  </div>
                )}

                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <label className="block mb-2">Email *</label>
                    <input
                      type="email"
                      required
                      value={formData.email}
                      onChange={(e) => setFormData({ ...formData, email: e.target.value })}
                      className="w-full px-3 py-2 border border-border rounded-lg bg-input-background"
                    />
                  </div>
                  <div>
                    <label className="block mb-2">Mobile *</label>
                    <input
                      type="tel"
                      required
                      value={formData.mobile}
                      onChange={(e) => setFormData({ ...formData, mobile: e.target.value })}
                      className="w-full px-3 py-2 border border-border rounded-lg bg-input-background"
                    />
                  </div>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <label className="block mb-2">Invoice By *</label>
                    <input
                      type="text"
                      required
                      value={formData.invoiceBy}
                      onChange={(e) => setFormData({ ...formData, invoiceBy: e.target.value })}
                      className="w-full px-3 py-2 border border-border rounded-lg bg-input-background"
                    />
                  </div>
                  <div>
                    <label className="block mb-2">Charge To *</label>
                    <input
                      type="text"
                      required
                      value={formData.chargeTo}
                      onChange={(e) => setFormData({ ...formData, chargeTo: e.target.value })}
                      className="w-full px-3 py-2 border border-border rounded-lg bg-input-background"
                    />
                  </div>
                </div>

                <div>
                  <div className="flex items-center justify-between mb-1">
                    <label>Account Holders (client directors / signers)</label>
                    <button
                      type="button"
                      onClick={handleAddAccountHolder}
                      className="flex items-center gap-1 px-3 py-1 text-sm bg-primary text-primary-foreground rounded-lg"
                    >
                      <Plus className="w-4 h-4" />
                      Add Holder
                    </button>
                  </div>
                  <p className="text-sm text-muted-foreground mb-3">
                    These are people at the client company (not your staff). Tick which forms each
                    person must receive — your users will process them per package.
                  </p>
                  <div className="space-y-3">
                    {accountHolders.map((holder, index) => (
                      <div key={holder.id} className="bg-muted/30 rounded-lg p-4 space-y-3">
                        <div className="flex items-center justify-between">
                          <span className="text-sm font-medium">Signer {index + 1}</span>
                          {accountHolders.length > 1 && (
                            <button
                              type="button"
                              onClick={() => handleRemoveAccountHolder(holder.id)}
                              className="p-1 text-destructive hover:bg-destructive/10 rounded"
                            >
                              <Trash2 className="w-4 h-4" />
                            </button>
                          )}
                        </div>
                        <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
                          <input
                            type="text"
                            placeholder="Name"
                            value={holder.name}
                            onChange={(e) => handleAccountHolderChange(holder.id, 'name', e.target.value)}
                            className="px-3 py-2 border border-border rounded-lg bg-input-background"
                          />
                          <input
                            type="email"
                            placeholder="Email"
                            value={holder.email}
                            onChange={(e) => handleAccountHolderChange(holder.id, 'email', e.target.value)}
                            className="px-3 py-2 border border-border rounded-lg bg-input-background"
                          />
                          <input
                            type="tel"
                            placeholder="Mobile"
                            value={holder.phone}
                            onChange={(e) => handleAccountHolderChange(holder.id, 'phone', e.target.value)}
                            className="px-3 py-2 border border-border rounded-lg bg-input-background"
                          />
                        </div>
                        <div className="flex gap-4">
                          <label className="flex items-center gap-2">
                            <input
                              type="checkbox"
                              checked={holder.moi}
                              onChange={(e) => handleAccountHolderChange(holder.id, 'moi', e.target.checked)}
                            />
                            <span className="text-sm">Needs MOI</span>
                          </label>
                          <label className="flex items-center gap-2">
                            <input
                              type="checkbox"
                              checked={holder.moiApproval}
                              onChange={(e) => handleAccountHolderChange(holder.id, 'moiApproval', e.target.checked)}
                            />
                            <span className="text-sm">Needs MOI Approval</span>
                          </label>
                          <label className="flex items-center gap-2">
                            <input
                              type="checkbox"
                              checked={holder.moa}
                              onChange={(e) => handleAccountHolderChange(holder.id, 'moa', e.target.checked)}
                            />
                            <span className="text-sm">Needs MOA</span>
                          </label>
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              </div>
            </div>

            <div className="border-t border-border pt-6">
              <div className="flex items-center justify-between mb-4">
                <h3>Packages</h3>
                <button
                  type="button"
                  onClick={handleAddPackage}
                  className="flex items-center gap-1 px-3 py-1.5 text-sm border border-border rounded-lg hover:bg-muted"
                >
                  <Plus className="w-4 h-4" />
                  Add Package
                </button>
              </div>
              <p className="text-sm text-muted-foreground mb-4">
                Pick a package from Products (fixed catalog price and included services). Optional
                add-ons below are extra services the customer can purchase on top — not part of the
                package definition.
              </p>
              {products.length === 0 && (
                <p className="text-sm text-amber-700 bg-amber-50 border border-amber-200 rounded-lg px-3 py-2 mb-4">
                  No products in the catalog yet. Add products under the Products tab first.
                </p>
              )}
              <div className="space-y-4">
                {packages.map((pkg, index) => (
                  <div key={pkg.key} className="bg-muted/30 rounded-lg p-4 space-y-3">
                    <div className="flex items-center justify-between">
                      <span className="font-medium">Package {index + 1}</span>
                      {packages.length > 1 && (
                        <button
                          type="button"
                          onClick={() => handleRemovePackage(pkg.key)}
                          className="p-1 text-destructive hover:bg-destructive/10 rounded"
                        >
                          <Trash2 className="w-4 h-4" />
                        </button>
                      )}
                    </div>
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                      <div>
                        <label className="block text-xs text-muted-foreground mb-1">Product package *</label>
                        <select
                          required
                          value={pkg.productId === '' ? '' : String(pkg.productId)}
                          onChange={(e) => handleProductSelect(pkg.key, e.target.value)}
                          className="w-full px-3 py-2 border border-border rounded-lg bg-input-background"
                        >
                          <option value="">Select a product</option>
                          {products.map((product) => (
                            <option key={product.id} value={product.id}>
                              {product.packageName} — MYR {productBasePrice(product).toLocaleString()}/yr
                            </option>
                          ))}
                        </select>
                      </div>
                      <div>
                        <label className="block text-xs text-muted-foreground mb-1">Total (MYR)</label>
                        <div className="w-full px-3 py-2 border border-border rounded-lg bg-muted/40 text-sm">
                          {pkg.productId === ''
                            ? '—'
                            : Number(pkg.packageValue || 0).toLocaleString('en-MY', {
                                minimumFractionDigits: 2,
                              })}
                        </div>
                      </div>
                    </div>
                    {pkg.productId !== '' && (
                      <p className="text-xs text-muted-foreground">
                        Package MYR{' '}
                        {scaledBasePackagePrice(pkg.basePackagePrice, pkg.validity).toLocaleString(
                          'en-MY',
                          { minimumFractionDigits: 2 },
                        )}{' '}
                        ({pkg.validity}) + optional add-ons MYR{' '}
                        {pkg.addOnLines
                          .reduce((sum, line) => sum + addOnLineSubtotal(line), 0)
                          .toLocaleString('en-MY', { minimumFractionDigits: 2 })}
                      </p>
                    )}
                    <textarea
                      rows={2}
                      placeholder="Package detail (optional)"
                      value={pkg.packageDetail}
                      onChange={(e) => handlePackageChange(pkg.key, 'packageDetail', e.target.value)}
                      className="w-full px-3 py-2 border border-border rounded-lg bg-input-background resize-none"
                    />
                    {pkg.productId !== '' && (
                      <div className="border border-border rounded-lg overflow-hidden">
                        <div className="px-3 py-2 bg-muted/50 text-xs font-medium text-muted-foreground">
                          Optional add-ons — MYR {ADD_ON_UNIT_PRICE.toLocaleString()} per unit (flat, not
                          scaled by validity)
                        </div>
                        <div className="divide-y divide-border">
                          {pkg.addOnLines.map((line) => {
                            const catalog = ADD_ON_CATALOG.find((item) => item.name === line.name);
                            return (
                              <div
                                key={line.name}
                                className="grid grid-cols-1 md:grid-cols-3 gap-2 px-3 py-2 text-sm items-center"
                              >
                                <div>
                                  <span className="truncate block">{line.name}</span>
                                  {catalog && (
                                    <span className="text-xs text-muted-foreground">{catalog.unit}</span>
                                  )}
                                </div>
                                <label className="flex items-center gap-2">
                                  <span className="text-xs text-muted-foreground w-8">Qty</span>
                                  <input
                                    type="number"
                                    min="0"
                                    value={line.qty}
                                    onChange={(e) =>
                                      handleAddOnQtyChange(pkg.key, line.name, e.target.value)
                                    }
                                    className="w-full px-2 py-1 border border-border rounded bg-input-background"
                                  />
                                </label>
                                <span className="text-muted-foreground text-right md:text-left">
                                  MYR {addOnLineSubtotal(line).toLocaleString('en-MY', { minimumFractionDigits: 2 })}
                                </span>
                              </div>
                            );
                          })}
                        </div>
                      </div>
                    )}
                    <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
                      <div>
                        <label className="block text-xs text-muted-foreground mb-1">Validity</label>
                        <select
                          value={pkg.validity}
                          onChange={(e) => handlePackageChange(pkg.key, 'validity', e.target.value)}
                          className="w-full px-3 py-2 border border-border rounded-lg bg-input-background"
                        >
                          <option value="6 Months">6 Months</option>
                          <option value="1 Year">1 Year</option>
                          <option value="2 Years">2 Years</option>
                          <option value="3 Years">3 Years</option>
                        </select>
                      </div>
                      <div>
                        <label className="block text-xs text-muted-foreground mb-1">Start date</label>
                        <input
                          type="date"
                          value={pkg.purchasedDate}
                          onChange={(e) => handlePackageChange(pkg.key, 'purchasedDate', e.target.value)}
                          className="w-full px-3 py-2 border border-border rounded-lg bg-input-background"
                        />
                      </div>
                      {editMode ? (
                        <div>
                          <label className="block text-xs text-muted-foreground mb-1">Status</label>
                          <select
                            value={pkg.status}
                            onChange={(e) => handlePackageChange(pkg.key, 'status', e.target.value)}
                            className="w-full px-3 py-2 border border-border rounded-lg bg-input-background"
                          >
                            <option value="Active">Active</option>
                            <option value="Expired">Expired</option>
                            <option value="Cancelled">Cancelled</option>
                          </select>
                        </div>
                      ) : (
                        <div className="flex items-end text-xs text-muted-foreground pb-2">
                          Catalog package MYR {pkg.basePackagePrice.toLocaleString()}/yr
                        </div>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          </div>

          {submitError && <div className="px-6 pb-0 text-sm text-destructive">{submitError}</div>}

          <div className="p-6 border-t border-border flex justify-end gap-3">
            <button type="button" onClick={onClose} disabled={submitting} className="px-6 py-2 border rounded-lg">
              Cancel
            </button>
            <button
              type="submit"
              disabled={submitting}
              className="px-6 py-2 bg-primary text-primary-foreground rounded-lg disabled:opacity-50"
            >
              {submitting ? 'Saving...' : editMode ? 'Save Changes' : 'Create Customer'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
