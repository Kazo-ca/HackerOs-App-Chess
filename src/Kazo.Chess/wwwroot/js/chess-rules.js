// The rules of the game, owned by chess.js (BSD-2-Clause, ../lib/chess.js). This module only
// adapts chess.js to the app: one game per call to createGame, and after every change a full
// snapshot of the position as a JSON string, so the C# side never asks chess.js twice for one
// render. Everything the board shows (legal moves, check, end of the game) comes from here.
import { Chess, DEFAULT_POSITION } from '../lib/chess.js/chess.js';

// The tags a saved game always carries (PGN's "seven tag roster"), in order.
const PGN_TAGS = ['Event', 'Site', 'Date', 'Round', 'White', 'Black', 'Result'];

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
 * @param {string} pgn The text of a .pgn file (its first game).
 * @returns The same object as createGame.
 * @throws {Error} When the text is not a valid game (chess.js names the first illegal move).
 */
export function loadGame(pgn) {
    const chess = new Chess();
    chess.loadPgn(pgn);
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
        // The game as a .pgn text: the given tags (JSON object, values already final), then the moves.
        pgn: tagsJson => {
            const tags = JSON.parse(tagsJson);
            for (const tag of PGN_TAGS) {
                chess.setHeader(tag, tags[tag] ?? '?');
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
