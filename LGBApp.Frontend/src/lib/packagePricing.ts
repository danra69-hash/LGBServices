import type { ProductResponse } from './api';

export interface AddOnLine {
  name: string;
  qty: number;
  unitPrice: number;
}

export interface PackagePricing {
  validity: string;
  basePackagePrice: number;
  addOnLines: AddOnLine[];
}

/** Fixed MYR price per add-on unit — configured on customers only, not on products. */
export const ADD_ON_UNIT_PRICE = 120;

/** Non-COSEC customers may purchase add-ons without a base product package. */
export const ADDONS_ONLY_PACKAGE_NAME = 'Add-ons only';

export function isAddonsOnlyPackageName(name: string): boolean {
  return name.trim().toLowerCase() === ADDONS_ONLY_PACKAGE_NAME.toLowerCase();
}

/** Optional services a customer may purchase on top of a package (not core package services). */
export const ADD_ON_CATALOG: { name: string; unit: string }[] = [
  { name: 'Overseas Support Service', unit: 'Month' },
  { name: 'Local Support Service', unit: 'Each' },
  { name: 'Attend Board Meeting', unit: '2hrs/Each' },
  { name: 'Prepare board meeting Minutes', unit: 'Per meeting' },
  { name: 'Lodgement fee to MBSR Audited A/C', unit: 'Each' },
  { name: 'Lodgement fee for MBRS annual return', unit: 'Each' },
];

export function validityFactor(validity: string): number {
  const match = validity.match(/(\d+)/);
  const amount = match ? parseInt(match[1], 10) : 1;
  if (validity.toLowerCase().includes('month')) {
    return amount / 12;
  }
  return amount;
}

export function inferValidity(purchasedDate: string, expiryDate: string): string {
  const start = new Date(purchasedDate);
  const end = new Date(expiryDate);
  if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime())) {
    return '1 Year';
  }
  const months = Math.round((end.getTime() - start.getTime()) / (1000 * 60 * 60 * 24 * 30.44));
  if (months >= 30) {
    const years = Math.round(months / 12);
    if (years <= 1) return '1 Year';
    return `${years} Years`;
  }
  if (months >= 5) return '6 Months';
  return '1 Year';
}

export function buildCustomerAddOnLines(existing?: AddOnLine[]): AddOnLine[] {
  const existingByName = new Map((existing ?? []).map((l) => [l.name, l]));
  return ADD_ON_CATALOG.map((item) => {
    const prev = existingByName.get(item.name);
    return {
      name: item.name,
      qty: prev?.qty ?? 0,
      unitPrice: ADD_ON_UNIT_PRICE,
    };
  });
}

export function addOnLineSubtotal(line: AddOnLine): number {
  return line.qty > 0 ? line.qty * ADD_ON_UNIT_PRICE : 0;
}

/** Package base scales with validity; optional customer add-ons are a flat MYR 120 × qty. */
export function computePackageValue(pricing: PackagePricing): number {
  const factor = validityFactor(pricing.validity);
  const scaledBase = pricing.basePackagePrice * factor;
  const addOnTotal = pricing.addOnLines.reduce((sum, line) => sum + addOnLineSubtotal(line), 0);
  return Math.round((scaledBase + addOnTotal) * 100) / 100;
}

export function scaledBasePackagePrice(basePackagePrice: number, validity: string): number {
  return Math.round(basePackagePrice * validityFactor(validity) * 100) / 100;
}

export function buildPricingFromProduct(
  product: ProductResponse,
  validity: string,
  existing?: Partial<PackagePricing>,
): PackagePricing {
  return {
    validity,
    basePackagePrice: existing?.basePackagePrice ?? Number(product.packagePrice ?? 0),
    addOnLines: existing?.addOnLines?.length
      ? existing.addOnLines
      : buildCustomerAddOnLines(),
  };
}
