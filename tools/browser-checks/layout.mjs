// Layout check on a published build: on several phone screens, plays a match against the CPU and checks after every
// move that the page never scrolls, the table keeps its starting size and every table card stays inside it.
// Also screenshots a card in flight, to see it shrink towards the table.
// Usage: serve the published wwwroot on http://localhost:8765, then `node tools/browser-checks/layout.mjs <out-dir>`.
// Needs Playwright (preinstalled in the cloud environment under /opt/node22/lib/node_modules).
import { createRequire } from 'module';
const require = createRequire('/opt/node22/lib/node_modules/');
const { chromium } = require('playwright');
const out = process.argv[2] ?? '.';
const screens = [{ name: 'se', width: 375, height: 667 }, { name: '14', width: 390, height: 844 }, { name: 'max', width: 430, height: 932 }];
const browser = await chromium.launch();
let failures = 0;

for (const screen of screens) {
  const page = await browser.newPage({ viewport: { width: screen.width, height: screen.height }, deviceScaleFactor: 2 });
  const errors = [];
  page.on('pageerror', e => errors.push(e.message));
  await page.goto('http://localhost:8765/?seed=7');
  await page.waitForSelector('section.start');
  await page.getByText('Nuova partita').click();
  await page.waitForSelector('.hand .card');
  const start = await page.locator('.table').boundingBox();
  let maxTable = 0, moves = 0, flightShot = false;
  const problems = new Set();

  for (let i = 0; i < 600 && moves < 60; i++) {
    if (await page.locator('.summary').count()) { await page.getByText('Continua').click(); continue; }
    if (await page.locator('#result-title').count()) break;
    const state = await page.evaluate(() => {
      const app = document.querySelector('.app'), table = document.querySelector('.table');
      const t = table.getBoundingClientRect();
      const cards = [...table.querySelectorAll('.card')].map(c => c.getBoundingClientRect());
      const outside = cards.filter(c => c.left < t.left - 1 || c.right > t.right + 1 || c.top < t.top - 1 || c.bottom > t.bottom + 1).length;
      const hand = [...document.querySelectorAll('.hand .card')].map(c => c.getBoundingClientRect());
      return {
        pageScroll: document.documentElement.scrollHeight - innerHeight,
        appScroll: app.scrollHeight - app.clientHeight,
        table: { x: t.x, y: t.y, width: t.width, height: t.height }, count: cards.length, outside,
        handOff: hand.filter(h => h.bottom > innerHeight + 1).length,
      };
    });
    maxTable = Math.max(maxTable, state.count);
    if (state.pageScroll > 0 || state.appScroll > 0) problems.add(`scroll page=${state.pageScroll} app=${state.appScroll}`);
    if (Math.abs(state.table.height - start.height) > 1 || Math.abs(state.table.width - start.width) > 1) problems.add(`table resized ${JSON.stringify(state.table)} (${state.count} cards)`);
    if (state.outside) problems.add(`${state.outside} card(s) outside the table with ${state.count} on it`);
    if (state.handOff) problems.add('hand below the screen');

    const mine = await page.locator('.hand .card.playable').all();
    if (mine.length) {
      await mine[0].click();
      if (await page.locator('.choice').count()) await page.locator('.choice').first().click();
      moves++;
      if (!flightShot) {
        await page.waitForTimeout(150);
        await page.screenshot({ path: `${out}/layout-${screen.name}-flight.png` });
        flightShot = true;
      }
      if (moves === 10) await page.screenshot({ path: `${out}/layout-${screen.name}.png` });
    } else {
      await page.waitForTimeout(150);
    }
  }
  await page.screenshot({ path: `${out}/layout-${screen.name}-end.png` });

  // A crowded table (more cards than a match usually leaves there): copies of a table card, with the columns the app
  // would set for that count (Home.razor, TableFit). Every card must still fit the table.
  for (const n of [8, 12, 16]) {
    const fit = await page.evaluate(n => {
      const table = document.querySelector('.table');
      const sample = table.querySelector('.card') || document.querySelector('.hand .card');
      if (!sample) return null;
      const saved = [table.innerHTML, table.getAttribute('style')];
      table.innerHTML = '';
      for (let i = 0; i < n; i++) table.appendChild(sample.cloneNode(true));
      table.setAttribute('style', [1, 2, 3, 4].map(r => `--c${r}: ${Math.ceil(n / r)}`).join('; '));
      const t = table.getBoundingClientRect();
      const cards = [...table.children].map(c => c.getBoundingClientRect());
      const outside = cards.filter(c => c.left < t.left - 1 || c.right > t.right + 1 || c.top < t.top - 1 || c.bottom > t.bottom + 1).length;
      return { outside, width: Math.round(cards[0].width), restore: saved };
    }, n);
    if (!fit) continue;
    await page.screenshot({ path: `${out}/layout-${screen.name}-${n}cards.png` });
    await page.evaluate(([html, style]) => { const t = document.querySelector('.table'); t.innerHTML = html; t.setAttribute('style', style); }, fit.restore);
    console.log(`  ${n} cards on the table: ${fit.width}px wide`, fit.outside ? `PROBLEM: ${fit.outside} outside` : 'ok');
    if (fit.outside) failures++;
  }
  console.log(`${screen.name} ${screen.width}x${screen.height}: table ${Math.round(start.width)}x${Math.round(start.height)}, ${moves} moves, up to ${maxTable} table cards`,
    problems.size ? `PROBLEMS: ${[...problems].join('; ')}` : 'ok', errors.length ? `ERRORS: ${errors.join(' | ')}` : '');
  failures += problems.size + errors.length;
  await page.close();
}
await browser.close();
process.exit(failures ? 1 : 0);
