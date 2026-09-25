# InvoiceDesk phone app

A small installable web app (PWA) for snapping receipts, seeing who owes you and starting a quick invoice. The desktop app stays the source of truth: the phone reads the `snap_*` tables and writes to `inbox` and the `receipts` bucket. See `docs/superpowers/specs/2026-09-25-phone-sync-design.md`.

## Setup

```sh
cd mobile
npm install
cp .env.example .env.local   # then fill in the two values
```

| Variable | Where to find it |
|---|---|
| `VITE_SUPABASE_URL` | Supabase project → Settings → API → Project URL |
| `VITE_SUPABASE_PUBLISHABLE_KEY` | Supabase project → Settings → API Keys → publishable key |

Vite puts these in the bundle at build time, so set them before `npm run build`. Only use the publishable key, never the secret one. Without them the app shows a "Not configured" screen.

## Run, test, build

```sh
npm run dev       # http://localhost:5173
npm test          # vitest: money maths, invoice sorting, offline queue
npm run build     # type-check, then build to dist/
npm run preview   # serve dist/ to try the service worker
```

The service worker only registers in a production build, so use `npm run preview` to test installing and offline use.

## Deploy

Deploy to Vercel with **root directory `mobile`**. Vercel picks up Vite on its own (build `npm run build`, output `dist`). Add both `VITE_` variables in the Vercel project's environment variables and redeploy.

In Supabase, add the Vercel URL to Auth → URL Configuration, and make sure the email template shows `{{ .Token }}` so people get a code rather than a link.

## Layout

- `src/lib/money.ts`: cents parsing and the preview total, with the desktop's rounding
- `src/lib/queue.ts`: the IndexedDB outbox and its flush
- `src/lib/invoices.ts`: "who owes me" sorting and totals
- `src/lib/supabase.ts`: the client and the uploader that the outbox uses
- `src/ui/`: one file per screen, plain DOM
- `sw/sw.js`: service worker template; the build fills in the file list
