// The rules of the game, owned by chess.js (BSD-2-Clause, ../lib/chess.js). This module only
// adapts chess.js to the app: one game per call to createGame, and after every change a full
// snapshot of the position as a JSON string, so the C# side never asks chess.js twice for one
// render. Everything the board shows (legal moves, check, end of the game) comes from here.
import { Chess, DEFAULT_POSITION } from '../lib/chess.js/chess.js';

// The tags a saved game always carries (PGN's "seven tag roster"), in order.
const PGN_TAGS = ['Event', 'Site', 'Date', 'Round', 'White', 'Black', 'Result'];

// A real game is far shorter; beyond this, chess.js (on the page's thread) takes seconds per call.
const MAX_PLIES = 1000;

// The longest tag value written. PGN puts no limit, readers often do.
const MAX_TAG_LENGTH = 255;

// A tag value as PGN can hold it and chess.js can read it back: one line, no quote, no backslash
// (chess.js reads neither escape), at most MAX_TAG_LENGTH characters.
const cleanTag = value => String(value ?? '')
    .replace(/[\u0000-\u001f\u007f]+/g, ' ')
    .replace(/"/g, "'")
    .replace(/\\/g, '/')
    .trim()
    .slice(0, MAX_TAG_LENGTH);

// The first game of a .pgn text: a collection is cut where the second game's tags begin.
const firstGame = text => {
    const movetext = text.search(/\r?\n\s*\r?\n/);
    if (movetext < 0) {
        return text;
    }
    const next = text.slice(movetext).search(/\r?\n\s*\r?\n\s*\[/);
    return next < 0 ? text : text.slice(0, movetext + next);
};

// chess.js's own messages name an illegal move or a bad FEN; its parser's ("Expected brace
// comment, end of input…") mean nothing to a player.
const readableError = error => {
    const message = error?.message ?? String(error);
    return /^(Invalid move|Invalid FEN)/.test(message) ? message : 'The text is not a valid PGN game.';
};

/**
 * Starts a game.
 * @param {string | null | undefined} fen Starting position; the standard one when empty.
 * @returns {{ snapshot: () => string, move: (from: string, to: string, promotion?: string | null) => string | null,
 *            undo: (plies: number) => string, pgn: (tagsJson: string) => string }}
 *   An object whose functions close over the game. Plain closures, not class methods: Blazor
 *   calls a function found on an object reference without binding `this`.
 */
export function createGame(fen) {
    const chess = fen ? new Chess(fen) : new Chess();
    // The position the game started from: the engine replays the moves from it, so it sees the
    // repetitions a lone FEN would hide.
    return wrap(chess, chess.fen());
}

/**
 * Starts a game from a saved one: its moves are replayed, so undo, the score sheet and the
 * engine see the whole game.
 * @param {string} pgn The text of a .pgn file; of a collection, the first game is loaded.
 * @returns The same object as createGame.
 * @throws {Error} When the text is not a valid game (the message names the first illegal move
 *   or a bad FEN), or the game is longer than MAX_PLIES half-moves.
 */
export function loadGame(pgn) {
    const chess = new Chess();
    try {
        chess.loadPgn(firstGame(pgn));
    } catch (error) {
        throw new Error(readableError(error));
    }
    if (chess.history().length > MAX_PLIES) {
        throw new Error(`The game is longer than ${MAX_PLIES} half-moves.`);
    }
    return wrap(chess, chess.getHeaders().FEN ?? DEFAULT_POSITION);
}

function wrap(chess, startFen) {
    return {
        snapshot: () => JSON.stringify(describe(chess, startFen)),
        move: (from, to, promotion) => {
            try {
                chess.move({ from, to, promotion: promotion || undefined });
            } catch {
                // chess.js throws on an illegal move; the caller gets null and keeps its position.
                return null;
            }
            return JSON.stringify(describe(chess, startFen));
        },
        // The game as a .pgn text: the given tags (JSON object), then the moves. Every tag value,
        // the given ones and those a loaded game brought, is cleaned so the file reads back.
        pgn: tagsJson => {
            const tags = JSON.parse(tagsJson);
            for (const [name, value] of Object.entries(chess.getHeaders())) {
                if (name !== 'FEN' && name.toUpperCase() === 'FEN') {
                    chess.removeHeader(name);
                } else {
                    chess.setHeader(name, cleanTag(value));
                }
            }
            for (const tag of PGN_TAGS) {
                const fallback = tag === 'Result' ? '*' : '?';
                chess.setHeader(tag, cleanTag(tags[tag]) || fallback);
            }
            return chess.pgn({ maxWidth: 80 });
        },
        // Takes back up to `plies` moves (fewer when the game has fewer).
        undo: plies => {
            for (let taken = 0; taken < plies && chess.undo(); taken++) {
                // chess.undo() returns null once the history is empty.
            }
            return JSON.stringify(describe(chess, startFen));
        },
    };
}

/**
 * Describes a position in the shape GameSnapshotJson reads on the C# side.
 * @param {Chess} chess The game.
 * @param {string} [startFen] The position the game started from (the current one when omitted).
 */
export function describe(chess, startFen = chess.fen()) {
    return {
        startFen,
        // The result a loaded game was saved with ("1-0", "0-1", "1/2-1/2" or "*").
        result: chess.getHeaders().Result ?? '*',
        fen: chess.fen(),
        turn: chess.turn(),
        inCheck: chess.inCheck(),
        checkmate: chess.isCheckmate(),
        stalemate: chess.isStalemate(),
        insufficientMaterial: chess.isInsufficientMaterial(),
        threefoldRepetition: chess.isThreefoldRepetition(),
        fiftyMoves: chess.isDrawByFiftyMoves(),
        moves: chess.moves({ verbose: true }).map(move => ({
            from: move.from,
            to: move.to,
            san: move.san,
            promotion: move.promotion ?? null,
        })),
        history: chess.history({ verbose: true }).map(move => ({
            from: move.from,
            to: move.to,
            san: move.san,
            color: move.color,
            promotion: move.promotion ?? null,
        })),
    };
}
