// Card-play animations (M10). Called by JsMoveAnimator while the board still shows the state before the move:
// the played card flies from its player to the table; on a capture it lands on the captured cards, gathers them
// and carries them to the player who took them. Floating copies live in a fixed layer above the page, so Blazor's
// DOM is never moved; settle() removes them once the new state has rendered.
(function () {
    const FLY = 380, HOLD = 260, GATHER = 220, CARRY = 420; // ms at normal speed
    let speed = 1; // "Opzioni" → velocità animazioni: 1.5 slow, 1 normal, 0.6 fast, 0 off
    const t = ms => ms * speed;
    const hidden = [];

    const reducedMotion = () => window.matchMedia('(prefers-reduced-motion: reduce)').matches;

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

    function setSpeed(factor) { speed = Math.max(0, Number(factor) || 0); }

    window.spincioMoves = { play, settle, setSpeed };
})();
