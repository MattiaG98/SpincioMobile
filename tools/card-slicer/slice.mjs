// Cuts the 40 cards out of the full-deck Piacentine scan (ADR 0011) and writes one clean WebP per card.
// Usage: node tools/card-slicer/slice.mjs <scan.jpg> <out-dir>
// The scan is a 10 × 4 grid: rows Denari, Coppe, Bastoni, Spade; columns A, 2..7, Fante, Cavallo, Re.
// For every card: straighten it (the angle that makes the printed frame lines sharpest), cut just inside the
// printed frame, then redraw the artwork on a clean white card with an even margin, livelier colours and a
// light sharpening, so the cards look printed rather than scanned. Works at any scan resolution.
// Needs Playwright (preinstalled in the cloud environment under /opt/node22/lib/node_modules).
import { createRequire } from 'module';
import { readFileSync, writeFileSync, mkdirSync } from 'fs';
const require = createRequire('/opt/node22/lib/node_modules/');
const { chromium } = require('playwright');

const [scan, outDir] = process.argv.slice(2);
const suits = ['D', 'C', 'B', 'S'];
const ranks = ['A', '2', '3', '4', '5', '6', '7', 'J', 'N', 'K'];
const W = 240, H = 440; // output: Piacentine proportions (1 : 1.83), enough for 3× screens at ~72 css px

mkdirSync(outDir, { recursive: true });
const browser = await chromium.launch();
const page = await browser.newPage();
const dataUrl = 'data:image/jpeg;base64,' + readFileSync(scan).toString('base64');
const result = await page.evaluate(async ({ dataUrl, W, H }) => {
  const img = new Image();
  img.src = dataUrl;
  await img.decode();
  const cellW = img.width / 10, cellH = img.height / 4;
  const lum = (d, i) => d[i] * 0.3 + d[i + 1] * 0.59 + d[i + 2] * 0.11;

  // A cell with some room around it, rotated by `angle` degrees about its centre.
  function cell(row, col, angle) {
    const pad = Math.round(cellW * 0.05);
    const w = Math.round(cellW) + 2 * pad, h = Math.round(cellH) + 2 * pad;
    const c = document.createElement('canvas');
    c.width = w; c.height = h;
    const ctx = c.getContext('2d', { willReadFrequently: true });
    ctx.fillStyle = '#fff'; ctx.fillRect(0, 0, w, h);
    ctx.translate(w / 2, h / 2);
    ctx.rotate(angle * Math.PI / 180);
    ctx.drawImage(img, col * cellW - pad, row * cellH - pad, w, h, -w / 2, -h / 2, w, h);
    return { c, w, h, d: ctx.getImageData(0, 0, w, h).data };
  }

  // Fraction of dark pixels per column (rows 15–85%) and per row (columns 15–85%).
  function profiles({ w, h, d }) {
    const cols = new Float32Array(w), rows = new Float32Array(h);
    const y0 = Math.round(h * 0.15), y1 = Math.round(h * 0.85), x0 = Math.round(w * 0.15), x1 = Math.round(w * 0.85);
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      if (lum(d, (y * w + x) * 4) >= 175) continue;
      if (y >= y0 && y < y1) cols[x]++;
      if (x >= x0 && x < x1) rows[y]++;
    }
    for (let x = 0; x < w; x++) cols[x] /= (y1 - y0);
    for (let y = 0; y < h; y++) rows[y] /= (x1 - x0);
    return { cols, rows };
  }

  const peak = (a, from, to) => { let best = from; for (let i = from; i < to; i++) if (a[i] > a[best]) best = i; return { at: best, v: a[best] }; };

  const found = [];
  for (let row = 0; row < 4; row++) for (let col = 0; col < 10; col++) {
    // Straighten: the frame lines are sharpest (highest column peaks) when the card is upright.
    let best = null;
    for (let angle = -2; angle <= 2.001; angle += 0.25) {
      const k = cell(row, col, angle), p = profiles(k);
      // Small penalty for tilting: a card is only turned when that clearly sharpens its frame.
      const score = peak(p.cols, 0, Math.round(k.w * 0.4)).v + peak(p.cols, Math.round(k.w * 0.6), k.w).v - 0.04 * Math.abs(angle);
      if (!best || score > best.score) best = { angle, score, k, p };
    }
    const { k, p } = best;
    const l = peak(p.cols, 0, Math.round(k.w * 0.4)), r = peak(p.cols, Math.round(k.w * 0.6), k.w);
    const t = peak(p.rows, 0, Math.round(k.h * 0.3)), b = peak(p.rows, Math.round(k.h * 0.7), k.h);
    // Art centre, for cards whose frame line is hidden by the artwork.
    let sx = 0, sy = 0, n = 0;
    for (let y = 0; y < k.h; y++) for (let x = 0; x < k.w; x++) {
      const i = (y * k.w + x) * 4, mx = Math.max(k.d[i], k.d[i + 1], k.d[i + 2]), mn = Math.min(k.d[i], k.d[i + 1], k.d[i + 2]);
      if (mx - mn > 80) { sx += x; sy += y; n++; }
    }
    found.push({ row, col, angle: best.angle, k, l, r, t, b, cx: sx / n, cy: sy / n });
  }

  // The printed frame has the same size on every card: median over the cards where both lines are clear.
  const median = a => { const s = [...a].sort((x, y) => x - y); return s[Math.floor(s.length / 2)]; };
  const strong = 0.6;
  const fw = median(found.filter(f => f.l.v > strong && f.r.v > strong).map(f => f.r.at - f.l.at));
  const fh = median(found.filter(f => f.t.v > strong && f.b.v > strong).map(f => f.b.at - f.t.at));

  const cards = [];
  for (const f of found) {
    // Several clues for where the frame is; keep the one that agrees with the artwork (horizontally) and with the
    // other cards of the same scan row (vertically). A misplaced card edge in the scan then cannot win.
    const pick = (candidates, expected, tolerance) => {
      const ok = candidates.filter(v => Number.isFinite(v)).sort((a, b) => Math.abs(a - expected) - Math.abs(b - expected));
      return ok.length && Math.abs(ok[0] - expected) <= tolerance ? ok[0] : Math.round(expected);
    };
    const lefts = [f.l.v > strong ? f.l.at : NaN, f.r.v > strong ? f.r.at - fw : NaN];
    const left = pick(lefts, f.cx - fw / 2, fw * 0.12);
    const how = (left === lefts[0] ? 'L' : left === lefts[1] ? 'R' : 'c') ;
    const rowTops = found.filter(o => o.row === f.row && o.t.v > strong && o.b.v > strong && Math.abs(o.b.at - o.t.at - fh) <= fh * 0.04).map(o => o.t.at);
    const expectedTop = rowTops.length ? median(rowTops) : f.cy - fh / 2;
    const top = pick([f.t.v > strong ? f.t.at : NaN, f.b.v > strong ? f.b.at - fh : NaN], expectedTop, fh * 0.06);

    // Cut just inside the frame line, then lay the artwork on a clean card with an even white margin.
    // Cut just past the printed line (the artwork reaches the frame: a deeper cut would clip swords and crowns).
    const inset = Math.max(2, Math.round(fw * 0.013));
    const art = { x: left + inset, y: top + inset, w: fw - 2 * inset, h: fh - 2 * inset };
    const out = document.createElement('canvas');
    out.width = W; out.height = H;
    const o = out.getContext('2d', { willReadFrequently: true });
    o.fillStyle = '#fff'; o.fillRect(0, 0, W, H);
    const scale = Math.min((W - 30) / art.w, (H - 24) / art.h);
    const dw = art.w * scale, dh = art.h * scale, dx = (W - dw) / 2, dy = (H - dh) / 2;
    o.imageSmoothingQuality = 'high';
    o.drawImage(f.k.c, art.x, art.y, art.w, art.h, dx, dy, dw, dh);

    // Printed look: paper to pure white, livelier colours, a little more contrast, light unsharp mask.
    const id = o.getImageData(0, 0, W, H), d = id.data, src = new Uint8ClampedArray(d);
    for (let i = 0; i < d.length; i += 4) {
      let rr = src[i], gg = src[i + 1], bb = src[i + 2];
      const mx = Math.max(rr, gg, bb), mn = Math.min(rr, gg, bb);
      if (mn > 205 && mx - mn < 40) { d[i] = d[i + 1] = d[i + 2] = 255; continue; }
      const y = rr * 0.3 + gg * 0.59 + bb * 0.11;
      rr = y + (rr - y) * 1.25; gg = y + (gg - y) * 1.25; bb = y + (bb - y) * 1.25;
      d[i] = (rr - 128) * 1.08 + 132; d[i + 1] = (gg - 128) * 1.08 + 132; d[i + 2] = (bb - 128) * 1.08 + 132;
    }
    const enhanced = new Uint8ClampedArray(d);
    for (let y = 1; y < H - 1; y++) for (let x = 1; x < W - 1; x++) for (let ch = 0; ch < 3; ch++) {
      const i = (y * W + x) * 4 + ch;
      let blur = 0;
      for (let yy = -1; yy <= 1; yy++) for (let xx = -1; xx <= 1; xx++) blur += enhanced[i + (yy * W + xx) * 4];
      blur /= 9;
      d[i] = enhanced[i] + 0.7 * (enhanced[i] - blur);
    }
    o.putImageData(id, 0, 0);
    // Redraw the Piacentine frame as a crisp line on the artwork's edge: it restores the card's structure and
    // covers whatever is left of the scanned line.
    o.strokeStyle = '#1b1b1b';
    o.lineWidth = 2;
    o.strokeRect(Math.round(dx) + 0.5, Math.round(dy) + 0.5, Math.round(dw) - 1, Math.round(dh) - 1);
    cards.push({ row: f.row, col: f.col, angle: f.angle, how: how + (top === f.t.at ? 'T' : top === f.b.at - fh ? 'B' : 'r'), lv: [f.l.v, f.r.v].map(v => v.toFixed(2)).join('/'), webp: out.toDataURL('image/webp', 0.84) });
  }
  return { fw, fh, cards };
}, { dataUrl, W, H });

console.log('frame', result.fw, 'x', result.fh);
for (const card of result.cards) {
  const name = `${ranks[card.col]}${suits[card.row]}`;
  writeFileSync(`${outDir}/${name}.webp`, Buffer.from(card.webp.split(',')[1], 'base64'));
  console.log(name, 'angle', card.angle, card.how, card.lv);
}
await browser.close();
