// Server restart check (ADR 0017) on the published app: Ada starts an online match against 3 CPUs; at her turn the
// server process is killed and a new one is started with the same key. The app must reconnect by itself, show the
// same hand and let her play on. Screenshots before and after.
// Usage: dotnet publish src/Spincio.Server -c Release -o /tmp/srv and dotnet publish src/Spincio.Client -c Release
// -o /tmp/pub, then `node tools/browser-checks/restart.mjs /tmp/pub/wwwroot /tmp/srv/Spincio.Server.dll [out-dir]`.
// It serves the app on http://localhost:5058 and runs the server on http://localhost:5080 (nothing else may use them).
// Needs Playwright (preinstalled in the cloud environment under /opt/node22/lib/node_modules).
import { createRequire } from 'module';
import { createServer } from 'http';
import { spawn } from 'child_process';
import { readFile } from 'fs/promises';
import { join, extname, dirname } from 'path';
const require = createRequire('/opt/node22/lib/node_modules/');
const { chromium } = require('playwright');
const [root, serverDll, out = '.'] = process.argv.slice(2);
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

const startServer = () => spawn('dotnet', [serverDll, '--urls', 'http://0.0.0.0:5080'], {
  cwd: dirname(serverDll), // its appsettings.json (allowed origins) is read from the working folder
  env: { ...process.env, Rooms__CheckpointKey: 'browser-check-key', Rooms__BotDelay: '00:00:00.200' }, stdio: 'ignore' });
const healthy = async () => {
  for (let i = 0; i < 60; i++) {
    try { if ((await fetch('http://localhost:5080/health')).ok) return true; } catch { /* not yet */ }
    await new Promise(r => setTimeout(r, 500));
  }
  return false;
};
const hand = page => page.locator('.hand .card').evaluateAll(cards => cards.map(c => c.getAttribute('aria-label')).join());

const problems = [];
const errors = [];
let server = startServer();
const browser = await chromium.launch();
try {
  if (!await healthy()) throw new Error('server did not start');
  const ada = await browser.newPage({ viewport: { width: 390, height: 844 } });
  ada.on('pageerror', e => errors.push(e.message));
  await ada.goto('http://localhost:5058/online?server=http://localhost:5080');
  await ada.getByPlaceholder('Nome').fill('Ada');
  await ada.getByRole('button', { name: 'Crea una stanza' }).click();
  await ada.getByRole('button', { name: 'Inizia la partita' }).click();
  await ada.waitForSelector('.hand .card.playable', { timeout: 30000 });
  const before = await hand(ada);
  await ada.screenshot({ path: `${out}/restart-before.png` });

  // Every connection to the game hub, with the frames it received: a new one after the restart is the reconnection.
  const sockets = [];
  ada.on('websocket', ws => {
    if (!ws.url().includes('/hubs/game')) return;
    const socket = { frames: 0 };
    sockets.push(socket);
    ws.on('framereceived', () => socket.frames++);
  });

  const t0 = Date.now();
  server.kill('SIGKILL'); // the server goes away with the room in its memory
  await new Promise(r => setTimeout(r, 1000));
  server = startServer();
  if (!await healthy()) throw new Error('second server did not start');

  // The app reconnects by itself (SignalR retries after 0, 2, 10, 30 s, then every 5 s) and hands back its restore
  // point: the new connection receives the handshake, the room and the snapshot of the rebuilt match.
  for (let i = 0; i < 180 && !sockets.some(s => s.frames >= 3); i++) await ada.waitForTimeout(500);
  const secondsToReconnect = Math.round((Date.now() - t0) / 1000);
  if (!sockets.some(s => s.frames >= 3)) problems.push('no new connection to the game after the restart');
  await ada.waitForTimeout(500);
  const after = await hand(ada);
  if (after !== before) problems.push(`hand after the restart "${after}" instead of "${before}"`);

  // The rebuilt match goes on: Ada's card leaves her hand only if the new server accepts the move.
  await ada.locator('.hand .card.playable').first().click();
  if (await ada.locator('.choice').count()) await ada.locator('.choice').first().click();
  await ada.waitForFunction(b => [...document.querySelectorAll('.hand .card')].map(c => c.getAttribute('aria-label')).join() !== b,
    before, { timeout: 15000 }).catch(() => problems.push('the move after the restart was not accepted'));
  await ada.screenshot({ path: `${out}/restart-after.png` });
  console.log(JSON.stringify({ before, after, secondsToReconnect, problems, errors }));
} finally {
  await browser.close();
  server.kill('SIGKILL');
  site.close();
}
process.exit(problems.length || errors.length ? 1 : 0);
