// money is whole cents in bigint maths so floats never touch a total

export interface TaxProfile {
  tax_enabled: boolean;
  tax_rate_ppm: number;
}

export interface PreviewLine {
  quantity: number;
  unit_cents: number;
}

export interface PreviewTotals {
  subtotal: number;
  tax: number;
  total: number;
}

interface Decimal {
  units: bigint;
  scale: number;
}

const DECIMAL = /^(-)?(\d*)(?:\.(\d*))?$/;

function parseDecimal(text: string): Decimal | null {
  const cleaned = text.trim().replace(/[\s,$]/g, '');
  const match = DECIMAL.exec(cleaned);
  if (!match) return null;
  const [, minus, whole = '', fraction = ''] = match;
  if (whole === '' && fraction === '') return null;
  const units = BigInt((whole || '0') + fraction);
  return { units: minus ? -units : units, scale: fraction.length };
}

// matches the desktop's MidpointRounding.AwayFromZero
export function divRound(numerator: bigint, denominator: bigint): bigint {
  const negative = numerator < 0n !== denominator < 0n;
  const n = numerator < 0n ? -numerator : numerator;
  const d = denominator < 0n ? -denominator : denominator;
  const rounded = (n * 2n + d) / (d * 2n);
  return negative ? -rounded : rounded;
}

// people type dollars so "12.5" has to mean 1250 cents
export function parseCents(text: string): number | null {
  const value = parseDecimal(text);
  if (!value) return null;
  const cents = divRound(value.units * 100n, 10n ** BigInt(value.scale));
  return Number(cents);
}

// four places keeps String(q) out of e-notation
export function parseQuantity(text: string): number | null {
  const value = parseDecimal(text);
  if (!value || value.units <= 0n || value.scale > 4) return null;
  return Number(value.units) / 10 ** value.scale;
}

export function lineCents(quantity: number, unitCents: number): number {
  const q = parseDecimal(String(quantity));
  if (!q) return Math.round(quantity * unitCents);
  return Number(divRound(q.units * BigInt(unitCents), 10n ** BigInt(q.scale)));
}

export function taxOn(taxableCents: number, ratePpm: number): number {
  return Number(divRound(BigInt(taxableCents) * BigInt(ratePpm), 1_000_000n));
}

// tax once on the subtotal like the desktop so rounding can't pile up
export function previewTotals(lines: PreviewLine[], profile: TaxProfile | null): PreviewTotals {
  const subtotal = lines.reduce((sum, line) => sum + lineCents(line.quantity, line.unit_cents), 0);
  const tax = profile?.tax_enabled ? taxOn(subtotal, profile.tax_rate_ppm) : 0;
  return { subtotal, tax, total: subtotal + tax };
}

const formatters = new Map<string, Intl.NumberFormat>();

export function formatMoney(cents: number, currency: string): string {
  let formatter = formatters.get(currency);
  if (!formatter) {
    try {
      formatter = new Intl.NumberFormat('en-AU', { style: 'currency', currency });
    } catch {
      // a bad code from the snapshot shouldn't blank the whole list
      formatter = new Intl.NumberFormat('en-AU', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }
    formatters.set(currency, formatter);
  }
  return formatter.format(cents / 100);
}

export function formatRate(ratePpm: number): string {
  return `${Number((ratePpm / 10_000).toFixed(4))}%`;
}
