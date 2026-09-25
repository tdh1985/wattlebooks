import { createHash } from 'node:crypto';
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, relative, sep } from 'node:path';
import { defineConfig, type Plugin } from 'vitest/config';

function publicFiles(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) return publicFiles(full);
    return name.endsWith('.txt') ? [] : ['/' + relative('public', full).split(sep).join('/')];
  });
}

// writes sw.js with this build's file names so the shell works offline
function serviceWorker(): Plugin {
  return {
    name: 'invoicedesk-sw',
    apply: 'build',
    generateBundle(_options, bundle) {
      const built = Object.keys(bundle).map((file) => '/' + file);
      // addAll rejects the whole list if one url appears twice
      const shell = [...new Set(['/', '/index.html', ...built, ...publicFiles('public')])];
      const version = createHash('sha256').update(shell.join('\n')).digest('hex').slice(0, 12);
      const source = readFileSync('sw/sw.js', 'utf8')
        .replace('__VERSION__', version)
        .replace('__SHELL__', JSON.stringify(shell));
      this.emitFile({ type: 'asset', fileName: 'sw.js', source });
    },
  };
}

export default defineConfig({
  plugins: [serviceWorker()],
  test: {
    environment: 'node',
    include: ['test/**/*.test.ts'],
  },
});
