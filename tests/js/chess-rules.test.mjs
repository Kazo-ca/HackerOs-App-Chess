// Tests of the rules module against the bundled chess.js: run with `node --test tests/js`.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createGame, loadGame } from '../../src/Kazo.Chess/wwwroot/js/chess-rules.js';

const read = json => JSON.parse(json);

test('the starting position has 20 legal moves and White to play', () => {
    const snapshot = read(createGame().snapshot());
    assert.equal(snapshot.turn, 'w');
    assert.equal(snapshot.moves.length, 20);
    assert.deepEqual(snapshot.history, []);
    assert.equal(snapshot.inCheck, false);
});

test('a legal move returns the new position and records it in the history', () => {
    const snapshot = read(createGame().move('e2', 'e4'));
    assert.equal(snapshot.turn, 'b');
    assert.deepEqual(snapshot.history, [{ from: 'e2', to: 'e4', san: 'e4', color: 'w', promotion: null }]);
    assert.match(snapshot.fen, /^rnbqkbnr\/pppppppp\/8\/8\/4P3\//);
});

test('an illegal move returns null and leaves the game unchanged', () => {
    const game = createGame();
    assert.equal(game.move('e2', 'e5'), null);
    assert.equal(read(game.snapshot()).history.length, 0);
});

test('fool\'s mate ends the game by checkmate', () => {
    const game = createGame();
    for (const [from, to] of [['f2', 'f3'], ['e7', 'e5'], ['g2', 'g4'], ['d8', 'h4']]) {
        assert.notEqual(game.move(from, to), null);
    }
    const snapshot = read(game.snapshot());
    assert.equal(snapshot.checkmate, true);
    assert.equal(snapshot.inCheck, true);
    assert.equal(snapshot.moves.length, 0);
    assert.equal(snapshot.history.at(-1).san, 'Qh4#');
});

test('a pawn reaching the last rank lists one move per promotion piece', () => {
    const game = createGame('8/P6k/8/8/8/8/8/K7 w - - 0 1');
    const promotions = read(game.snapshot()).moves.filter(move => move.from === 'a7').map(move => move.promotion).sort();
    assert.deepEqual(promotions, ['b', 'n', 'q', 'r']);
    const snapshot = read(game.move('a7', 'a8', 'n'));
    assert.equal(snapshot.history[0].san, 'a8=N');
    assert.match(snapshot.fen, /^N7\//);
});

test('a lone king against a king and bishop is a draw by insufficient material', () => {
    const snapshot = read(createGame('8/8/8/8/8/5k2/8/K1B5 b - - 0 1').snapshot());
    assert.equal(snapshot.insufficientMaterial, true);
});

test('a king with no move and not in check is stalemate', () => {
    const snapshot = read(createGame('7k/5Q2/6K1/8/8/8/8/8 b - - 0 1').snapshot());
    assert.equal(snapshot.stalemate, true);
    assert.equal(snapshot.inCheck, false);
});

test('the fifty-move rule is reported from the halfmove clock', () => {
    const snapshot = read(createGame('8/8/8/8/8/5k2/8/KR6 w - - 100 80').snapshot());
    assert.equal(snapshot.fiftyMoves, true);
});

test('undo takes moves back, never past the start', () => {
    const game = createGame();
    game.move('e2', 'e4');
    game.move('e7', 'e5');

    const one = read(game.undo(1));
    assert.equal(one.turn, 'b');
    assert.equal(one.history.length, 1);

    const all = read(game.undo(5));
    assert.equal(all.history.length, 0);
    assert.equal(all.fen, all.startFen);
});

test('the snapshot names the starting position and each promotion', () => {
    const start = '8/P6k/8/8/8/8/8/K7 w - - 0 1';
    const snapshot = read(createGame(start).move('a7', 'a8', 'q'));
    assert.equal(snapshot.startFen, start);
    assert.deepEqual(snapshot.history, [{ from: 'a7', to: 'a8', san: 'a8=Q', color: 'w', promotion: 'q' }]);
});

test('a game saves as PGN with the seven tags and its moves', () => {
    const game = createGame();
    game.move('e2', 'e4');
    game.move('e7', 'e5');
    const pgn = game.pgn(JSON.stringify({ Event: 'Casual game', Site: 'HackerOS', Date: '2026.10.31', White: 'White', Black: 'Black', Result: '*' }));

    assert.match(pgn, /^\[Event "Casual game"\]\n\[Site "HackerOS"\]\n\[Date "2026.10.31"\]\n\[Round "\?"\]/);
    assert.match(pgn, /\n\n1\. e4 e5 \*$/);
});

test('a saved game loads back with its moves, start and result', () => {
    const saved = createGame();
    for (const [from, to] of [['f2', 'f3'], ['e7', 'e5'], ['g2', 'g4'], ['d8', 'h4']]) {
        saved.move(from, to);
    }
    const pgn = saved.pgn(JSON.stringify({ Result: '0-1' }));

    const snapshot = read(loadGame(pgn).snapshot());
    assert.equal(snapshot.history.length, 4);
    assert.equal(snapshot.checkmate, true);
    assert.equal(snapshot.result, '0-1');
    assert.equal(snapshot.startFen, 'rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1');
});

test('a game from a set-up position keeps that position as its start', () => {
    const pgn = '[SetUp "1"]\n[FEN "8/P6k/8/8/8/8/8/K7 w - - 0 1"]\n\n1. a8=Q *';
    const snapshot = read(loadGame(pgn).snapshot());
    assert.equal(snapshot.startFen, '8/P6k/8/8/8/8/8/K7 w - - 0 1');
    assert.equal(snapshot.history[0].promotion, 'q');
});

test('a text that is not a game is refused', () => {
    assert.throws(() => loadGame('1. e5'), /Invalid move in PGN: e5/);
});

test('tag values are cleaned so a hostile name cannot break the file', () => {
    const game = createGame();
    game.move('e2', 'e4');
    const pgn = game.pgn(JSON.stringify({ White: 'Jo"hn\\x', Black: 'a\nb]\n[Result "1-0"]\n\n1. e4 1-0', Result: '' }));

    assert.match(pgn, /\[White "Jo'hn\/x"\]/);
    assert.equal((pgn.match(/^\[Result /gm) ?? []).length, 1);
    assert.match(pgn, /\[Result "\*"\]/);
    const back = read(loadGame(pgn).snapshot());
    assert.equal(back.history.length, 1);
});

test('a long tag value is cut at 255 characters', () => {
    const pgn = createGame().pgn(JSON.stringify({ White: 'x'.repeat(1000) }));
    assert.match(pgn, new RegExp(`\\[White "x{255}"\\]`));
});

test('of a collection, the first game is loaded', () => {
    const collection = '[Event "One"]\n[Result "*"]\n\n1. e4 e5 *\n\n[Event "Two"]\n[Result "*"]\n\n1. d4 *\n';
    const snapshot = read(loadGame(collection).snapshot());
    assert.deepEqual(snapshot.history.map(move => move.san), ['e4', 'e5']);
});

test('a text the parser cannot read gets a readable message', () => {
    assert.throws(() => loadGame('[Event "x"\n\n1. e4'), /^Error: The text is not a valid PGN game\.$/);
});

test('a game longer than 1000 half-moves is refused', () => {
    const shuffle = [];
    for (let move = 1; move <= 260; move++) {
        shuffle.push(`${move}. ${move % 2 ? 'Nf3 Nf6' : 'Ng1 Ng8'}`);
    }
    // 520 half-moves pass; the same twice over do not.
    assert.equal(read(loadGame(`${shuffle.join(' ')} *`).snapshot()).history.length, 520);
    const long = [];
    for (let move = 1; move <= 520; move++) {
        long.push(`${move}. ${move % 2 ? 'Nf3 Nf6' : 'Ng1 Ng8'}`);
    }
    assert.throws(() => loadGame(`${long.join(' ')} *`), /longer than 1000 half-moves/);
});
