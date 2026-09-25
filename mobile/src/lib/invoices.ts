export type InvoiceStatus = 'Sent' | 'PartPaid' | 'Paid' | 'Overdue';

export interface SnapInvoice {
  local_id: number;
  number: string;
  client_name: string;
  issue_date: string;
  due_date: string;
  currency: string;
  total_cents: number;
  paid_cents: number;
  status: InvoiceStatus | string;
}

export interface CurrencyTotals {
  currency: string;
  owed: number;
  overdue: number;
}

export function balance(invoice: SnapInvoice): number {
  return invoice.total_cents - invoice.paid_cents;
}

// paid ones sink to the bottom since nobody needs chasing for them
function rank(invoice: SnapInvoice): number {
  if (invoice.status === 'Overdue') return 0;
  return invoice.status === 'Paid' ? 2 : 1;
}

export function sortInvoices(invoices: SnapInvoice[]): SnapInvoice[] {
  return [...invoices].sort(
    (a, b) =>
      rank(a) - rank(b) ||
      a.due_date.localeCompare(b.due_date) ||
      a.number.localeCompare(b.number, 'en-AU', { numeric: true }),
  );
}

// one total per currency since foreign invoices can't be added to local ones
export function totalsByCurrency(invoices: SnapInvoice[], home: string): CurrencyTotals[] {
  const byCurrency = new Map<string, CurrencyTotals>();
  for (const invoice of invoices) {
    const owing = balance(invoice);
    if (owing <= 0) continue;
    const totals = byCurrency.get(invoice.currency) ?? { currency: invoice.currency, owed: 0, overdue: 0 };
    totals.owed += owing;
    if (invoice.status === 'Overdue') totals.overdue += owing;
    byCurrency.set(invoice.currency, totals);
  }
  if (!byCurrency.has(home)) byCurrency.set(home, { currency: home, owed: 0, overdue: 0 });
  return [...byCurrency.values()].sort(
    (a, b) => Number(b.currency === home) - Number(a.currency === home) || a.currency.localeCompare(b.currency),
  );
}
