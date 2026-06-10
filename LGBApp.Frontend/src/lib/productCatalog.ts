/** Bundled extras included in a product package (Figma catalog). */
export interface ProductBundledAddOn {
  name: string;
  unit: string;
  unitPrice?: number;
}

export const PRODUCT_BUNDLED_ADD_ONS: ProductBundledAddOn[] = [
  { name: 'Overseas Support Service', unit: 'Month', unitPrice: 120 },
  { name: 'Local Support Service', unit: 'Each' },
  { name: 'Attend Board Meeting', unit: '2hrs/Each' },
  { name: 'Prepare board meeting Minutes', unit: 'Per meeting' },
  { name: 'Lodgement fee to MBSR Audited A/C', unit: 'Each' },
  { name: 'Lodgement fee for MBRS annual return', unit: 'Each' },
];

export const FIGMA_PACKAGE_NAMES = [
  'Dormant',
  'Basic Package',
  'Professional Package',
  'Enterprise Package',
  'Enterprise Plus',
] as const;
