import './styles.css';
import type { Session } from '@supabase/supabase-js';
import { App, clearSnapshot } from './app';
import { supabase } from './lib/supabase';
import { h, replace } from './ui/dom';
import { owedScreen } from './ui/owed';
import { quickScreen } from './ui/quick';
import { notConfiguredScreen, signInScreen } from './ui/signin';
import { snapScreen } from './ui/snap';

type Tab = 'snap' | 'owed' | 'invoice';

const TABS: { id: Tab; label: string; icon: string }[] = [
  {
    id: 'snap',
    label: 'Snap receipt',
    icon: '<path d="M4 8h3l1.6-2.4h6.8L17 8h3a1 1 0 0 1 1 1v9a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V9a1 1 0 0 1 1-1z"/><circle cx="12" cy="13" r="3.4"/>',
  },
  {
    id: 'owed',
    label: 'Who owes me',
    icon: '<circle cx="12" cy="12" r="8.5"/><path d="M14.6 9.2c-.5-.9-1.5-1.4-2.6-1.4-1.5 0-2.6.8-2.6 2s1.1 1.7 2.6 2.1 2.7.9 2.7 2.2-1.2 2.1-2.7 2.1c-1.2 0-2.3-.6-2.8-1.6M12 6.3v1.5m0 8.5v1.5"/>',
  },
  {
    id: 'invoice',
    label: 'Quick invoice',
    icon: '<path d="M7 3.5h7.5L19 8v12.5H7z"/><path d="M14.5 3.5V8H19M10 12h6m-6 3.5h6"/>',
  },
];

const root = document.getElementById('app')!;
let current: { app: App; userId: string; dispose: () => void } | null = null;

function tabFromHash(): Tab {
  const id = location.hash.slice(1);
  return TABS.some((t) => t.id === id) ? (id as Tab) : 'snap';
}

function icon(paths: string): SVGElement {
  const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
  svg.setAttribute('viewBox', '0 0 24 24');
  svg.setAttribute('aria-hidden', 'true');
  svg.innerHTML = paths;
  return svg;
}

function showApp(session: Session) {
  const client = supabase!;
  const app = new App(client, session.user.id, session.user.email ?? '');

  const quick = quickScreen(app);
  const owed = owedScreen(app, (snapshot) => quick.update(snapshot));
  const screens: Record<Tab, HTMLElement> = { snap: snapScreen(app), owed: owed.el, invoice: quick.el };

  const pending = h('button', { type: 'button', class: 'pending', hidden: true, onclick: () => void app.flush() });
  const main = h('main', { class: 'content' });
  const tabButtons = TABS.map((t) =>
    h(
      'a',
      { href: `#${t.id}`, class: 'tab', 'data-tab': t.id },
      icon(t.icon),
      h('span', null, t.label),
    ),
  );

  const signOut = h(
    'button',
    {
      type: 'button',
      class: 'link small',
      onclick: async () => {
        const waiting = await app.pendingCount();
        if (waiting > 0 && !confirm(`${waiting} not uploaded yet. They stay on this phone until you sign in again. Sign out?`)) return;
        clearSnapshot();
        await client.auth.signOut();
      },
    },
    'Sign out',
  );

  replace(
    root,
    h('header', { class: 'topbar' }, h('span', { class: 'wordmark' }, 'InvoiceDesk'), pending),
    main,
    h('footer', { class: 'account' }, h('span', null, app.email), ' · ', signOut),
    h('nav', { class: 'tabbar', 'aria-label': 'Sections' }, tabButtons),
  );

  function showTab() {
    const tab = tabFromHash();
    replace(main, screens[tab]);
    for (const b of tabButtons) {
      const active = b.dataset.tab === tab;
      b.classList.toggle('active', active);
      if (active) b.setAttribute('aria-current', 'page');
      else b.removeAttribute('aria-current');
    }
    if (tab === 'owed') owed.refresh();
    window.scrollTo(0, 0);
  }

  const setPending = (count: number) => {
    pending.hidden = count === 0;
    pending.textContent = `${count} waiting to upload`;
  };
  const updatePending = () => void app.pendingCount().then(setPending);

  const flush = () => void app.flush();
  const onVisible = () => {
    if (document.visibilityState === 'visible') flush();
  };
  const unlisten = app.outbox.onChange(updatePending);
  window.addEventListener('hashchange', showTab);
  window.addEventListener('online', flush);
  document.addEventListener('visibilitychange', onVisible);

  showTab();
  updatePending();
  flush();

  // fetch once so quick invoice has clients even if who owes me is never opened
  if (tabFromHash() !== 'owed') void app.fetchSnapshot().then(quick.update, () => undefined);

  current = {
    app,
    userId: session.user.id,
    dispose() {
      unlisten();
      window.removeEventListener('hashchange', showTab);
      window.removeEventListener('online', flush);
      document.removeEventListener('visibilitychange', onVisible);
      app.outbox.close();
    },
  };
}

function render(session: Session | null) {
  if (session && current?.userId === session.user.id) return;
  current?.dispose();
  current = null;
  if (session) showApp(session);
  else replace(root, signInScreen(supabase!));
}

if (!supabase) {
  replace(root, notConfiguredScreen());
} else {
  // the listener fires with the stored session first so it also covers start up
  supabase.auth.onAuthStateChange((_event, session) => {
    // supabase-js warns against awaiting inside this callback
    setTimeout(() => render(session), 0);
  });
}

if ('serviceWorker' in navigator && import.meta.env.PROD) {
  window.addEventListener('load', () => {
    void navigator.serviceWorker.register('/sw.js');
  });
}
