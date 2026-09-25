import { openDB, type DBSchema, type IDBPDatabase } from 'idb';

export type InboxKind = 'receipt' | 'draft_invoice';

export interface QueueItem {
  id: string;
  // a shared phone must never upload one login's items into another's inbox
  user_id: string;
  kind: InboxKind;
  payload: unknown;
  // an arraybuffer since older safari lost blobs stored in indexeddb
  photo: ArrayBuffer | null;
  queued_at: string;
}

export interface StoredItem extends QueueItem {
  seq: number;
}

export interface FailedItem extends StoredItem {
  reason: string;
  failed_at: string;
}

export interface Uploader {
  send(item: QueueItem): Promise<void>;
  // lets a flush recover from an expired token without the user noticing
  refresh?(): Promise<boolean>;
}

export interface FlushResult {
  sent: number;
  failed: number;
  remaining: number;
  error: unknown;
}

export type Outcome = 'done' | 'permanent' | 'retry';

interface OutboxSchema extends DBSchema {
  items: { key: number; value: StoredItem };
  failed: { key: number; value: FailedItem };
}

export function statusOf(error: unknown): number | null {
  if (!error || typeof error !== 'object') return null;
  const e = error as { status?: unknown; statusCode?: unknown };
  const raw = typeof e.status === 'number' && e.status > 0 ? e.status : Number(e.statusCode);
  return Number.isInteger(raw) && raw > 0 ? raw : null;
}

// the row or photo is already there from a try that timed out
export function isDuplicate(error: unknown): boolean {
  if (!error || typeof error !== 'object') return false;
  return (error as { code?: unknown }).code === '23505' || statusOf(error) === 409;
}

// these 4xx codes can pass on a later try so they keep their place
const TRANSIENT_4XX = new Set([401, 408, 429]);

export function classify(error: unknown): Outcome {
  if (isDuplicate(error)) return 'done';
  const status = statusOf(error);
  if (status !== null && status >= 400 && status < 500 && !TRANSIENT_4XX.has(status)) return 'permanent';
  return 'retry';
}

function reasonOf(error: unknown): string {
  const message = error && typeof error === 'object' && 'message' in error ? String(error.message) : String(error);
  const status = statusOf(error);
  return status ? `${message} (${status})` : message;
}

export class Outbox {
  private db: Promise<IDBPDatabase<OutboxSchema>> | null = null;
  private running: Promise<FlushResult> | null = null;
  private again = false;
  private listeners = new Set<() => void>();

  constructor(private readonly name = 'invoicedesk-outbox') {}

  private open(): Promise<IDBPDatabase<OutboxSchema>> {
    this.db ??= openDB<OutboxSchema>(this.name, 2, {
      upgrade(db, oldVersion) {
        if (oldVersion < 1) db.createObjectStore('items', { keyPath: 'seq', autoIncrement: true });
        if (oldVersion < 2) db.createObjectStore('failed', { keyPath: 'seq' });
      },
    });
    return this.db;
  }

  onChange(listener: () => void): () => void {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  private changed(): void {
    for (const listener of this.listeners) listener();
  }

  async add(item: QueueItem): Promise<void> {
    const db = await this.open();
    await db.add('items', item as StoredItem);
    this.changed();
  }

  async list(userId: string): Promise<StoredItem[]> {
    const db = await this.open();
    return (await db.getAll('items')).filter((i) => i.user_id === userId);
  }

  async count(userId: string): Promise<number> {
    return (await this.list(userId)).length;
  }

  async listFailed(userId: string): Promise<FailedItem[]> {
    const db = await this.open();
    return (await db.getAll('failed')).filter((i) => i.user_id === userId);
  }

  async discard(seq: number): Promise<void> {
    const db = await this.open();
    await db.delete('failed', seq);
    this.changed();
  }

  flush(uploader: Uploader, userId: string): Promise<FlushResult> {
    if (this.running) {
      this.again = true;
      return this.running;
    }
    this.running = this.drain(uploader, userId).finally(() => {
      this.running = null;
    });
    return this.running;
  }

  private async send(uploader: Uploader, item: StoredItem, canRefresh: { value: boolean }): Promise<unknown> {
    const { seq: _seq, ...plain } = item;
    try {
      await uploader.send(plain);
      return null;
    } catch (error) {
      if (statusOf(error) !== 401 || !canRefresh.value || !uploader.refresh) return error;
      canRefresh.value = false;
      if (!(await uploader.refresh())) return error;
      return this.send(uploader, item, canRefresh);
    }
  }

  // a retryable failure stops the flush so the desktop gets items in order
  private async drain(uploader: Uploader, userId: string): Promise<FlushResult> {
    let sent = 0;
    let failed = 0;
    const canRefresh = { value: true };
    try {
      do {
        this.again = false;
        for (const item of await this.list(userId)) {
          const error = await this.send(uploader, item, canRefresh);
          const outcome = error === null ? 'done' : classify(error);
          if (outcome === 'retry') return { sent, failed, remaining: await this.count(userId), error };

          const db = await this.open();
          const tx = db.transaction(['items', 'failed'], 'readwrite');
          if (outcome === 'permanent') {
            await tx.objectStore('failed').put({ ...item, reason: reasonOf(error), failed_at: new Date().toISOString() });
            failed++;
          } else {
            sent++;
          }
          await tx.objectStore('items').delete(item.seq);
          await tx.done;
        }
      } while (this.again);
      return { sent, failed, remaining: await this.count(userId), error: null };
    } finally {
      this.changed();
    }
  }

  close(): void {
    void this.db?.then((db) => db.close());
    this.db = null;
  }
}
