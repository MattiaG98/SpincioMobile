import { createRequire } from 'module';
const require = createRequire('/opt/node22/lib/node_modules/');
const { chromium } = require('playwright');
const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
const errors = [];
page.on('pageerror', e => errors.push(e.message));
page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
// Plays 30 of my turns and checks that, whenever it is my turn, no card is still flying or hidden.
// Usage: serve the published wwwroot on http://localhost:8765, then `node tools/browser-checks/animations.mjs`.
await page.goto('http://localhost:8765/?seed=5');
await page.getByText('Nuova partita').click();
const checks = [];
const t0 = Date.now();
while (checks.length < 30 && Date.now() - t0 < 240000) {
  await page.waitForSelector('.hand .card.playable:visible, .overlay button:has-text("Continua")', { timeout: 60000 });
  if (await page.locator('.overlay button:has-text("Continua")').count()) { await page.locator('.overlay button:has-text("Continua")').click(); continue; }
  await page.waitForTimeout(300); // my turn: nothing should be flying or hidden
  checks.push(await page.evaluate(() => ({ fly: document.querySelectorAll('.fly-card').length, hidden: [...document.querySelectorAll('.card')].filter(e => e.style.visibility === 'hidden').length })));
  await page.locator('.hand .card.playable').first().click();
  if (await page.locator('.choice').count()) await page.locator('.choice').first().click();
}
console.log(JSON.stringify({ bad: checks.filter(c => c.fly || c.hidden), myTurns: checks.length, seconds: Math.round((Date.now() - t0) / 1000), errors }));
await browser.close();
