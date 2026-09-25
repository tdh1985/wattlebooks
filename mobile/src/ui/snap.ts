import type { App } from '../app';
import { toJpeg } from '../lib/image';
import { receiptPayload, type DraftInvoicePayload, type ReceiptPayload } from '../lib/payloads';
import type { FailedItem } from '../lib/queue';
import { errorText, h, replace, toast } from './dom';

const when = new Intl.DateTimeFormat('en-AU', { day: 'numeric', month: 'short', hour: 'numeric', minute: '2-digit' });

export function snapScreen(app: App): HTMLElement {
  const content = h('div', { class: 'screen' });
  const failedBox = h('div');
  const root = h('section', { class: 'screen' }, content, failedBox);
  let previewUrl = '';

  function release() {
    if (previewUrl) URL.revokeObjectURL(previewUrl);
    previewUrl = '';
  }

  const fileInput = () =>
    h('input', {
      type: 'file',
      accept: 'image/*',
      capture: 'environment',
      class: 'visually-hidden',
      onchange: (e: Event) => {
        const file = (e.target as HTMLInputElement).files?.[0];
        if (file) showPreview(file);
      },
    });

  function showStart() {
    release();
    replace(
      content,
      h('h1', null, 'Snap receipt'),
      h('p', { class: 'muted' }, 'It goes to Money in & out on your computer, ready to scan.'),
      h(
        'label',
        { class: 'camera-button' },
        fileInput(),
        h('span', { class: 'camera-icon', 'aria-hidden': 'true' }, cameraSvg()),
        h('span', null, 'Take a photo'),
      ),
    );
  }

  function showPreview(file: File) {
    release();
    previewUrl = URL.createObjectURL(file);
    const note = h('textarea', {
      id: 'note',
      rows: 2,
      maxlength: 500,
      placeholder: 'Fuel for the ute',
    });
    const error = h('p', { class: 'error', role: 'alert' });
    const save = h('button', { type: 'submit', class: 'primary' }, 'Save');

    const form = h(
      'form',
      {
        class: 'stack',
        onsubmit: async (e: Event) => {
          e.preventDefault();
          save.disabled = true;
          save.textContent = 'Saving...';
          try {
            const jpeg = await toJpeg(file);
            await app.outbox.add({
              id: crypto.randomUUID(),
              user_id: app.userId,
              kind: 'receipt',
              payload: receiptPayload(note.value),
              photo: await jpeg.arrayBuffer(),
              queued_at: new Date().toISOString(),
            });
          } catch (err) {
            save.disabled = false;
            save.textContent = 'Save';
            error.textContent = `Couldn't save the photo. ${errorText(err)}`;
            return;
          }
          showStart();
          toast(navigator.onLine ? 'Saved. Uploading now.' : "Saved. It'll upload when you have signal.");
          void app.flush();
        },
      },
      h('img', { class: 'receipt-preview', src: previewUrl, alt: 'The receipt you just took' }),
      h('label', { for: 'note' }, 'Note ', h('span', { class: 'muted' }, '(optional)')),
      note,
      error,
      save,
      h('label', { class: 'secondary button-like' }, fileInput(), 'Retake'),
    );
    replace(content, h('h1', null, 'Snap receipt'), form);
  }

  function failedRow(item: FailedItem): HTMLElement {
    const title =
      item.kind === 'receipt'
        ? (item.payload as ReceiptPayload).note || 'Receipt photo'
        : `Draft invoice, ${(item.payload as DraftInvoicePayload).lines?.length ?? 0} lines`;
    return h(
      'li',
      { class: 'failed-item' },
      h(
        'div',
        { class: 'failed-main' },
        h('strong', null, title),
        h('span', { class: 'muted' }, when.format(new Date(item.queued_at))),
        h('span', { class: 'failed-reason' }, item.reason),
      ),
      h(
        'button',
        {
          type: 'button',
          class: 'secondary small',
          'aria-label': `Discard ${title}`,
          onclick: () => void app.outbox.discard(item.seq),
        },
        'Discard',
      ),
    );
  }

  async function renderFailed() {
    const failed = await app.outbox.listFailed(app.userId);
    replace(
      failedBox,
      failed.length === 0
        ? null
        : h(
            'div',
            { class: 'stack' },
            h('h2', null, "Couldn't upload"),
            h('p', { class: 'hint' }, 'InvoiceDesk sync turned these down, so trying again won’t help.'),
            h('ul', { class: 'failed-list' }, failed.map(failedRow)),
          ),
    );
  }

  showStart();
  void renderFailed();
  app.outbox.onChange(() => void renderFailed());
  return root;
}

function cameraSvg(): SVGElement {
  const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
  svg.setAttribute('viewBox', '0 0 24 24');
  svg.setAttribute('width', '40');
  svg.setAttribute('height', '40');
  svg.innerHTML =
    '<path fill="none" stroke="currentColor" stroke-width="1.8" stroke-linejoin="round" d="M4 8h3l1.6-2.4h6.8L17 8h3a1 1 0 0 1 1 1v9a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V9a1 1 0 0 1 1-1z"/><circle cx="12" cy="13" r="3.6" fill="none" stroke="currentColor" stroke-width="1.8"/>';
  return svg;
}
