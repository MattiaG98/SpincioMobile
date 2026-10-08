// CPU thinking time in the browser (WebAssembly), per level: animations off, so each CPU move takes the fixed
// pause (450 ms, LocalGameSession.DefaultCpuDelay) plus the bot's decision. Prints the decision time per move.
// Usage: serve the published wwwroot on http://localhost:8765, then `node tools/browser-checks/cpu-time.mjs`.
// Needs Playwright (preinstalled in the cloud environment under /opt/node22/lib/node_modules).
import { createRequire } from 'module';
const require = createRequire('/opt/node22/lib/node_modules/');
const { chromium } = require('playwright');
const PAUSE = 450;
const browser = await chromium.launch();
for (const level of ['Normale', 'Difficile']) {
  const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
  const errors = [];
  page.on('pageerror', e => errors.push(e.message));
  await page.goto('http://localhost:8765/?seed=21');
  await page.getByRole('button', { name: /Opzioni/ }).click();
  await page.getByRole('radio', { name: level, exact: true }).click();
  await page.getByRole('radio', { name: 'No', exact: true }).click();
  await page.getByRole('button', { name: 'Fatto' }).click();
  await page.getByText('Nuova partita').click();
  await page.waitForSelector('.hand .card');
  // Time between consecutive CPU moves, measured in the page (feed line changes while it is not my turn).
  await page.evaluate(() => {
    window.cpu = [];
    let last = performance.now(), text = document.querySelector('.feed summary')?.textContent;
    new MutationObserver(() => {
      const now = performance.now(), t = document.querySelector('.feed summary')?.textContent;
      if (t !== text) { if (!document.querySelector('.me.turn') && !document.querySelector('.overlay')) window.cpu.push(now - last); last = now; text = t; }
    }).observe(document.querySelector('.board'), { subtree: true, childList: true, characterData: true });
  });
  const t0 = Date.now();
  while (Date.now() - t0 < 90000) {
    if (await page.locator('.summary').count()) { await page.getByRole('button', { name: 'Continua', exact: true }).click(); continue; }
    if (await page.locator('#result-title').count()) break;
    const mine = page.locator('.hand .card.playable');
    if (await mine.count()) { await mine.first().click(); if (await page.locator('.choice').count()) await page.locator('.choice').first().click(); }
    await page.waitForTimeout(50);
  }
  const pause = PAUSE;
  const think = (await page.evaluate(() => window.cpu)).map(ms => Math.max(0, ms - pause)).sort((a, b) => a - b);
  const pct = p => Math.round(think[Math.min(think.length - 1, Math.floor(p * think.length))] ?? 0);
  console.log(`${level}: ${think.length} CPU moves, decision median ${pct(0.5)} ms, p90 ${pct(0.9)} ms, max ${pct(1)} ms`, errors.length ? errors : '');
  await page.close();
}
await browser.close();
