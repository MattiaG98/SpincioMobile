// Online players and invitations check (ADR 0015) on the published app, three players on three "devices":
// Ada, Bea and Cleo open the online page on the same server and write their names; Bea goes back to the main menu.
// Ada sees both in "Giocatori online" and invites Cleo, who declines (Ada reads "Ha rifiutato"), then Bea, whose
// invitation shows at the top of the main menu: she accepts, plays against Ada, lands in Ada's room on the other team;
// Ada reads "Ha accettato", starts, both see cards. Screenshots the list and the invitation bar.
// Usage: start the server (bash tools/online/start-server.sh, http://localhost:5080), publish the client
// (dotnet publish src/Spincio.Client -c Release -o /tmp/pub), then
// `node tools/browser-checks/lobby.mjs /tmp/pub/wwwroot [out-dir]`. It serves the app on http://localhost:5058.
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
const errors = [];
const check = async (what, promise) => { try { await promise; } catch { problems.push(what); } };
const browser = await chromium.launch();
try {
  // newPage() opens a new context each time: three devices, each with its own player id.
  const open = async name => {
    const page = await browser.newPage({ viewport: { width: 390, height: 844 }, deviceScaleFactor: 2 });
    page.on('pageerror', e => errors.push(`${name}: ${e.message}`));
    await page.goto('http://localhost:5058/online?server=http://localhost:5080');
    await page.getByPlaceholder('Nome').fill(name);
    await page.getByPlaceholder('Nome').press('Tab'); // the name is saved on change
    return page;
  };
  const ada = await open('Ada'), bea = await open('Bea'), cleo = await open('Cleo');
  await bea.getByText('Torna al gioco offline').click();
  await bea.waitForSelector('section.menu-screen');

  await check('Ada does not see Bea and Cleo online', ada.waitForFunction(
    () => [...document.querySelectorAll('.online-players .player-name')].map(n => n.textContent).join() === 'Bea,Cleo', null, { timeout: 15000 }));
  await ada.screenshot({ path: `${out}/lobby-list.png` });

  // Cleo declines.
  await ada.locator('.online-players li', { hasText: 'Cleo' }).getByRole('button', { name: 'Invita' }).click();
  await check('no room after the first invitation', ada.waitForSelector('.room-code'));
  await check('Cleo gets no invitation', cleo.waitForSelector('.invite-banner', { timeout: 10000 }));
  await cleo.getByRole('button', { name: 'Rifiuta' }).click();
  await check('Ada does not read that Cleo declined', ada.locator('.online-players li', { hasText: 'Cleo' }).getByText('Ha rifiutato').waitFor({ timeout: 10000 }));

  // Bea, on the main menu, accepts and plays against Ada.
  await ada.locator('.online-players li', { hasText: 'Bea' }).getByRole('button', { name: 'Invita' }).click();
  await check('Bea gets no invitation on the main menu', bea.waitForSelector('.invite-banner', { timeout: 10000 }));
  await bea.waitForTimeout(400); // the bar slides in
  await bea.screenshot({ path: `${out}/lobby-invitation.png` });
  const box = await bea.locator('.invite-banner').boundingBox();
  if (!box || box.y < 0 || box.x < 0 || box.x + box.width > 390 || box.y + box.height > 844 / 2) problems.push(`invitation bar out of place: ${JSON.stringify(box)}`);
  await bea.getByRole('button', { name: 'Accetta' }).click();
  const choices = await bea.locator('.invite-actions button').allInnerTexts();
  if (choices.join() !== 'Con Ada,Contro Ada,Rifiuta') problems.push(`choices: ${choices.join()}`);
  await bea.screenshot({ path: `${out}/lobby-choice.png` });
  await bea.getByRole('button', { name: 'Contro Ada' }).click();
  await check('Bea is not taken to the room', bea.waitForSelector('.room-code', { timeout: 10000 }));
  const beaSeats = await bea.locator('.seats li').allInnerTexts();
  if (!beaSeats.some(s => s.includes('Squadra B') && s.includes('Bea') && s.includes('(tu)'))) problems.push(`Bea not on the other team: ${beaSeats.join(' | ')}`);
  if (await bea.locator('.invite-banner').count()) problems.push('the invitation bar is still there after accepting');
  await check('Ada does not read that Bea accepted', ada.locator('.online-players li', { hasText: 'Bea' }).getByText('Ha accettato').waitFor({ timeout: 10000 }));
  await ada.screenshot({ path: `${out}/lobby-room.png` });

  await ada.getByRole('button', { name: 'Inizia la partita' }).click();
  for (const [name, page] of [['Ada', ada], ['Bea', bea]]) {
    await check(`${name} sees no cards`, page.waitForSelector('.hand .card', { timeout: 20000 }));
  }
  await bea.screenshot({ path: `${out}/lobby-match.png` });
  console.log(JSON.stringify({ choices, beaSeats, problems, errors }));
} finally {
  await browser.close();
  site.close();
}
process.exit(problems.length || errors.length ? 1 : 0);
