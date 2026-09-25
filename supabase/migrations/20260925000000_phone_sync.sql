-- phone sync: the desktop writes snap_*, the phone writes inbox and receipt photos
-- every row belongs to one login and row level security keeps it that way

create table public.sync_links (
  user_id uuid primary key default auth.uid() references auth.users on delete cascade,
  sync_id text not null,
  linked_at timestamptz not null default now()
);

create table public.snap_profile (
  user_id uuid primary key default auth.uid() references auth.users on delete cascade,
  business_name text not null default '',
  currency text not null,
  tax_enabled boolean not null,
  tax_rate_ppm integer not null,
  tax_name text not null default 'GST',
  updated_at timestamptz not null default now()
);

create table public.snap_clients (
  user_id uuid not null default auth.uid() references auth.users on delete cascade,
  local_id integer not null,
  name text not null,
  email text not null default '',
  primary key (user_id, local_id)
);

create table public.snap_invoices (
  user_id uuid not null default auth.uid() references auth.users on delete cascade,
  local_id integer not null,
  number text not null,
  client_name text not null,
  issue_date date not null,
  due_date date not null,
  currency text not null,
  total_cents bigint not null,
  paid_cents bigint not null,
  status text not null,
  primary key (user_id, local_id)
);

-- the phone picks the id so a retried upload can't make a second row
create table public.inbox (
  id uuid primary key,
  user_id uuid not null default auth.uid() references auth.users on delete cascade,
  kind text not null check (kind in ('receipt', 'draft_invoice')),
  payload jsonb not null default '{}'::jsonb,
  photo_path text,
  created_at timestamptz not null default now(),
  claimed_at timestamptz,
  claimed_by text,
  failed text
);

create index inbox_user_created on public.inbox (user_id, created_at);

alter table public.sync_links enable row level security;
alter table public.snap_profile enable row level security;
alter table public.snap_clients enable row level security;
alter table public.snap_invoices enable row level security;
alter table public.inbox enable row level security;

create policy "own rows" on public.sync_links for all to authenticated
  using (user_id = (select auth.uid())) with check (user_id = (select auth.uid()));
create policy "own rows" on public.snap_profile for all to authenticated
  using (user_id = (select auth.uid())) with check (user_id = (select auth.uid()));
create policy "own rows" on public.snap_clients for all to authenticated
  using (user_id = (select auth.uid())) with check (user_id = (select auth.uid()));
create policy "own rows" on public.snap_invoices for all to authenticated
  using (user_id = (select auth.uid())) with check (user_id = (select auth.uid()));
create policy "own rows" on public.inbox for all to authenticated
  using (user_id = (select auth.uid())) with check (user_id = (select auth.uid()));

-- photos live under <user id>/ so the folder name is the owner check
insert into storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
values ('receipts', 'receipts', false, 10485760, array['image/jpeg', 'image/png', 'image/webp']);

create policy "own receipts read" on storage.objects for select to authenticated
  using (bucket_id = 'receipts' and (storage.foldername(name))[1] = (select auth.uid())::text);
create policy "own receipts add" on storage.objects for insert to authenticated
  with check (bucket_id = 'receipts' and (storage.foldername(name))[1] = (select auth.uid())::text);
-- a retried upload overwrites its own photo, which storage treats as an update
create policy "own receipts update" on storage.objects for update to authenticated
  using (bucket_id = 'receipts' and (storage.foldername(name))[1] = (select auth.uid())::text)
  with check (bucket_id = 'receipts' and (storage.foldername(name))[1] = (select auth.uid())::text);
create policy "own receipts delete" on storage.objects for delete to authenticated
  using (bucket_id = 'receipts' and (storage.foldername(name))[1] = (select auth.uid())::text);
