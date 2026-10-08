// Card-play animations (M10). Called by JsMoveAnimator while the board still shows the state before the move:
// the played card flies from its player to the table; on a capture it lands on the captured cards, gathers them
// and carries them to the player who took them. Floating copies live in a fixed layer above the page, so Blazor's
// DOM is never moved; settle() removes them once the new state has rendered.
// Points scored (sweeps, declarations, end of round) pop up as "+N" and fly to the team's score: points().
(function () {
    const FLY = 380, HOLD = 260, GATHER = 220, CARRY = 420; // ms at normal speed
    const POP = 260, SHOW = 420, TO_SCORE = 520, BUMP = 360; // points: appear, stay, fly to the score, score bump
    let speed = 1; // "Opzioni" → velocità animazioni: 1.5 slow, 1 normal, 0.6 fast, 0 off
    const t = ms => ms * speed;
    const hidden = [];

    const reducedMotion = () => window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const wait = ms => new Promise(r => setTimeout(r, ms));

    // While the animations run, the "Spazzino!" banner comes from points(); the page's own flash is the fallback.
    const syncFlag = () => document.documentElement.classList.toggle('moves-on', speed > 0 && !reducedMotion());
    syncFlag();

    function layer() {
        let el = document.getElementById('fly-layer');
        if (!el) {
            el = document.createElement('div');
            el.id = 'fly-layer';
            document.body.appendChild(el);
        }
        return el;
    }

    // Size of a card in an area of the board; the table's cards shrink to fit it (app.css), so sizes differ.
    function cardSize(selector) {
        const sample = document.querySelector(selector) || document.querySelector('.hand .card');
        if (sample) {
            const r = sample.getBoundingClientRect();
            return { w: r.width, h: r.height };
        }
        return { w: 64, h: 117 };
    }

    // Centre of the area where a seat sits, relative to the viewer: 0 = me, 1 = right, 2 = partner, 3 = left.
    function seatCentre(position) {
        const el = position === 0 ? document.querySelector('.me .hand, .me') : document.querySelector(`.seat-${position} .seat-cards, .seat-${position}`);
        const r = (el || document.querySelector('.table')).getBoundingClientRect();
        return { x: r.left + r.width / 2, y: r.top + r.height / 2 };
    }

    function hide(el) {
        el.style.visibility = 'hidden';
        hidden.push(el);
    }

    const box = (x, y, size) => ({ left: (x - size.w / 2) + 'px', top: (y - size.h / 2) + 'px', width: size.w + 'px', height: size.h + 'px' });

    function flyingCard(card, x, y, size) {
        const el = document.createElement('div');
        el.className = 'fly-card';
        Object.assign(el.style, box(x, y, size));
        const img = document.createElement('img');
        img.src = `cards/${card}.webp`;
        img.alt = '';
        el.appendChild(img);
        layer().appendChild(el);
        return el;
    }

    // Moves an element so that its centre goes to (x, y), resizing it to `size` on the way: a card played from the hand
    // shrinks as it nears the table, until it is as big as the cards already there.
    async function moveTo(el, x, y, size, duration) {
        const from = { left: el.style.left, top: el.style.top, width: el.style.width, height: el.style.height };
        const to = box(x, y, size);
        if (duration > 0) {
            const a = el.animate([from, to], { duration, easing: 'cubic-bezier(.2,.7,.3,1)', fill: 'forwards' });
            await a.finished;
            Object.assign(el.style, to);
            a.cancel();
        } else {
            Object.assign(el.style, to);
        }
    }

    // Where the next card dropped on the table will appear, and how big: measured with an invisible probe card.
    function dropSlot(table) {
        const probe = document.createElement('span');
        probe.className = 'card';
        probe.style.visibility = 'hidden';
        table.appendChild(probe);
        const r = probe.getBoundingClientRect();
        table.removeChild(probe);
        return { x: r.left + r.width / 2, y: r.top + r.height / 2, size: { w: r.width, h: r.height } };
    }

    async function play(card, position, captured) {
        if (speed === 0 || reducedMotion()) return;
        const table = document.querySelector('.table');
        if (!table) return;

        // Start: my own card from the hand, everybody else's from their seat (as big as the cards shown there).
        let start, startSize;
        const fromHand = position === 0 ? document.querySelector(`.hand [data-card="${card}"]`) : null;
        if (fromHand) {
            const r = fromHand.getBoundingClientRect();
            start = { x: r.left + r.width / 2, y: r.top + r.height / 2 };
            startSize = { w: r.width, h: r.height };
            hide(fromHand);
        } else {
            start = seatCentre(position);
            startSize = position === 0 ? cardSize('.hand .card') : cardSize(`.seat-${position} .card`);
        }
        const played = flyingCard(card, start.x, start.y, startSize);
        if (position !== 0) {
            played.animate([{ opacity: 0.4 }, { opacity: 1 }], { duration: t(FLY / 2), fill: 'forwards' });
        }

        const targets = captured.map(c => table.querySelector(`[data-card="${c}"]`)).filter(Boolean);
        if (targets.length === 0) {
            const slot = dropSlot(table);
            await moveTo(played, slot.x, slot.y, slot.size, t(FLY));
            return; // stays until settle(), when the real card has rendered underneath
        }

        // Capture: land on the first captured card, as big as it and slightly raised so it stays readable...
        // (not on the middle of the group: that may be a card it does not take).
        const rects = targets.map(t => t.getBoundingClientRect());
        const tableSize = { w: rects[0].width, h: rects[0].height };
        const landX = rects[0].left + rects[0].width / 2, landY = rects[0].top + rects[0].height / 2 - tableSize.h * 0.18;
        await moveTo(played, landX, landY, tableSize, t(FLY));

        // ...show which cards it takes...
        const copies = targets.map((t, i) => {
            const r = rects[i];
            const copy = flyingCard(t.dataset.card, r.left + r.width / 2, r.top + r.height / 2, { w: r.width, h: r.height });
            copy.classList.add('taken');
            hide(t);
            return copy;
        });
        played.style.zIndex = '2'; // keep the played card on top of the ones it takes
        await played.animate([{ transform: 'none' }, { transform: 'scale(1.08)' }, { transform: 'none' }],
            { duration: t(HOLD), easing: 'ease-in-out' }).finished;

        // ...gathers them under itself...
        await Promise.all(copies.map((c, i) => moveTo(c, landX + (i + 1) * 3, landY + (i + 1) * 3, tableSize, t(GATHER))));

        // ...and carries them to whoever took them.
        const end = seatCentre(position);
        const all = [...copies, played];
        const small = { w: tableSize.w * 0.45, h: tableSize.h * 0.45 };
        await Promise.all(all.map(c => moveTo(c, end.x, end.y, small, t(CARRY))));
        all.forEach(c => c.animate([{ opacity: 1 }, { opacity: 0 }], { duration: t(120), fill: 'forwards' }));
        await new Promise(r => setTimeout(r, t(120)));
        all.forEach(c => c.remove());
    }

    function centreOf(el) {
        const r = el.getBoundingClientRect();
        return { x: r.left + r.width / 2, y: r.top + r.height / 2 };
    }

    // gains: [{ kind: 'sweep' | 'declaration' | 'roundend', from: 0-3 (seat, relative to me) or -1 (table),
    //           ours: bool, points: n, label: text or null }]. Several gains play together (end of round: both teams).
    async function points(gains) {
        if (!gains || !gains.length || speed === 0 || reducedMotion()) return;
        const boxes = document.querySelectorAll('.scorebar .score'); // "Noi", then "Loro"
        const table = document.querySelector('.table');
        if (boxes.length < 2 || !table) return;
        await Promise.all(gains.map(g => flyPoints(g, boxes[g.ours ? 0 : 1], table, gains.length > 1 ? (g.ours ? -1 : 1) : 0)));
    }

    // "+N" (with what earned it) pops up where it was made, then flies to the score, which bumps as it lands.
    // The score still shows the old total: the new one renders right after, with the new state.
    async function flyPoints(g, box, table, side) {
        const el = document.createElement('div');
        el.className = `points-pop ${g.ours ? 'ours' : 'theirs'} ${g.kind}`;
        if (g.label) {
            const label = document.createElement('span');
            label.className = 'label';
            label.textContent = g.label;
            el.appendChild(label);
        }
        const value = document.createElement('strong');
        value.textContent = `+${g.points}`;
        el.appendChild(value);
        layer().appendChild(el);

        const from = g.from < 0 ? centreOf(table) : seatCentre(g.from);
        const size = el.getBoundingClientRect();
        // Two gains from the table: side by side. Always fully on screen (side seats are at the edges).
        const clamp = (v, half, max) => Math.min(Math.max(v, half + 8), max - half - 8);
        const x = clamp(from.x + side * (size.width / 2 + 8), size.width / 2, innerWidth);
        const y = clamp(from.y, size.height / 2, innerHeight);
        el.style.left = (x - size.width / 2) + 'px';
        el.style.top = (y - size.height / 2) + 'px';

        await el.animate([{ transform: 'scale(.3)', opacity: 0 }, { transform: 'scale(1.15)', opacity: 1, offset: .7 }, { transform: 'scale(1)', opacity: 1 }],
            { duration: t(POP), easing: 'ease-out', fill: 'forwards' }).finished;
        await wait(t(SHOW));
        const end = centreOf(box);
        await el.animate([{ transform: 'none', opacity: 1 }, { transform: `translate(${end.x - x}px, ${end.y - y}px) scale(.4)`, opacity: .85 }],
            { duration: t(TO_SCORE), easing: 'cubic-bezier(.45,0,.8,.5)', fill: 'forwards' }).finished;
        el.remove();
        // Not awaited: the bump goes on while the new score renders.
        box.animate([{ transform: 'scale(1)' }, { transform: 'scale(1.3)', boxShadow: '0 0 0 3px #f2b705, 0 0 18px #f2b705', offset: .35 }, { transform: 'scale(1)' }],
            { duration: t(BUMP), easing: 'ease-out' });
    }

    // After the new state has rendered: drop the floating copies and show anything we hid that is still on screen.
    async function settle() {
        await new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)));
        const el = document.getElementById('fly-layer');
        if (el) el.replaceChildren();
        while (hidden.length) {
            const h = hidden.pop();
            if (h.isConnected) h.style.visibility = '';
        }
    }

    function setSpeed(factor) {
        speed = Math.max(0, Number(factor) || 0);
        syncFlag();
    }

    window.spincioMoves = { play, points, settle, setSpeed };
})();
