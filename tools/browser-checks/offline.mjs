// Offline check on a published build: plays the "Difficile" level, times CPU moves, takes screenshots.
// Usage: serve the published wwwroot on http://localhost:8765, then `node tools/browser-checks/offline.mjs <out-dir>`.
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
await page.goto('http://localhost:8765/?seed=3');
await page.waitForSelector('section.start');
await page.screenshot({ path: `${out}/m5-start.png` });
await page.getByLabel(/Difficile/).check();
await page.getByText('Nuova partita').click();
await page.waitForSelector('.hand .card');
// Measure CPU move intervals: time between consecutive feed changes while it is not my turn.
const intervals = [];
let last = await page.locator('.feed').innerText(), lastT = Date.now();
let plays = 0, shots = 0, sweeps = 0;
for (let i = 0; i < 400 && plays < 25; i++) {
  if (await page.locator('.summary').count()) { await page.getByText('Continua').click(); continue; }
  if (await page.locator('#result-title').count()) break;
  const feed = await page.locator('.feed').innerText();
  if (feed !== last) { const now = Date.now(); if (!(await page.locator('.me.turn').count())) intervals.push(now - lastT); last = feed; lastT = now; }
  if (await page.locator('.sweep-flash').count() && sweeps === 0) { sweeps++; await page.screenshot({ path: `${out}/m6-sweep.png` }); }
  if (await page.locator('.me.turn').count()) {
    if (shots === 0) { shots++; await page.screenshot({ path: `${out}/m6-table.png` }); }
    const declare = page.locator('button.declare');
    if (await declare.count()) { await declare.click(); continue; }
    const card = page.locator('.hand .card.playable').first();
    if (await card.count()) { await card.click(); if (await page.locator('.choice').count()) await page.locator('.choice').first().click(); plays++; lastT = Date.now(); last = await page.locator('.feed').innerText(); }
  }
  await page.waitForTimeout(50);
}
intervals.sort((a,b)=>a-b);
const med = intervals[Math.floor(intervals.length/2)], max = intervals[intervals.length-1];
console.log(JSON.stringify({ plays, cpuMoves: intervals.length, medianMs: med, maxMs: max, p90: intervals[Math.floor(intervals.length*0.9)], errors }));
await browser.close();
