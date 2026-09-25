import { cachedSnapshot, type App, type Snapshot } from '../app';
import { formatMoney, formatRate, lineCents, parseCents, parseQuantity, previewTotals } from '../lib/money';
import { draftInvoicePayload, type DraftClient, type DraftLine } from '../lib/payloads';
import type { SnapClient } from '../lib/supabase';
import { errorText, h, replace } from './dom';

interface LineInputs {
  row: HTMLElement;
  description: HTMLInputElement;
  quantity: HTMLInputElement;
  price: HTMLInputElement;
  amount: HTMLElement;
}

type ClientChoice = { kind: 'none' } | { kind: 'existing'; client: SnapClient } | { kind: 'new' };

export function quickScreen(app: App): { el: HTMLElement; update: (s: Snapshot) => void } {
  const root = h('section', { class: 'screen' });
  let snapshot = cachedSnapshot();
  let choice: ClientChoice = { kind: 'none' };
  let lines: LineInputs[] = [];
  let nextLineId = 0;

  const currency = () => snapshot?.profile?.currency ?? 'AUD';

  const clientBox = h('div', { class: 'stack' });
  const lineList = h('div', { class: 'stack' });
  const notes = h('textarea', { id: 'notes', rows: 3, maxlength: 2000 });
  const preview = h('div', { class: 'preview' });
  const error = h('p', { class: 'error', role: 'alert' });
  const newName = h('input', { id: 'new-name', type: 'text', autocomplete: 'off', maxlength: 200 });
  const newEmail = h('input', { id: 'new-email', type: 'email', autocomplete: 'off', inputmode: 'email' });

  function renderClient() {
    if (choice.kind === 'existing') {
      const picked = choice.client;
      replace(
        clientBox,
        h(
          'div',
          { class: 'chosen' },
          h('div', null, h('strong', null, picked.name), picked.email ? h('span', { class: 'muted' }, picked.email) : null),
          h('button', { type: 'button', class: 'link', onclick: () => setChoice({ kind: 'none' }) }, 'Change'),
        ),
      );
      return;
    }

    if (choice.kind === 'new') {
      replace(
        clientBox,
        h('label', { for: 'new-name' }, 'Client name'),
        newName,
        h('label', { for: 'new-email' }, 'Email ', h('span', { class: 'muted' }, '(optional)')),
        newEmail,
        h('button', { type: 'button', class: 'link', onclick: () => setChoice({ kind: 'none' }) }, 'Pick an existing client'),
      );
      newName.focus();
      return;
    }

    const clients = snapshot?.clients ?? [];
    const search = h('input', {
      type: 'search',
      placeholder: clients.length ? 'Search clients' : 'No clients synced yet',
      'aria-label': 'Search clients',
      autocomplete: 'off',
    });
    const results = h('ul', { class: 'client-list' });
    const showResults = () => {
      const q = search.value.trim().toLowerCase();
      const matches = clients
        .filter((c) => !q || c.name.toLowerCase().includes(q) || c.email.toLowerCase().includes(q))
        .slice(0, 8);
      replace(
        results,
        matches.map((c) =>
          h(
            'li',
            null,
            h(
              'button',
              { type: 'button', onclick: () => setChoice({ kind: 'existing', client: c }) },
              h('strong', null, c.name),
              c.email ? h('span', { class: 'muted' }, c.email) : null,
            ),
          ),
        ),
      );
    };
    search.addEventListener('input', showResults);
    showResults();
    replace(
      clientBox,
      search,
      results,
      h(
        'button',
        {
          type: 'button',
          class: 'secondary',
          onclick: () => {
            newName.value = search.value.trim();
            setChoice({ kind: 'new' });
          },
        },
        '+ New client',
      ),
    );
  }

  function setChoice(next: ClientChoice) {
    choice = next;
    renderClient();
  }

  function addLine() {
    const n = nextLineId++;
    const description = h('input', { id: `desc-${n}`, type: 'text', maxlength: 500, placeholder: 'Call-out' });
    const quantity = h('input', {
      id: `qty-${n}`,
      type: 'text',
      inputmode: 'decimal',
      value: '1',
      class: 'num',
      oninput: renderPreview,
    });
    const price = h('input', {
      id: `price-${n}`,
      type: 'text',
      inputmode: 'decimal',
      placeholder: '0.00',
      class: 'num',
      oninput: renderPreview,
    });
    const amount = h('span', { class: 'line-amount' });
    const inputs: LineInputs = { row: h('div'), description, quantity, price, amount };
    inputs.row = h(
      'div',
      { class: 'line card' },
      h('label', { for: `desc-${n}` }, 'Description'),
      description,
      h(
        'div',
        { class: 'line-grid' },
        h('div', null, h('label', { for: `qty-${n}` }, 'Qty'), quantity),
        h('div', null, h('label', { for: `price-${n}` }, 'Unit price ex tax'), price),
      ),
      h(
        'div',
        { class: 'line-foot' },
        amount,
        h(
          'button',
          {
            type: 'button',
            class: 'link danger',
            onclick: () => {
              lines = lines.filter((l) => l !== inputs);
              inputs.row.remove();
              if (lines.length === 0) addLine();
              renderPreview();
            },
          },
          'Remove',
        ),
      ),
    );
    lines.push(inputs);
    lineList.append(inputs.row);
    renderPreview();
  }

  function isBlank(line: LineInputs): boolean {
    return !line.description.value.trim() && !line.price.value.trim();
  }

  function readLines(): { lines: DraftLine[]; problem: string } {
    const out: DraftLine[] = [];
    for (const [i, line] of lines.entries()) {
      if (isBlank(line)) continue;
      const quantity = parseQuantity(line.quantity.value);
      const unit = parseCents(line.price.value || '0');
      if (quantity === null) return { lines: out, problem: `Line ${i + 1}: the quantity needs to be a number above 0.` };
      if (unit === null) return { lines: out, problem: `Line ${i + 1}: the price needs to be an amount like 120 or 49.50.` };
      out.push({ description: line.description.value, quantity, unit_cents: unit });
    }
    return { lines: out, problem: out.length ? '' : 'Add at least one line.' };
  }

  function renderPreview() {
    const money = (cents: number) => formatMoney(cents, currency());
    const valid: DraftLine[] = [];
    for (const line of lines) {
      const quantity = parseQuantity(line.quantity.value);
      const unit = parseCents(line.price.value || '0');
      const ok = quantity !== null && unit !== null && !isBlank(line);
      line.amount.textContent = ok ? money(lineCents(quantity, unit)) : '';
      if (ok) valid.push({ description: '', quantity, unit_cents: unit });
    }
    const profile = snapshot?.profile ?? null;
    const totals = previewTotals(valid, profile);
    replace(
      preview,
      h('div', { class: 'preview-row' }, h('span', null, 'Subtotal'), h('span', null, money(totals.subtotal))),
      profile?.tax_enabled
        ? h(
            'div',
            { class: 'preview-row' },
            h('span', null, `${profile.tax_name} ${formatRate(profile.tax_rate_ppm)}`),
            h('span', null, money(totals.tax)),
          )
        : null,
      h('div', { class: 'preview-row preview-total' }, h('span', null, 'Total'), h('span', null, money(totals.total))),
      h(
        'p',
        { class: 'hint' },
        profile ? 'Estimate. InvoiceDesk works out the final total.' : 'Estimate without tax. InvoiceDesk works out the final total.',
      ),
    );
  }

  function readClient(): DraftClient | string {
    if (choice.kind === 'existing') return { local_id: choice.client.local_id };
    if (choice.kind === 'new') {
      const name = newName.value.trim();
      if (!name) return 'Give the new client a name.';
      return { name, email: newEmail.value.trim() };
    }
    return 'Pick a client or add a new one.';
  }

  const save = h('button', { type: 'submit', class: 'primary' }, 'Save draft');

  async function onSubmit(e: Event) {
    e.preventDefault();
    const client = readClient();
    if (typeof client === 'string') {
      error.textContent = client;
      return;
    }
    const read = readLines();
    if (read.problem) {
      error.textContent = read.problem;
      return;
    }
    error.textContent = '';
    save.disabled = true;
    try {
      await app.outbox.add({
        id: crypto.randomUUID(),
        user_id: app.userId,
        kind: 'draft_invoice',
        payload: draftInvoicePayload(client, read.lines, notes.value),
        photo: null,
        queued_at: new Date().toISOString(),
      });
    } catch (err) {
      error.textContent = `Couldn't save. ${errorText(err)}`;
      save.disabled = false;
      return;
    }
    void app.flush();
    showDone();
  }

  function showDone() {
    replace(
      root,
      h('h1', null, 'Quick invoice'),
      h(
        'div',
        { class: 'card stack done' },
        h('p', { class: 'done-title' }, 'Saved.'),
        h('p', null, "It'll be in InvoiceDesk as a draft next time it's open."),
        h('button', { type: 'button', class: 'primary', onclick: showForm }, 'Start another'),
      ),
    );
  }

  function showForm() {
    choice = { kind: 'none' };
    lines = [];
    newName.value = '';
    newEmail.value = '';
    notes.value = '';
    error.textContent = '';
    save.disabled = false;
    replace(lineList);
    replace(
      root,
      h('h1', null, 'Quick invoice'),
      h('p', { class: 'muted' }, 'A draft for InvoiceDesk to number and send.'),
      h(
        'form',
        { class: 'stack', onsubmit: onSubmit, novalidate: true },
        h('h2', null, 'Client'),
        clientBox,
        h('h2', null, 'Lines'),
        lineList,
        h('button', { type: 'button', class: 'secondary', onclick: addLine }, '+ Add line'),
        h('label', { for: 'notes' }, 'Notes ', h('span', { class: 'muted' }, '(optional)')),
        notes,
        preview,
        error,
        save,
      ),
    );
    renderClient();
    addLine();
  }

  showForm();

  return {
    el: root,
    update(next: Snapshot) {
      snapshot = next;
      if (choice.kind === 'none' && root.contains(clientBox)) renderClient();
      renderPreview();
    },
  };
}
