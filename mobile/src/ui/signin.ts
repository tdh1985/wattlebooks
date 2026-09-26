import type { SupabaseClient } from '@supabase/supabase-js';
import { errorText, h, replace } from './dom';

const CODE = /^\d{6,10}$/;

export function signInScreen(client: SupabaseClient): HTMLElement {
  const root = h('main', { class: 'signin' });
  let email = '';

  const brand = () =>
    h(
      'div',
      { class: 'signin-brand' },
      h('img', { src: '/icons/icon-192.png', alt: '', width: 72, height: 72 }),
      h('h1', null, 'InvoiceDesk'),
      h('p', { class: 'muted' }, 'Snap receipts and check who owes you, from your phone.'),
    );

  function showEmail(message = '') {
    const input = h('input', {
      id: 'email',
      type: 'email',
      name: 'email',
      autocomplete: 'email',
      inputmode: 'email',
      required: true,
      value: email,
      placeholder: 'you@example.com',
    });
    const error = h('p', { class: 'error', role: 'alert' }, message);
    const button = h('button', { type: 'submit', class: 'primary' }, 'Send code');

    const form = h(
      'form',
      {
        class: 'card stack',
        onsubmit: async (e: Event) => {
          e.preventDefault();
          email = input.value.trim();
          if (!email) return;
          button.disabled = true;
          button.textContent = 'Sending...';
          const { error: err } = await client.auth.signInWithOtp({ email, options: { shouldCreateUser: true } });
          if (err) {
            button.disabled = false;
            button.textContent = 'Send code';
            error.textContent = errorText(err);
            return;
          }
          showCode();
        },
      },
      h('label', { for: 'email' }, 'Email'),
      input,
      h('p', { class: 'hint' }, 'Use the same email you signed in with on Settings → Phone in InvoiceDesk.'),
      error,
      button,
    );
    replace(root, brand(), form);
    input.focus();
  }

  function showCode() {
    const input = h('input', {
      id: 'code',
      type: 'text',
      name: 'code',
      inputmode: 'numeric',
      autocomplete: 'one-time-code',
      pattern: '[0-9]{6,10}',
      maxlength: 10,
      required: true,
      class: 'code-input',
    });
    const error = h('p', { class: 'error', role: 'alert' });
    const button = h('button', { type: 'submit', class: 'primary' }, 'Sign in');

    const form = h(
      'form',
      {
        class: 'card stack',
        onsubmit: async (e: Event) => {
          e.preventDefault();
          const token = input.value.replace(/\D/g, '');
          if (!CODE.test(token)) {
            error.textContent = 'The code is 6 to 10 digits.';
            return;
          }
          button.disabled = true;
          button.textContent = 'Checking...';
          const { error: err } = await client.auth.verifyOtp({ email, token, type: 'email' });
          if (err) {
            button.disabled = false;
            button.textContent = 'Sign in';
            error.textContent = errorText(err);
          }
          // success needs nothing here because the auth listener swaps screens
        },
      },
      h('label', { for: 'code' }, 'Code'),
      h('p', { class: 'hint' }, `We emailed a code to ${email}. If it isn't there, check your junk folder.`),
      input,
      error,
      button,
      h(
        'div',
        { class: 'row-links' },
        h('button', { type: 'button', class: 'link', onclick: () => showEmail() }, 'Use a different email'),
        h(
          'button',
          {
            type: 'button',
            class: 'link',
            onclick: async () => {
              const { error: err } = await client.auth.signInWithOtp({ email, options: { shouldCreateUser: true } });
              error.textContent = err ? errorText(err) : 'A new code is on its way.';
            },
          },
          'Send a new code',
        ),
      ),
    );
    replace(root, brand(), form);
    input.focus();
  }

  showEmail();
  return root;
}

export function notConfiguredScreen(): HTMLElement {
  return h(
    'main',
    { class: 'signin' },
    h(
      'div',
      { class: 'signin-brand' },
      h('img', { src: '/icons/icon-192.png', alt: '', width: 72, height: 72 }),
      h('h1', null, 'Not configured'),
    ),
    h(
      'div',
      { class: 'card stack' },
      h('p', null, 'This copy of the InvoiceDesk phone app has no Supabase project set.'),
      h(
        'p',
        { class: 'hint' },
        'Set VITE_SUPABASE_URL and VITE_SUPABASE_PUBLISHABLE_KEY, then build or deploy again.',
      ),
    ),
  );
}
