// Tests of the UCI module of the computer opponent, with a scripted worker in place of Stockfish:
// `node --test tests/js`. The real engine is played against in tests/e2e.
import { afterEach, test } from 'node:test';
import assert from 'node:assert/strict';
import { createEngine } from '../../src/Kazo.Chess/wwwroot/js/chess-engine.js';

// A worker that answers UCI the way Stockfish does, unless told to stay silent.
class ScriptedWorker {
    static last = null;

    constructor() {
        this.received = [];
        this.terminated = false;
        this.silent = ScriptedWorker.silent;
        this.searchReply = ScriptedWorker.searchReply ?? 'bestmove e2e4 ponder e7e5';
        ScriptedWorker.last = this;
    }

    postMessage(command) {
        this.received.push(command);
        if (this.silent || this.terminated) {
            return;
        }
        const reply = command === 'uci' ? ['id name Fake', 'uciok']
            : command === 'isready' ? ['readyok']
            : command.startsWith('go') && !this.holdSearch ? ['info depth 1', this.searchReply]
            : command === 'stop' && this.holdSearch ? [this.searchReply]
            : [];
        for (const line of reply) {
            queueMicrotask(() => this.onmessage?.({ data: line }));
        }
    }

    terminate() {
        this.terminated = true;
    }
}

const original = globalThis.Worker;
globalThis.Worker = ScriptedWorker;
afterEach(() => {
    ScriptedWorker.silent = false;
    ScriptedWorker.searchReply = undefined;
});
process.on('exit', () => { globalThis.Worker = original; });

test('the handshake then a search send UCI in order and return the bestmove line', async () => {
    const engine = await createEngine({ workerUrl: 'fake.js' });
    const worker = ScriptedWorker.last;

    await engine.newGame();
    const line = await engine.bestMove('8/8/8/8/8/8/8/K6k w - - 0 1', 5, 100);

    assert.equal(line, 'bestmove e2e4 ponder e7e5');
    assert.deepEqual(worker.received, [
        'uci', 'isready', 'ucinewgame', 'isready',
        'setoption name Skill Level value 5', 'position fen 8/8/8/8/8/8/8/K6k w - - 0 1', 'go movetime 100',
    ]);
});

test('two searches asked at once run one after the other', async () => {
    const engine = await createEngine({ workerUrl: 'fake.js' });
    const worker = ScriptedWorker.last;

    const [first, second] = await Promise.all([engine.bestMove('A', 0, 10), engine.bestMove('B', 20, 10)]);

    assert.equal(first, second);
    const positions = worker.received.filter(command => command.startsWith('position'));
    assert.deepEqual(positions, ['position fen A', 'position fen B']);
});

test('stop ends a search with the move found so far', async () => {
    const engine = await createEngine({ workerUrl: 'fake.js' });
    ScriptedWorker.last.holdSearch = true;

    const search = engine.bestMove('A', 10, 60_000);
    engine.stop();

    assert.equal(await search, 'bestmove e2e4 ponder e7e5');
});

test('a worker that never answers the handshake is ended and reported', async () => {
    ScriptedWorker.silent = true;

    await assert.rejects(createEngine({ workerUrl: 'fake.js', handshakeMs: 30 }), /did not answer within 30 ms/);
    assert.equal(ScriptedWorker.last.terminated, true);
});

test('a search past its deadline ends the worker, and later calls fail at once', async () => {
    const engine = await createEngine({ workerUrl: 'fake.js', searchMarginMs: 20 });
    ScriptedWorker.last.holdSearch = true;

    await assert.rejects(engine.bestMove('A', 10, 10), /did not answer within 30 ms/);
    assert.equal(ScriptedWorker.last.terminated, true);
    await assert.rejects(engine.newGame(), /did not answer/);
});

test('a worker error rejects the pending request', async () => {
    const engine = await createEngine({ workerUrl: 'fake.js' });
    const worker = ScriptedWorker.last;
    worker.holdSearch = true;

    const search = engine.bestMove('A', 10, 60_000);
    worker.onerror({ message: 'wasm failed' });

    await assert.rejects(search, /The chess engine stopped: wasm failed/);
    assert.equal(worker.terminated, true);
});

test('terminate rejects the pending request and ends the worker', async () => {
    const engine = await createEngine({ workerUrl: 'fake.js' });
    const worker = ScriptedWorker.last;
    worker.holdSearch = true;

    const search = engine.bestMove('A', 10, 60_000);
    engine.terminate();

    await assert.rejects(search, /was closed/);
    assert.equal(worker.terminated, true);
    engine.stop();
    assert.equal(worker.received.includes('stop'), false, 'no command reaches a closed worker');
});
