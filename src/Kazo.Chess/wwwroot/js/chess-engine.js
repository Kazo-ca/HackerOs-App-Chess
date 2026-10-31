// The computer opponent: Stockfish 19 "lite, single-threaded" (stockfish.js, GPL-3.0, ../engine)
// in a Web Worker, so the search never blocks the OS. It needs no cross-origin isolation. This
// module only speaks UCI over postMessage; the choice of level and what to do with the move stay
// in C# (Engine/StockfishEngine.cs).

const ENGINE_URL = new URL('../engine/stockfish-19-lite-single.js', import.meta.url);

/**
 * Starts the engine and waits until it answers.
 * @returns {Promise<{ bestMove: (fen: string, skillLevel: number, moveTimeMs: number) => Promise<string>,
 *                     newGame: () => Promise<void>, stop: () => void, terminate: () => void }>}
 *   Plain closures, not class methods: Blazor calls a function found on an object reference
 *   without binding `this`.
 */
export async function createEngine() {
    const worker = new Worker(ENGINE_URL);
    // The request waiting for its answer: it reads every line until one matches.
    let pending = null;
    let failure = null;

    const fail = error => {
        failure = error;
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
        event.preventDefault();
        fail(new Error(`The chess engine stopped: ${event.message || 'unknown error'}.`));
    };

    // Sends commands, then resolves with the first line that matches `until`.
    const ask = (commands, until) => new Promise((resolve, reject) => {
        if (failure) {
            reject(failure);
            return;
        }
        pending = { until, resolve, reject };
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

    await ask(['uci'], line => line === 'uciok');
    await ask(['isready'], line => line === 'readyok');

    return {
        newGame: () => serialize(() => ask(['ucinewgame', 'isready'], line => line === 'readyok')),
        bestMove: (fen, skillLevel, moveTimeMs) => serialize(() => ask([
            `setoption name Skill Level value ${skillLevel}`,
            `position fen ${fen}`,
            `go movetime ${moveTimeMs}`,
        ], line => line.startsWith('bestmove'))),
        // Ends the current search early; its bestmove still arrives and settles the request.
        stop: () => worker.postMessage('stop'),
        terminate: () => {
            worker.terminate();
            fail(new Error('The chess engine was closed.'));
        },
    };
}
