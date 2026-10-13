// Invite link check (ADR 0014): the published app (no built-in server) is opened with ?server=…; Ada creates a room
// and copies the invite link, Bea opens it (server and room already filled in) and joins, Ada starts, both see cards.
// Usage: start the server (bash tools/online/start-server.sh, http://localhost:5080), publish the client
// (dotnet publish src/Spincio.Client -c Release -o /tmp/pub), then
// `node tools/browser-checks/invite.mjs /tmp/pub/wwwroot [out-dir]`. It serves the app on http://localhost:5058
// (an origin the server allows) with the same fallback to index.html as GitHub Pages.
// Needs Playwright (preinstalled in the cloud environment under /opt/node22/lib/node_modules).
import { createRequire } from 'module';
import { createServer } from 'http';
import { readFile } from 'fs/promises';
import { join, extname } from 'path';
const require = createRequire('/opt/node22/lib/node_modules/');
const { chromium } = require('playwright');
const [root, out = '.'] = process.argv.slice(2);
const types = { '.html': 'text/html', '.js': 'text/javascript', '.css': 'text/css', '.json': 'application/json', '.wasm': 'application/wasm',
  '.webp': 'image/webp', '.svg': 'image/svg+xml', '.png': 'image/png', '.webmanifest': 'application/manifest+json', '.dat': 'application/octet-stream' };
const site = createServer(async (req, res) => {
  const path = decodeURIComponent(new URL(req.url, 'http://x').pathname);
  for (const file of [join(root, path), join(root, 'index.html')]) {
    try {
      const body = await readFile(file);
      res.writeHead(200, { 'content-type': types[extname(file)] ?? 'application/octet-stream' });
      return res.end(body);
    } catch { /* next candidate: SPA fallback */ }
  }
  res.writeHead(404).end();
}).listen(5058);

const problems = [];
const browser = await chromium.launch();
try {
  const ada = await browser.newPage({ viewport: { width: 390, height: 844 } });
  const bea = await browser.newPage({ viewport: { width: 390, height: 844 } });
  await ada.goto('http://localhost:5058/online?server=http://localhost:5080');
  await ada.getByPlaceholder('Nome').fill('Ada');
  await ada.getByRole('button', { name: 'Crea una stanza' }).click();
  await ada.waitForSelector('.invite-link');
  const invite = await ada.locator('.invite-link').inputValue();
  if (!/\/online\?server=.+&room=[A-Z0-9]{5}$/.test(invite)) problems.push(`odd invite link: ${invite}`);

  await bea.goto(invite);
  await bea.waitForSelector('input.code');
  const code = await bea.locator('input.code').inputValue();
  if (!invite.endsWith(code)) problems.push(`room code not filled in: "${code}"`);
  await bea.getByPlaceholder('Nome').fill('Bea');
  await bea.getByRole('button', { name: 'Entra' }).click();
  await ada.waitForFunction(() => document.body.innerText.includes('Bea'), null, { timeout: 15000 }).catch(() => problems.push('Ada does not see Bea in the room'));
  await ada.screenshot({ path: `${out}/invite-lobby.png` });
  await ada.getByRole('button', { name: 'Inizia la partita' }).click();
  for (const [name, page] of [['Ada', ada], ['Bea', bea]]) {
    await page.waitForSelector('.hand .card', { timeout: 20000 }).catch(() => problems.push(`${name} sees no cards`));
  }
  await bea.screenshot({ path: `${out}/invite-match.png` });
  console.log(JSON.stringify({ invite, code, problems }));
} finally {
  await browser.close();
  site.close();
}
process.exit(problems.length ? 1 : 0);
