// the landing page is static on github pages, so the contact form posts here

const LIMITS = { name: 100, email: 200, message: 5000 };

export default {
  async fetch(request, env) {
    const origin = request.headers.get('Origin') ?? '';
    const allowed = list(env.ALLOWED_ORIGINS);
    const cors = allowed.has(origin)
      ? { 'Access-Control-Allow-Origin': origin, 'Vary': 'Origin' }
      : {};

    if (request.method === 'OPTIONS') {
      return new Response(null, {
        status: 204,
        headers: { ...cors, 'Access-Control-Allow-Methods': 'POST', 'Access-Control-Allow-Headers': 'Content-Type', 'Access-Control-Max-Age': '86400' },
      });
    }
    if (request.method !== 'POST' || new URL(request.url).pathname !== '/') {
      return reply(404, 'Not found', cors);
    }
    if (!allowed.has(origin)) return reply(403, 'Forbidden', cors);

    let form;
    try {
      form = await request.formData();
    } catch {
      return reply(400, 'Send the form again.', cors);
    }

    // bots fill every field, people never see this one
    if (form.get('website')) return reply(200, 'ok', cors);

    const name = text(form.get('name'));
    const email = text(form.get('email'));
    const message = text(form.get('message'));
    if (!name || !email || !message) return reply(400, 'Fill in your name, email and message.', cors);
    if (name.length > LIMITS.name || email.length > LIMITS.email || message.length > LIMITS.message) {
      return reply(400, 'That message is too long.', cors);
    }
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) return reply(400, 'Check your email address.', cors);

    const ok = await verifyTurnstile(form.get('cf-turnstile-response'), request, env);
    if (!ok) return reply(403, "We couldn't confirm you're not a bot. Try again.", cors);

    const sent = await fetch('https://api.resend.com/emails', {
      method: 'POST',
      headers: { Authorization: `Bearer ${env.RESEND_API_KEY}`, 'Content-Type': 'application/json' },
      signal: AbortSignal.timeout(10_000),
      body: JSON.stringify({
        from: env.CONTACT_FROM,
        to: [env.CONTACT_TO],
        reply_to: email,
        subject: `InvoiceDesk message from ${name.replace(/[\r\n]+/g, ' ')}`,
        text: `${message}\n\n--\n${name} <${email}>`,
      }),
    }).catch(() => null);

    if (!sent?.ok) return reply(502, "Your message didn't send. Try again in a minute.", cors);
    return reply(200, 'ok', cors);
  },
};

async function verifyTurnstile(token, request, env) {
  const hostnames = list(env.TURNSTILE_HOSTNAMES);
  if (typeof token !== 'string' || token.length === 0 || token.length > 2048 || hostnames.size === 0) return false;
  try {
    const r = await fetch('https://challenges.cloudflare.com/turnstile/v0/siteverify', {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      signal: AbortSignal.timeout(10_000),
      body: new URLSearchParams({
        secret: env.TURNSTILE_SECRET,
        response: token,
        remoteip: request.headers.get('CF-Connecting-IP') ?? '',
      }),
    });
    if (!r.ok) return false;
    const result = await r.json();
    return result.success === true && result.action === 'contact' && hostnames.has(result.hostname);
  } catch {
    // fail closed if siteverify is down or slow
    return false;
  }
}

function text(value) {
  return typeof value === 'string' ? value.trim() : '';
}

function list(value) {
  return new Set((value ?? '').split(',').map((s) => s.trim()).filter(Boolean));
}

function reply(status, message, cors) {
  return new Response(JSON.stringify({ ok: status === 200, message }), {
    status,
    headers: { ...cors, 'Content-Type': 'application/json' },
  });
}
