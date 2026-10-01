// Cuts the 40 cards out of the full-deck Piacentine scan (ADR 0011) and writes one WebP per card.
// Usage: node tools/card-slicer/slice.mjs <scan.jpg> <out-dir>
// The scan is a 10 × 4 grid: rows Denari, Coppe, Bastoni, Spade; columns A, 2..7, Fante, Cavallo, Re.
// In each grid cell the card's printed frame is found from dark-pixel projections, then a small white margin is kept.
// Needs Playwright (preinstalled in the cloud environment under /opt/node22/lib/node_modules).
import { createRequire } from 'module';
import { readFileSync, writeFileSync, mkdirSync } from 'fs';
const require = createRequire('/opt/node22/lib/node_modules/');
const { chromium } = require('playwright');

const [scan, outDir] = process.argv.slice(2);
const suits = ['D', 'C', 'B', 'S'];
const ranks = ['A', '2', '3', '4', '5', '6', '7', 'J', 'N', 'K'];
const W = 180, H = 330; // output size: the card's own proportions (frame plus white margin)

mkdirSync(outDir, { recursive: true });
const browser = await chromium.launch();
const page = await browser.newPage();
const dataUrl = 'data:image/jpeg;base64,' + readFileSync(scan).toString('base64');
const result = await page.evaluate(async ({ dataUrl, W, H }) => {
  const img = new Image();
  img.src = dataUrl;
  await img.decode();
  const src = document.createElement('canvas');
  src.width = img.width; src.height = img.height;
  const ctx = src.getContext('2d');
  ctx.drawImage(img, 0, 0);
  const cellW = img.width / 10, cellH = img.height / 4;
  const out = [];
  for (let row = 0; row < 4; row++) {
    for (let col = 0; col < 10; col++) {
      const x0 = Math.round(col * cellW), y0 = Math.round(row * cellH);
      const w = Math.round(cellW), h = Math.round(cellH);
      const px = ctx.getImageData(x0, y0, w, h).data;
      const dark = (x, y) => { const i = (y * w + x) * 4; return px[i] * 0.3 + px[i + 1] * 0.59 + px[i + 2] * 0.11 < 175; };
      // The printed frame is a thin line running almost the whole card: look for long dark columns and rows.
      const yA = Math.round(h * 0.15), yB = Math.round(h * 0.85), xA = Math.round(w * 0.15), xB = Math.round(w * 0.85);
      const colHits = x => { let n = 0; for (let y = yA; y < yB; y++) if (dark(x, y)) n++; return n / (yB - yA); };
      const rowHits = y => { let n = 0; for (let x = xA; x < xB; x++) if (dark(x, y)) n++; return n / (xB - xA); };
      const scan = (from, to, step, hits) => { for (let v = from; v !== to; v += step) if (hits(v) > 0.8) return v; return -1; };
      const l = scan(0, Math.round(w * 0.35), 1, colHits), r = scan(w - 1, Math.round(w * 0.65), -1, colHits);
      const t = scan(0, Math.round(h * 0.25), 1, rowHits), b = scan(h - 1, Math.round(h * 0.75), -1, rowHits);
      // Fallback: centre of the coloured artwork (pips and figures sit in the middle of the frame).
      let sx = 0, sy = 0, n = 0;
      for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
        const i = (y * w + x) * 4, mx = Math.max(px[i], px[i + 1], px[i + 2]), mn = Math.min(px[i], px[i + 1], px[i + 2]);
        if (mx - mn > 80) { sx += x; sy += y; n++; }
      }
      out.push({ row, col, x0, y0, l, r, t, b, cx: sx / n, cy: sy / n });
    }
  }
  // Frame size is the same on every card: take the median of the cards where both lines were found.
  const median = a => { const s = [...a].sort((p, q) => p - q); return s[Math.floor(s.length / 2)]; };
  const fw = median(out.filter(c => c.l >= 0 && c.r >= 0).map(c => c.r - c.l));
  const fh = median(out.filter(c => c.t >= 0 && c.b >= 0).map(c => c.b - c.t));
  const cards = [];
  const ok = (a, z, size) => a >= 0 && z >= 0 && Math.abs(z - a - size) <= 6;
  for (const c of out) {
    let { l, r, t, b } = c;
    if (!ok(l, r, fw)) { l = Math.round(c.cx - fw / 2); r = l + fw; }
    if (!ok(t, b, fh)) {
      // Cards in a scan row sit at about the same height: use the row's median top edge.
      const tops = out.filter(o => o.row === c.row && ok(o.t, o.b, fh)).map(o => o.t);
      t = tops.length ? median(tops) : Math.round(c.cy - fh / 2); b = t + fh;
    }
    // Keep a white margin around the printed frame, like the real card.
    const mx = Math.round(fw * 0.07), my = Math.round(fh * 0.035);
    const box = { x: c.x0 + l - mx, y: c.y0 + t - my, w: fw + 2 * mx, h: fh + 2 * my, found: [c.l, c.r, c.t, c.b].map(v => v >= 0 ? 1 : 0).join('') };
      const dst = document.createElement('canvas');
      dst.width = W; dst.height = H;
      const d = dst.getContext('2d');
      d.fillStyle = '#fff'; d.fillRect(0, 0, W, H);
      d.imageSmoothingQuality = 'high';
      d.drawImage(src, box.x, box.y, box.w, box.h, 0, 0, W, H);
      cards.push({ row: c.row, col: c.col, box, webp: dst.toDataURL('image/webp', 0.86) });
  }
  return { fw, fh, cards };
}, { dataUrl, W, H });

console.log('frame', result.fw, 'x', result.fh);
for (const card of result.cards) {
  const name = `${ranks[card.col]}${suits[card.row]}`;
  writeFileSync(`${outDir}/${name}.webp`, Buffer.from(card.webp.split(',')[1], 'base64'));
  console.log(name, JSON.stringify(card.box));
}
await browser.close();
