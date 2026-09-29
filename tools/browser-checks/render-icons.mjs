// Renders src/Spincio.Client/Assets/icon.svg to the PWA icons.
// Usage: node tools/browser-checks/render-icons.mjs src/Spincio.Client/Assets/icon.svg src/Spincio.Client/wwwroot
import { createRequire } from 'module';
const require = createRequire('/opt/node22/lib/node_modules/');
const { chromium } = require('playwright');
const fs = require('fs');
const svg = fs.readFileSync(process.argv[2], 'utf8');
const browser = await chromium.launch();
for (const [size, file] of [[512,'icon-512.png'],[192,'icon-192.png'],[32,'favicon.png']]) {
  const page = await browser.newPage({ viewport: { width: size, height: size } });
  await page.setContent(`<html><body style="margin:0;background:transparent">${svg.replace('<svg ', `<svg width="${size}" height="${size}" `)}</body></html>`);
  await page.screenshot({ path: `${process.argv[3]}/${file}`, omitBackground: true });
}
await browser.close();
