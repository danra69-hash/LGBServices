/** Display format used across the app (dd/mm/yyyy). */
export const DATE_DISPLAY_FORMAT = 'dd/mm/yyyy';

const COMPLETE_ISO = /^\d{4}-\d{2}-\d{2}$/;
const COMPLETE_DISPLAY = /^(\d{1,2})\/(\d{1,2})\/(\d{4})$/;
const COMPLETE_DISPLAY_DASH = /^(\d{1,2})-(\d{1,2})-(\d{4})$/;

/** Normalize API or user input to dd/mm/yyyy for display. */
export function formatDateDisplay(value?: string | null): string {
  if (!value) return '';
  const iso = parseDateToIso(value);
  if (!iso) return value;
  const [, y, m, d] = iso.match(/^(\d{4})-(\d{2})-(\d{2})$/) ?? [];
  if (!y) return value;
  return `${d}/${m}/${y}`;
}

/** Parse dd/mm/yyyy, dd-mm-yyyy, or yyyy-mm-dd to yyyy-mm-dd for API. */
export function parseDateToIso(value: string): string | null {
  const trimmed = value.trim();
  if (!trimmed) return null;

  if (COMPLETE_ISO.test(trimmed)) return trimmed;

  const slash = trimmed.match(COMPLETE_DISPLAY);
  if (slash) {
    const day = Number(slash[1]);
    const month = Number(slash[2]);
    const year = Number(slash[3]);
    if (!isValidDate(year, month, day)) return null;
    return `${year}-${pad(month)}-${pad(day)}`;
  }

  const dash = trimmed.match(COMPLETE_DISPLAY_DASH);
  if (dash) {
    const day = Number(dash[1]);
    const month = Number(dash[2]);
    const year = Number(dash[3]);
    if (!isValidDate(year, month, day)) return null;
    return `${year}-${pad(month)}-${pad(day)}`;
  }

  const digits = trimmed.replace(/\D/g, '');
  if (digits.length === 8) {
    const day = Number(digits.slice(0, 2));
    const month = Number(digits.slice(2, 4));
    const year = Number(digits.slice(4, 8));
    if (!isValidDate(year, month, day)) return null;
    return `${year}-${pad(month)}-${pad(day)}`;
  }

  return null;
}

export function isCompleteDisplayDate(value: string): boolean {
  return parseDateToIso(value) !== null;
}

/** Insert slashes while typing digits: 15062026 → 15/06/2026 */
export function formatDateTyping(value: string): string {
  const digits = value.replace(/\D/g, '').slice(0, 8);
  if (digits.length <= 2) return digits;
  if (digits.length <= 4) return `${digits.slice(0, 2)}/${digits.slice(2)}`;
  return `${digits.slice(0, 2)}/${digits.slice(2, 4)}/${digits.slice(4)}`;
}

/** Parse API schedule timestamps (yyyy-mm-dd or yyyy-mm-ddTHH:mm) to calendar parts. */
export function parseScheduleToDay(value: string): { year: number; month: number; day: number } | null {
  const head = value.trim().slice(0, 10);
  const iso = parseDateToIso(head);
  if (!iso) return null;
  const [year, month, day] = iso.split('-').map(Number);
  return { year, month: month - 1, day };
}

function pad(n: number): string {
  return String(n).padStart(2, '0');
}

function isValidDate(year: number, month: number, day: number): boolean {
  if (month < 1 || month > 12 || day < 1 || day > 31) return false;
  const dt = new Date(year, month - 1, day);
  return dt.getFullYear() === year && dt.getMonth() === month - 1 && dt.getDate() === day;
}
