// The computer opponent: Stockfish 19 "lite, single-threaded" (stockfish.js, GPL-3.0, ../engine)
// in a Web Worker, so the search never blocks the OS. It needs no cross-origin isolation. This
// module only speaks UCI over postMessage; the choice of level and what to do with the move stay
// in C# (Engine/StockfishEngine.cs).

const ENGINE_URL = new URL('../engine/stockfish-19-lite-single.js', import.meta.url);

// A worker that loads but never answers must not freeze the window: every request has a deadline,
// and a missed deadline ends the worker (the C# side then shows the error and starts a fresh one).
const DEFAULT_TIMEOUTS = { handshakeMs: 20_000, searchMarginMs: 5_000 };

/**
 * Starts the engine and waits until it answers.
 * @param {{ handshakeMs?: number, searchMarginMs?: number, workerUrl?: string | URL }} [options]
 *   Deadlines (the search deadline is its movetime plus the margin) and, for tests, the worker script.
 * @returns {Promise<{ bestMove: (fen: string, skillLevel: number, moveTimeMs: number) => Promise<string>,
 *                     newGame: () => Promise<string>, stop: () => void, terminate: () => void }>}
 *   Plain closures, not class methods: Blazor calls a function found on an object reference
 *   without binding `this`.
 */
export async function createEngine(options = {}) {
    const { handshakeMs, searchMarginMs } = { ...DEFAULT_TIMEOUTS, ...options };
    const worker = new Worker(options.workerUrl ?? ENGINE_URL);
    // The request waiting for its answer: it reads every line until one matches.
    let pending = null;
    let failure = null;

    const fail = error => {
        if (!failure) {
            failure = error;
            worker.terminate();
        }
        pending?.reject(error);
        pending = null;
    };

    worker.onmessage = event => {
        const line = typeof event.data === 'string' ? event.data : String(event.data);
        if (pending && pending.until(line)) {
            const { resolve } = pending;
            pending = null;
            resolve(line);
        }
    };
    worker.onerror = event => {
        event.preventDefault?.();
        fail(new Error(`The chess engine stopped: ${event.message || 'unknown error'}.`));
    };

    // Sends commands, then resolves with the first line that matches `until`, or fails the engine
    // when no such line comes within `timeoutMs`.
    const ask = (commands, until, timeoutMs) => new Promise((resolve, reject) => {
        if (failure) {
            reject(failure);
            return;
        }
        const timer = setTimeout(
            () => fail(new Error(`The chess engine did not answer within ${timeoutMs} ms.`)),
            timeoutMs);
        pending = {
            until,
            resolve: line => { clearTimeout(timer); resolve(line); },
            reject: error => { clearTimeout(timer); reject(error); },
        };
        for (const command of commands) {
            worker.postMessage(command);
        }
    });

    // Searches run one after the other: a new request waits for the previous bestmove.
    let queue = Promise.resolve();
    const serialize = work => {
        const next = queue.then(work, work);
        queue = next.catch(() => {});
        return next;
    };

    try {
        await ask(['uci'], line => line === 'uciok', handshakeMs);
        await ask(['isready'], line => line === 'readyok', handshakeMs);
    } catch (error) {
        fail(error);
        throw error;
    }

    return {
        newGame: () => serialize(() => ask(['ucinewgame', 'isready'], line => line === 'readyok', handshakeMs)),
        bestMove: (fen, skillLevel, moveTimeMs) => serialize(() => ask([
            `setoption name Skill Level value ${skillLevel}`,
            `position fen ${fen}`,
            `go movetime ${moveTimeMs}`,
        ], line => line.startsWith('bestmove'), moveTimeMs + searchMarginMs)),
        // Ends the current search early; its bestmove still arrives and settles the request.
        stop: () => {
            if (!failure) {
                worker.postMessage('stop');
            }
        },
        terminate: () => fail(new Error('The chess engine was closed.')),
    };
}
