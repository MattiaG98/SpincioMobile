// Points animation check on a published build: plays a round and watches the "+N" pops (js/moves.js, points()).
// Checks that every pop lands before its score changes, that the end-of-round points fly in only when the summary
// closes, and that the score bar then shows the summary's match score. Screenshots a pop of each kind.
// Usage: serve the published wwwroot on http://localhost:8765, then `node tools/browser-checks/points.mjs <out-dir>`.
// Needs Playwright (preinstalled in the cloud environment under /opt/node22/lib/node_modules).
import { createRequire } from 'module';
const require = createRequire('/opt/node22/lib/node_modules/');
const { chromium } = require('playwright');
const out = process.argv[2] ?? '.';
const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 390, height: 844 }, deviceScaleFactor: 2 });
const errors = [];
page.on('pageerror', e => errors.push(e.message));
page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
const problems = [];

await page.goto('http://localhost:8765/?seed=11');
await page.getByText('Nuova partita').click();
await page.waitForSelector('.hand .card');
// Every pop, with the score bar when it appears and when it is removed (it must not have changed by then).
await page.evaluate(() => {
  window.pops = [];
  const bar = () => [...document.querySelectorAll('.scorebar .score strong')].map(s => s.textContent).join('-');
  new MutationObserver(records => {
    for (const r of records) {
      for (const n of r.addedNodes) if (n.classList?.contains('points-pop')) window.pops.push({ cls: n.className, text: n.innerText.replace(/\n/g, ' '), barIn: bar() });
      for (const n of r.removedNodes) if (n.classList?.contains('points-pop')) { const p = window.pops.findLast(p => p.cls === n.className && !p.barOut); if (p) p.barOut = bar(); }
    }
  }).observe(document.body, { childList: true, subtree: true });
});

const shot = {};
let summaryChecked = false;
const t0 = Date.now();
while (!summaryChecked && Date.now() - t0 < 300000) {
  const kind = await page.evaluate(() => document.querySelector('.points-pop')?.className.split(' ').pop());
  if (kind && !shot[kind]) { shot[kind] = true; await page.screenshot({ path: `${out}/points-${kind}.png` }); }
  if (await page.locator('.summary').count()) {
    const before = await page.evaluate(() => [...document.querySelectorAll('.scorebar .score strong')].map(s => s.textContent).join('-'));
    const total = await page.locator('.summary tr.total').last().locator('td').allInnerTexts(); // "Punteggio partita", Noi, Loro
    await page.getByRole('button', { name: 'Continua', exact: true }).click();
    await page.waitForSelector('.points-pop.roundend', { timeout: 5000 }).catch(() => problems.push('no end-of-round pop'));
    await page.waitForTimeout(250);
    await page.screenshot({ path: `${out}/points-roundend.png` });
    await page.waitForFunction(() => !document.querySelector('.points-pop'), null, { timeout: 5000 });
    await page.waitForTimeout(100);
    const after = await page.evaluate(() => [...document.querySelectorAll('.scorebar .score strong')].map(s => s.textContent).join('-'));
    if (before === after) problems.push(`score bar unchanged after the summary (${before})`);
    if (after !== `${total[1]}-${total[2]}`) problems.push(`score bar ${after}, summary says ${total[1]}-${total[2]}`);
    summaryChecked = true;
    break;
  }
  if (await page.locator('.declare').count()) { await page.locator('.declare').click(); continue; }
  const mine = page.locator('.hand .card.playable');
  if (await mine.count()) {
    await mine.first().click();
    if (await page.locator('.choice').count()) await page.locator('.choice').first().click();
  }
  await page.waitForTimeout(60);
}

const pops = await page.evaluate(() => window.pops);
for (const p of pops) if (p.barOut && p.barOut !== p.barIn) problems.push(`score changed while "${p.text}" was flying (${p.barIn} → ${p.barOut})`);
if (!summaryChecked) problems.push('no round summary within 5 minutes');
console.log(JSON.stringify({ pops: pops.map(p => `${p.cls.replace('points-pop ', '')}: ${p.text}`), problems, errors }, null, 1));
await browser.close();
process.exit(problems.length || errors.length ? 1 : 0);
