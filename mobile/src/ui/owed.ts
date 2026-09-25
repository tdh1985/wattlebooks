import { cachedSnapshot, type App, type Snapshot } from '../app';
import { balance, sortInvoices, totalsByCurrency, type SnapInvoice } from '../lib/invoices';
import { formatMoney } from '../lib/money';
import { h, replace } from './dom';

const PILLS: Record<string, { label: string; tone: string }> = {
  Overdue: { label: 'Overdue', tone: 'overdue' },
  PartPaid: { label: 'Part paid', tone: 'partpaid' },
  Sent: { label: 'Sent', tone: 'sent' },
  Paid: { label: 'Paid', tone: 'paid' },
};

const dateFormat = new Intl.DateTimeFormat('en-AU', { day: 'numeric', month: 'short', year: 'numeric' });

function formatDate(isoDate: string): string {
  const [y, m, d] = isoDate.split('-').map(Number);
  return dateFormat.format(new Date(y, m - 1, d));
}

export function owedScreen(app: App, onSnapshot: (s: Snapshot) => void): { el: HTMLElement; refresh: () => void } {
  const root = h('section', { class: 'screen' });
  const heading = h('h1', null, 'Who owes me');
  const business = h('p', { class: 'eyebrow' });
  const refreshButton = h('button', { type: 'button', class: 'secondary small', onclick: () => refresh() }, 'Refresh');
  const banner = h('div');
  const body = h('div', { class: 'stack' });
  root.append(h('div', { class: 'screen-head' }, h('div', null, business, heading), refreshButton), banner, body);

  let loading = false;

  function render(snapshot: Snapshot) {
    business.textContent = snapshot.profile?.business_name || '';
    const home = snapshot.profile?.currency ?? snapshot.invoices[0]?.currency ?? 'AUD';

    if (!snapshot.profile && snapshot.invoices.length === 0) {
      replace(
        body,
        h(
          'div',
          { class: 'card empty' },
          h('p', null, 'Nothing here yet.'),
          h('p', { class: 'hint' }, 'Open InvoiceDesk on your computer and sign in on Settings → Phone.'),
        ),
      );
      return;
    }

    const totals = totalsByCurrency(snapshot.invoices, home);
    const invoices = sortInvoices(snapshot.invoices);
    replace(
      body,
      totals.map((t) =>
        h(
          'div',
          { class: 'totals' },
          h('div', { class: 'total-card' }, h('span', null, 'Owed'), h('strong', null, formatMoney(t.owed, t.currency))),
          h(
            'div',
            { class: `total-card ${t.overdue > 0 ? 'is-overdue' : ''}` },
            h('span', null, 'Overdue'),
            h('strong', null, formatMoney(t.overdue, t.currency)),
          ),
        ),
      ),
      invoices.length === 0
        ? h('div', { class: 'card empty' }, h('p', null, 'No one owes you anything right now.'))
        : h('ul', { class: 'invoice-list' }, invoices.map(invoiceRow)),
    );
  }

  function invoiceRow(invoice: SnapInvoice): HTMLElement {
    const pill = PILLS[invoice.status] ?? { label: invoice.status, tone: 'sent' };
    const owing = balance(invoice);
    return h(
      'li',
      { class: 'invoice' },
      h(
        'div',
        { class: 'invoice-main' },
        h('span', { class: 'invoice-client' }, invoice.client_name || 'No client'),
        h('span', { class: 'invoice-meta' }, `${invoice.number} · due ${formatDate(invoice.due_date)}`),
      ),
      h(
        'div',
        { class: 'invoice-side' },
        h('span', { class: 'invoice-amount' }, formatMoney(invoice.status === 'Paid' ? invoice.total_cents : owing, invoice.currency)),
        h('span', { class: `pill pill-${pill.tone}` }, pill.label),
      ),
    );
  }

  function showBanner(message: string) {
    replace(banner, message ? h('div', { class: 'banner', role: 'status' }, message) : null);
  }

  async function refresh() {
    if (loading) return;
    loading = true;
    refreshButton.disabled = true;
    refreshButton.textContent = 'Loading...';
    try {
      const snapshot = await app.fetchSnapshot();
      showBanner('');
      render(snapshot);
      onSnapshot(snapshot);
    } catch {
      const cached = cachedSnapshot();
      const when = cached ? ` Showing what it was at ${new Date(cached.fetched_at).toLocaleString('en-AU')}.` : '';
      showBanner(`You're offline or InvoiceDesk sync can't be reached.${when}`);
      if (cached) render(cached);
      else replace(body);
    } finally {
      loading = false;
      refreshButton.disabled = false;
      refreshButton.textContent = 'Refresh';
    }
  }

  const cached = cachedSnapshot();
  if (cached) render(cached);
  else replace(body, h('p', { class: 'muted' }, 'Loading...'));

  return { el: root, refresh };
}
