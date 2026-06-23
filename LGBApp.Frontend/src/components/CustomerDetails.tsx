import { Mail, Phone, Building2, DollarSign, X, Package, Users, Edit, Trash2 } from 'lucide-react';
import {
  type CustomerPackageDto,
  type CustomerResponse,
  type ProductResponse,
} from '@/lib/api';
import {
  ADD_ON_UNIT_PRICE,
  addOnLineSubtotal,
  scaledBasePackagePrice,
} from '@/lib/packagePricing';

type Customer = CustomerResponse;

interface CustomerDetailsProps {
  customer: Customer | null;
  products?: ProductResponse[];
  onClose: () => void;
  onEdit: (customer: Customer) => void;
  onDelete: (customer: Customer) => void;
  onOpenTracking?: (customer: Customer) => void;
  onOpenPackageWork?: (customer: Customer, pkg: CustomerPackageDto) => void;
}

export function CustomerDetails({ customer, products = [], onClose, onEdit, onDelete, onOpenTracking, onOpenPackageWork }: CustomerDetailsProps) {
  if (!customer) return null;

  return (
    <div className="bg-card rounded-lg border border-border overflow-hidden">
      <div className="p-4 border-b border-border flex items-center justify-between">
        <h2>Customer Details</h2>
        <div className="flex items-center gap-1">
          <button
            type="button"
            onClick={() => onEdit(customer)}
            className="p-1 hover:bg-muted rounded transition-colors"
            title="Edit customer"
          >
            <Edit className="w-5 h-5" />
          </button>
          <button
            type="button"
            onClick={() => onDelete(customer)}
            className="p-1 hover:bg-destructive/10 text-destructive rounded transition-colors"
            title="Delete customer"
          >
            <Trash2 className="w-5 h-5" />
          </button>
          <button
            type="button"
            onClick={onClose}
            className="p-1 hover:bg-muted rounded transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
        </div>
      </div>

      <div className="p-6">
        <div className="mb-6">
          <h3 className="mb-1">{customer.company}</h3>
          <p className="text-sm text-muted-foreground mb-4">{customer.name}</p>
          <div className="space-y-3">
            <div className="flex items-center gap-3">
              <Building2 className="w-5 h-5 text-muted-foreground" />
              <span>{customer.company}</span>
            </div>
            <div className="flex items-center gap-3">
              <Mail className="w-5 h-5 text-muted-foreground" />
              <a href={`mailto:${customer.email}`} className="text-primary hover:underline">
                {customer.email}
              </a>
            </div>
            <div className="flex items-center gap-3">
              <Phone className="w-5 h-5 text-muted-foreground" />
              <a href={`tel:${customer.phone}`} className="text-primary hover:underline">
                {customer.phone}
              </a>
            </div>
            <div className="flex items-center gap-3">
              <DollarSign className="w-5 h-5 text-muted-foreground" />
              <span>Active Value: MYR {(customer.value || 0).toLocaleString()}</span>
            </div>
          </div>
        </div>

        <div className="border-t border-border pt-6 mb-6">
          <h3 className="mb-4">Account Holders</h3>
          <p className="text-xs text-muted-foreground mb-3">
            Contacts flagged for MOI/MOA get a signatory login (scoped to this company). Client-added signatories are marked below.
          </p>
          <div className="space-y-3">
            {(customer.accountHolders || []).map((holder) => {
              const isMOI = (customer.moi || []).includes(holder.name);
              const isMOIApproval = (customer.moiApproval || []).includes(holder.name);
              const isMOA = (customer.moa || []).includes(holder.name);
              return (
                <div key={holder.id} className="bg-muted/30 rounded-lg p-3">
                  <div className="flex items-center justify-between mb-2">
                    <div className="flex items-center gap-2 flex-wrap">
                      <Users className="w-4 h-4 text-muted-foreground" />
                      <span className="font-medium">{holder.name}</span>
                      {holder.userId && (
                        <span className="text-xs px-2 py-0.5 rounded-full bg-green-100 text-green-800">Account #{holder.userId}</span>
                      )}
                      {holder.clientAdded && (
                        <span className="text-xs px-2 py-0.5 rounded-full bg-amber-100 text-amber-900">Client-added</span>
                      )}
                    </div>
                    <div className="flex items-center gap-4">
                      <label className="flex items-center gap-2 cursor-pointer">
                        <input
                          type="checkbox"
                          checked={isMOI}
                          readOnly
                          className="w-4 h-4 cursor-pointer"
                          onClick={(e) => e.stopPropagation()}
                        />
                        <span className="text-sm">MOI</span>
                      </label>
                      <label className="flex items-center gap-2 cursor-pointer">
                        <input
                          type="checkbox"
                          checked={isMOIApproval}
                          readOnly
                          className="w-4 h-4 cursor-pointer"
                          onClick={(e) => e.stopPropagation()}
                        />
                        <span className="text-sm">MOI Approval</span>
                      </label>
                      <label className="flex items-center gap-2 cursor-pointer">
                        <input
                          type="checkbox"
                          checked={isMOA}
                          readOnly
                          className="w-4 h-4 cursor-pointer"
                          onClick={(e) => e.stopPropagation()}
                        />
                        <span className="text-sm">MOA</span>
                      </label>
                    </div>
                  </div>
                  {isMOI && (
                    <div className="mt-2 pt-2 border-t border-border text-xs">
                      {isMOIApproval ? (
                        <span className="text-green-700 font-medium">MOI approved</span>
                      ) : (
                        <span className="text-muted-foreground">MOI pending — complete via package workboard</span>
                      )}
                    </div>
                  )}
                  <div className="space-y-1 text-sm text-muted-foreground">
                    <div className="flex items-center gap-2">
                      <Mail className="w-3 h-3" />
                      {holder.email}
                    </div>
                    <div className="flex items-center gap-2">
                      <Phone className="w-3 h-3" />
                      {holder.phone}
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        </div>

        <div className="border-t border-border pt-6 mb-6">
          <div className="flex items-center justify-between mb-4">
            <h3>
            Packages
            {(customer.packages?.length ?? 0) > 1 && (
              <span className="ml-2 text-sm font-normal text-muted-foreground">
                ({customer.packages.length} total)
              </span>
            )}
            </h3>
            {onOpenTracking && (
              <button
                type="button"
                onClick={() => onOpenTracking(customer)}
                className="text-sm px-3 py-1.5 border border-border rounded-lg hover:bg-muted"
              >
                Open Tracking
              </button>
            )}
          </div>
          <div className="space-y-3">
            {(customer.packages?.length ? customer.packages : [{
              id: 0,
              packageName: customer.package,
              packageValue: customer.packageValue,
              purchasedDate: customer.purchasedDate,
              expiryDate: customer.expiryDate,
              status: 'Active',
            }]).map((pkg) => {
              const catalogProduct = products.find((p) => p.packageName === pkg.packageName);
              return (
              <div key={pkg.id} className="bg-muted/30 rounded-lg p-4 space-y-2">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <Package className="w-4 h-4 text-muted-foreground" />
                    <span className="font-medium">{pkg.packageName}</span>
                    {catalogProduct && (
                      <span className="text-xs text-muted-foreground">(from Products)</span>
                    )}
                  </div>
                  <span className={`px-2 py-0.5 rounded-full text-xs ${
                    pkg.status === 'Active' ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-800'
                  }`}>
                    {pkg.status}
                  </span>
                </div>
                <div className="flex justify-between text-sm">
                  <span className="text-muted-foreground">Total value</span>
                  <span>MYR {(pkg.packageValue || 0).toLocaleString()}</span>
                </div>
                {pkg.pricing?.basePackagePrice != null && pkg.validity && (
                  <div className="flex justify-between text-sm text-muted-foreground">
                    <span>Package ({pkg.validity})</span>
                    <span>
                      MYR{' '}
                      {scaledBasePackagePrice(
                        pkg.pricing.basePackagePrice,
                        pkg.validity,
                      ).toLocaleString()}
                    </span>
                  </div>
                )}
                <div className="flex justify-between text-sm">
                  <span className="text-muted-foreground">Active value</span>
                  <span className={pkg.status === 'Active' ? 'text-green-700 font-medium' : ''}>
                    MYR {(pkg.activeValue ?? 0).toLocaleString('en-MY', { minimumFractionDigits: 2 })}
                  </span>
                </div>
                {'validity' in pkg && pkg.validity && (
                  <div className="flex justify-between text-sm">
                    <span className="text-muted-foreground">Validity</span>
                    <span>{pkg.validity}</span>
                  </div>
                )}
                {'pricing' in pkg &&
                  pkg.pricing?.addOnLines?.some((line) => line.qty > 0) && (
                  <div className="pt-2 border-t border-border text-sm space-y-1">
                    <span className="text-muted-foreground">
                      Optional add-ons (MYR {ADD_ON_UNIT_PRICE}/unit)
                    </span>
                    {pkg.pricing.addOnLines
                      .filter((line) => line.qty > 0)
                      .map((line) => (
                        <div key={line.name} className="flex justify-between text-muted-foreground">
                          <span>
                            {line.name} × {line.qty}
                          </span>
                          <span>
                            MYR {addOnLineSubtotal(line).toLocaleString('en-MY', { minimumFractionDigits: 2 })}
                          </span>
                        </div>
                      ))}
                  </div>
                )}
                <div className="flex justify-between text-sm">
                  <span className="text-muted-foreground">Purchased</span>
                  <span>{pkg.purchasedDate || 'N/A'}</span>
                </div>
                <div className="flex justify-between text-sm">
                  <span className="text-muted-foreground">Expires</span>
                  <span>{pkg.expiryDate || 'N/A'}</span>
                </div>
                {catalogProduct && catalogProduct.services?.length > 0 && (
                  <div className="pt-2 border-t border-border text-sm">
                    <span className="text-muted-foreground">Included services</span>
                    <ul className="mt-1 space-y-0.5">
                      {catalogProduct.services.map((service) => (
                        <li key={service} className="text-muted-foreground">
                          • {service}{' '}
                          <span className="text-foreground">
                            (qty {catalogProduct.serviceQuantities?.[service] ?? 0})
                          </span>
                        </li>
                      ))}
                    </ul>
                  </div>
                )}
                {catalogProduct && catalogProduct.addOns?.length > 0 && (
                  <div className="pt-2 border-t border-border text-sm">
                    <span className="text-muted-foreground">Bundled extras (in package)</span>
                    <ul className="mt-1 space-y-0.5">
                      {catalogProduct.addOns.map((addOn) => (
                        <li key={addOn} className="text-muted-foreground">
                          • {addOn}{' '}
                          <span className="text-foreground">
                            (qty {catalogProduct.addOnQuantities?.[addOn] ?? 0})
                          </span>
                        </li>
                      ))}
                    </ul>
                  </div>
                )}
                {pkg.packageDetail && !catalogProduct && (
                  <p className="text-sm text-muted-foreground whitespace-pre-line">{pkg.packageDetail}</p>
                )}
                {onOpenPackageWork && pkg.id > 0 && (
                  <button
                    type="button"
                    onClick={() => onOpenPackageWork(customer, pkg as CustomerPackageDto)}
                    className="mt-2 w-full px-3 py-2 text-sm bg-primary text-primary-foreground rounded-lg hover:bg-primary/90"
                  >
                    Manage package
                  </button>
                )}
              </div>
            );
            })}
          </div>
          <div className="mt-3 flex justify-between text-sm font-medium">
            <span>Total active value</span>
            <span>MYR {(customer.value || 0).toLocaleString()}</span>
          </div>
          <div className="mt-4 space-y-2">
            <div className="flex items-center justify-between text-sm">
              <span className="text-muted-foreground">MOI (Memorandum of Incorporation)</span>
              <span className="font-medium">{(customer.moi || []).join(', ') || 'None'}</span>
            </div>
            <div className="flex items-center justify-between text-sm">
              <span className="text-muted-foreground">MOI Approval</span>
              <span className="font-medium">{(customer.moiApproval || []).join(', ') || 'None'}</span>
            </div>
            <div className="flex items-center justify-between text-sm">
              <span className="text-muted-foreground">MOA (Memorandum of Association)</span>
              <span className="font-medium">{(customer.moa || []).join(', ') || 'None'}</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
