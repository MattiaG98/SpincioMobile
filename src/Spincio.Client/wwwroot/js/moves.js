// Card-play animations (M10). Called by JsMoveAnimator while the board still shows the state before the move:
// the played card flies from its player to the table; on a capture it lands on the captured cards, gathers them
// and carries them to the player who took them. Floating copies live in a fixed layer above the page, so Blazor's
// DOM is never moved; settle() removes them once the new state has rendered.
(function () {
    const FLY = 380, HOLD = 260, GATHER = 220, CARRY = 420; // ms
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

    function cardSize() {
        const sample = document.querySelector('.hand .card, .table .card');
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

    function flyingCard(card, x, y, size) {
        const el = document.createElement('div');
        el.className = 'fly-card';
        el.style.width = size.w + 'px';
        el.style.height = size.h + 'px';
        el.style.left = (x - size.w / 2) + 'px';
        el.style.top = (y - size.h / 2) + 'px';
        const img = document.createElement('img');
        img.src = `cards/${card}.webp`;
        img.alt = '';
        el.appendChild(img);
        layer().appendChild(el);
        return el;
    }

    // Moves an element (by transform) so that its centre goes to (x, y).
    function moveTo(el, x, y, duration, extra) {
        const left = parseFloat(el.style.left) + parseFloat(el.style.width) / 2;
        const top = parseFloat(el.style.top) + parseFloat(el.style.height) / 2;
        const current = getComputedStyle(el).transform;
        const to = `translate(${x - left}px, ${y - top}px) ${extra || ''}`;
        return el.animate([{ transform: current === 'none' ? 'none' : current }, { transform: to }],
            { duration, easing: 'cubic-bezier(.2,.7,.3,1)', fill: 'forwards' }).finished;
    }

    // Where the next card dropped on the table will appear: measured with an invisible probe card.
    function dropSlot(table, size) {
        const probe = document.createElement('span');
        probe.className = 'card';
        probe.style.visibility = 'hidden';
        table.appendChild(probe);
        const r = probe.getBoundingClientRect();
        table.removeChild(probe);
        return { x: r.left + r.width / 2, y: r.top + r.height / 2 };
    }

    async function play(card, position, captured) {
        if (reducedMotion()) return;
        const table = document.querySelector('.table');
        if (!table) return;
        const size = cardSize();

        // Start: my own card from the hand, everybody else's from their seat.
        let start;
        const fromHand = position === 0 ? document.querySelector(`.hand [data-card="${card}"]`) : null;
        if (fromHand) {
            const r = fromHand.getBoundingClientRect();
            start = { x: r.left + r.width / 2, y: r.top + r.height / 2 };
            hide(fromHand);
        } else {
            start = seatCentre(position);
        }
        const played = flyingCard(card, start.x, start.y, size);
        if (position !== 0) {
            played.animate([{ opacity: 0.4 }, { opacity: 1 }], { duration: FLY / 2, fill: 'forwards' });
        }

        const targets = captured.map(c => table.querySelector(`[data-card="${c}"]`)).filter(Boolean);
        if (targets.length === 0) {
            const slot = dropSlot(table, size);
            await moveTo(played, slot.x, slot.y, FLY);
            return; // stays until settle(), when the real card has rendered underneath
        }

        // Capture: land on the first captured card, slightly raised so it stays readable...
        // (not on the middle of the group: that may be a card it does not take).
        const rects = targets.map(t => t.getBoundingClientRect());
        const landX = rects[0].left + rects[0].width / 2, landY = rects[0].top + rects[0].height / 2 - size.h * 0.18;
        await moveTo(played, landX, landY, FLY);

        // ...show which cards it takes...
        const copies = targets.map((t, i) => {
            const r = rects[i];
            const copy = flyingCard(t.dataset.card, r.left + r.width / 2, r.top + r.height / 2, size);
            copy.classList.add('taken');
            hide(t);
            return copy;
        });
        played.style.zIndex = '2'; // keep the played card on top of the ones it takes
        await played.animate([{ transform: getComputedStyle(played).transform }, { transform: `${getComputedStyle(played).transform} scale(1.08)` },
            { transform: getComputedStyle(played).transform }], { duration: HOLD, easing: 'ease-in-out' }).finished;

        // ...gathers them under itself...
        await Promise.all(copies.map((c, i) => moveTo(c, landX + (i + 1) * 3, landY + (i + 1) * 3, GATHER)));

        // ...and carries them to whoever took them.
        const end = seatCentre(position);
        const all = [...copies, played];
        await Promise.all(all.map(c => moveTo(c, end.x, end.y, CARRY, 'scale(.45)')));
        all.forEach(c => c.animate([{ opacity: 1 }, { opacity: 0 }], { duration: 120, fill: 'forwards' }));
        await new Promise(r => setTimeout(r, 120));
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

    window.spincioMoves = { play, settle };
})();
