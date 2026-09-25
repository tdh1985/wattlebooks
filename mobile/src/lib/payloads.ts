// inbox payloads version 1, the desktop reads these shapes exactly

export interface ReceiptPayload {
  v: 1;
  note: string;
  taken_at: string;
}

export type DraftClient = { local_id: number } | { name: string; email: string };

export interface DraftLine {
  description: string;
  quantity: number;
  unit_cents: number;
}

export interface DraftInvoicePayload {
  v: 1;
  client: DraftClient;
  lines: DraftLine[];
  notes: string;
}

export function receiptPayload(note: string, takenAt: Date = new Date()): ReceiptPayload {
  return { v: 1, note: note.trim(), taken_at: takenAt.toISOString() };
}

export function draftInvoicePayload(client: DraftClient, lines: DraftLine[], notes: string): DraftInvoicePayload {
  return {
    v: 1,
    client,
    lines: lines.map((l) => ({ description: l.description.trim(), quantity: l.quantity, unit_cents: l.unit_cents })),
    notes: notes.trim(),
  };
}
