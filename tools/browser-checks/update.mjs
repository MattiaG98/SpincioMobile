// Update check: an installed app must offer the new version and switch to it when the player taps "Aggiorna".
// Usage: publish two builds with different versions, e.g.
//   dotnet publish src/Spincio.Client -c Release -o /tmp/pubA
//   dotnet publish src/Spincio.Client -c Release -o /tmp/pubB -p:Version=9.9.9
// then `node tools/browser-checks/update.mjs /tmp/pubA/wwwroot /tmp/pubB/wwwroot 9.9.9 [out-dir]`.
// It serves /tmp/update-site (a symlink switched from A to B) on http://localhost:8766.
// Needs Playwright (preinstalled in the cloud environment under /opt/node22/lib/node_modules).
import { createRequire } from 'module';
import { spawn } from 'child_process';
import { symlinkSync, rmSync } from 'fs';
const require = createRequire('/opt/node22/lib/node_modules/');
const { chromium } = require('playwright');
const [siteA, siteB, versionB, out = '.'] = process.argv.slice(2);
const site = '/tmp/update-site';
const link = target => { rmSync(site, { force: true }); symlinkSync(target, site); };
link(siteA);
const server = spawn('python3', ['-m', 'http.server', '8766', '--directory', site], { stdio: 'ignore' });
await new Promise(r => setTimeout(r, 800));
const problems = [];
const browser = await chromium.launch();
try {
  const page = await browser.newPage({ viewport: { width: 390, height: 844 } });
  await page.goto('http://localhost:8766/');
  await page.waitForSelector('section.start');
  await page.waitForFunction(() => navigator.serviceWorker.ready.then(() => true));
  await page.reload(); // from now on the installed version (A) serves the app, as on the phone
  await page.waitForSelector('section.start');
  const before = await page.locator('section.start .app-version').innerText();
  if (!(await page.evaluate(() => !!navigator.serviceWorker.controller))) problems.push('version A is not installed');

  link(siteB); // version B is published
  await page.evaluate(() => document.dispatchEvent(new Event('visibilitychange'))); // the app comes back to the foreground
  await page.waitForSelector('#update-bar:not([hidden])', { timeout: 30000 }).catch(() => problems.push('no update bar'));
  await page.screenshot({ path: `${out}/update-bar.png` });
  await Promise.all([page.waitForEvent('load', { timeout: 30000 }), page.locator('#update-bar .update-now').click()]);
  await page.waitForSelector('section.start');
  const after = await page.locator('section.start .app-version').innerText();
  if (after !== `v${versionB}`) problems.push(`after the update the app shows ${after}, expected v${versionB}`);
  console.log(JSON.stringify({ before, after, problems }));
} finally {
  await browser.close();
  server.kill();
  rmSync(site, { force: true });
}
process.exit(problems.length ? 1 : 0);
