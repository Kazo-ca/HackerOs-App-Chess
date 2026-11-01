// Pointer and keyboard plumbing of the chessboard. C# (BoardInteraction) takes every decision; this
// module only does what C# cannot: turn pointer coordinates into square names, capture the pointer,
// and draw the dragged piece under the finger without a .NET round trip per pointer move.

const FILES = 'abcdefgh';

// Pixels a pointer must travel before a press becomes a drag (a shaky tap stays a tap).
const DRAG_THRESHOLD_PX = 6;

// Keys the board handles itself: the page must not scroll or click on them.
const BOARD_KEYS = new Set(['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', ' ', 'Enter']);

/**
 * Binds the board's listeners.
 * @param {HTMLElement} host The focusable element around the board.
 * @param {SVGSVGElement} svg The board drawing, 8×8 units, with data-orientation="white" or "black".
 * @param {{ invokeMethodAsync: (name: string, ...args: unknown[]) => Promise<unknown> }} dotnet The board component.
 * @returns {{ detach: () => void }} A handle that removes every listener.
 */
export function attach(host, svg, dotnet) {
    let press = null;

    // A callback refused by .NET (the window closing, an error in the move) must not become an
    // unhandled promise rejection: it is reported once, the board keeps working.
    const call = (method, ...args) => dotnet.invokeMethodAsync(method, ...args)
        .catch(error => console.error(`Chess board: ${method} failed.`, error));

    const squareAt = (clientX, clientY) => {
        const rect = svg.getBoundingClientRect();
        const column = Math.floor(((clientX - rect.left) / rect.width) * 8);
        const row = Math.floor(((clientY - rect.top) / rect.height) * 8);
        if (column < 0 || column > 7 || row < 0 || row > 7) {
            return null;
        }
        const white = svg.dataset.orientation !== 'black';
        const file = white ? column : 7 - column;
        const rank = white ? 7 - row : row;
        return FILES[file] + (rank + 1);
    };

    const startDrag = () => {
        const size = svg.getBoundingClientRect().width / 8;
        const floating = document.createElement('img');
        floating.src = press.piece.getAttribute('href');
        floating.alt = '';
        // A detached image under the pointer: fixed to the viewport, so a transformed window frame
        // around the board cannot offset it, and transparent to the pointer.
        Object.assign(floating.style, {
            position: 'fixed',
            width: `${size}px`,
            height: `${size}px`,
            left: '0',
            top: '0',
            pointerEvents: 'none',
            zIndex: '2147483000',
            filter: 'drop-shadow(0 6px 8px rgba(0, 0, 0, 0.45))',
        });
        document.body.appendChild(floating);
        press.piece.classList.add('drag-source');
        press.floating = floating;
        press.size = size;
    };

    const moveFloating = (clientX, clientY) => {
        const half = press.size / 2;
        press.floating.style.transform = `translate(${clientX - half}px, ${clientY - half}px)`;
    };

    const endPress = () => {
        if (!press) {
            return;
        }
        press.floating?.remove();
        press.piece?.classList.remove('drag-source');
        if (svg.hasPointerCapture(press.pointerId)) {
            svg.releasePointerCapture(press.pointerId);
        }
        press = null;
    };

    const onPointerDown = event => {
        if (press || (event.pointerType === 'mouse' && event.button !== 0)) {
            return;
        }
        const square = squareAt(event.clientX, event.clientY);
        if (!square) {
            return;
        }
        host.focus({ preventScroll: true });
        const piece = svg.querySelector(`image[data-square="${square}"][data-movable="true"]`);
        press = { pointerId: event.pointerId, x: event.clientX, y: event.clientY, piece, floating: null, size: 0 };
        svg.setPointerCapture(event.pointerId);
        event.preventDefault();
        call('OnPress', square);
    };

    const onPointerMove = event => {
        if (!press || event.pointerId !== press.pointerId || !press.piece) {
            return;
        }
        if (!press.floating && Math.hypot(event.clientX - press.x, event.clientY - press.y) >= DRAG_THRESHOLD_PX) {
            startDrag();
        }
        if (press.floating) {
            moveFloating(event.clientX, event.clientY);
        }
    };

    const onPointerUp = event => {
        if (!press || event.pointerId !== press.pointerId) {
            return;
        }
        const dragged = press.floating !== null;
        const square = squareAt(event.clientX, event.clientY);
        endPress();
        call('OnRelease', square, dragged);
    };

    const onPointerCancel = event => {
        if (!press || event.pointerId !== press.pointerId) {
            return;
        }
        endPress();
        // Cancelled by the browser (a system gesture): the piece goes back, still selected.
        call('OnRelease', null, true);
    };

    const onKeyDown = event => {
        if (BOARD_KEYS.has(event.key) && event.target === host && !event.ctrlKey && !event.altKey && !event.metaKey) {
            event.preventDefault();
        }
    };

    svg.addEventListener('pointerdown', onPointerDown);
    svg.addEventListener('pointermove', onPointerMove);
    svg.addEventListener('pointerup', onPointerUp);
    svg.addEventListener('pointercancel', onPointerCancel);
    host.addEventListener('keydown', onKeyDown);

    return {
        detach: () => {
            endPress();
            svg.removeEventListener('pointerdown', onPointerDown);
            svg.removeEventListener('pointermove', onPointerMove);
            svg.removeEventListener('pointerup', onPointerUp);
            svg.removeEventListener('pointercancel', onPointerCancel);
            host.removeEventListener('keydown', onKeyDown);
        },
    };
}
