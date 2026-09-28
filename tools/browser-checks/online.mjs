// Online check: two players in two browsers create/join a room, start and play a few moves.
// Usage: run the server (port 5080) and the client in Development (port 5058), then
// `node tools/browser-checks/online.mjs <out-dir>`.
import { createRequire } from 'module';
const require = createRequire('/opt/node22/lib/node_modules/');
const { chromium } = require('playwright');
const out = process.argv[2] ?? '.';
const browser = await chromium.launch();
const errors = [];
async function player() {
  const ctx = await browser.newContext({ viewport: { width: 390, height: 844 }, deviceScaleFactor: 2 });
  const page = await ctx.newPage();
  page.on('pageerror', e => errors.push(e.message));
  page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
  return page;
}
const ada = await player(), bea = await player();
await ada.goto('http://localhost:5058/');
await ada.getByRole('link', { name: 'Gioca online con gli amici' }).click();
await ada.getByPlaceholder('Nome').fill('Ada');
await ada.getByRole('button', { name: 'Crea una stanza' }).click();
await ada.waitForSelector('.room-code');
const code = (await ada.locator('.room-code').innerText()).trim();
await bea.goto('http://localhost:5058/online');
await bea.getByPlaceholder('Nome').fill('Bea');
await bea.getByPlaceholder('ABCDE').fill(code);
await bea.getByRole('button', { name: 'Entra' }).click();
await bea.waitForSelector('.room-code');
await ada.waitForTimeout(500);
await ada.screenshot({ path: `${out}/m7-lobby.png` });
await ada.getByRole('button', { name: 'Inizia la partita' }).click();
await ada.waitForSelector('.hand .card', { timeout: 20000 });
await bea.waitForSelector('.hand .card', { timeout: 20000 });
let moves = 0;
for (let i = 0; i < 300 && moves < 6; i++) {
  for (const p of [ada, bea]) {
    if (await p.locator('.summary').count()) await p.getByText('Continua').click();
    if (await p.locator('.me.turn').count()) {
      const declare = p.locator('button.declare');
      if (await declare.count()) { await declare.click(); await p.waitForTimeout(300); continue; }
      const card = p.locator('.hand .card.playable').first();
      if (await card.count()) { await card.click(); if (await p.locator('.choice').count()) await p.locator('.choice').first().click(); moves++; await p.waitForTimeout(400); }
    }
  }
  await ada.waitForTimeout(100);
}
await ada.screenshot({ path: `${out}/m7-game-ada.png` });
await bea.screenshot({ path: `${out}/m7-game-bea.png` });
const adaHand = await ada.locator('.hand .card').evaluateAll(els => els.map(e => e.getAttribute('aria-label')));
const beaHand = await bea.locator('.hand .card').evaluateAll(els => els.map(e => e.getAttribute('aria-label')));
const footer = await ada.locator('.footer').innerText();
console.log(JSON.stringify({ code, moves, adaHand, beaHand, overlap: adaHand.filter(c => beaHand.includes(c)), footer, errors }));
await browser.close();
