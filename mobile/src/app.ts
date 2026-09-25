import type { SupabaseClient } from '@supabase/supabase-js';
import { Outbox, type FlushResult } from './lib/queue';
import { inboxUploader, type SnapClient, type SnapProfile } from './lib/supabase';
import type { SnapInvoice } from './lib/invoices';

export interface Snapshot {
  profile: SnapProfile | null;
  clients: SnapClient[];
  invoices: SnapInvoice[];
  fetched_at: string;
}

const SNAPSHOT_KEY = 'invoicedesk.snapshot';

export class App {
  readonly outbox = new Outbox();

  constructor(
    readonly client: SupabaseClient,
    readonly userId: string,
    readonly email: string,
  ) {}

  flush(): Promise<FlushResult> {
    return this.outbox.flush(inboxUploader(this.client), this.userId);
  }

  pendingCount(): Promise<number> {
    return this.outbox.count(this.userId);
  }

  async fetchSnapshot(): Promise<Snapshot> {
    const [profile, clients, invoices] = await Promise.all([
      this.client.from('snap_profile').select('business_name,currency,tax_enabled,tax_rate_ppm,tax_name').maybeSingle(),
      this.client.from('snap_clients').select('local_id,name,email').order('name'),
      this.client
        .from('snap_invoices')
        .select('local_id,number,client_name,issue_date,due_date,currency,total_cents,paid_cents,status'),
    ]);
    const error = profile.error ?? clients.error ?? invoices.error;
    if (error) throw error;
    const snapshot: Snapshot = {
      profile: profile.data as SnapProfile | null,
      clients: (clients.data ?? []) as SnapClient[],
      invoices: (invoices.data ?? []) as SnapInvoice[],
      fetched_at: new Date().toISOString(),
    };
    saveSnapshot(snapshot);
    return snapshot;
  }
}

// kept so quick invoice still works with no signal
export function cachedSnapshot(): Snapshot | null {
  try {
    const raw = localStorage.getItem(SNAPSHOT_KEY);
    return raw ? (JSON.parse(raw) as Snapshot) : null;
  } catch {
    return null;
  }
}

function saveSnapshot(snapshot: Snapshot): void {
  try {
    localStorage.setItem(SNAPSHOT_KEY, JSON.stringify(snapshot));
  } catch {
    // private mode can refuse storage and the live data still shows
  }
}

export function clearSnapshot(): void {
  try {
    localStorage.removeItem(SNAPSHOT_KEY);
  } catch {
    // nothing cached to clear
  }
}
