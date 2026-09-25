import { createClient, type SupabaseClient } from '@supabase/supabase-js';
import { isDuplicate, type QueueItem, type Uploader } from './queue';

const url = import.meta.env.VITE_SUPABASE_URL as string | undefined;
const key = import.meta.env.VITE_SUPABASE_PUBLISHABLE_KEY as string | undefined;

export const supabase: SupabaseClient | null = url && key ? createClient(url, key) : null;

export interface SnapProfile {
  business_name: string;
  currency: string;
  tax_enabled: boolean;
  tax_rate_ppm: number;
  tax_name: string;
}

export interface SnapClient {
  local_id: number;
  name: string;
  email: string;
}

export function inboxUploader(client: SupabaseClient): Uploader {
  return {
    async send(item: QueueItem) {
      let photoPath: string | null = null;
      if (item.photo) {
        photoPath = `${item.user_id}/${item.id}.jpg`;
        const { error } = await client.storage
          .from('receipts')
          .upload(photoPath, new Blob([item.photo], { type: 'image/jpeg' }), {
            upsert: true,
            contentType: 'image/jpeg',
          });
        if (error && !isDuplicate(error)) throw error;
      }

      const { error, status } = await client
        .from('inbox')
        .insert({ id: item.id, kind: item.kind, payload: item.payload, photo_path: photoPath });
      // postgrest puts the http status on the response not the error
      if (error) throw Object.assign(new Error(error.message), { code: error.code, status });
    },

    async refresh() {
      const { error } = await client.auth.refreshSession();
      return !error;
    },
  };
}
