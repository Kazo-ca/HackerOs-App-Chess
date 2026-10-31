// The rules of the game, owned by chess.js (BSD-2-Clause, ../lib/chess.js). This module only
// adapts chess.js to the app: one game per call to createGame, and after every change a full
// snapshot of the position as a JSON string, so the C# side never asks chess.js twice for one
// render. Everything the board shows (legal moves, check, end of the game) comes from here.
import { Chess } from '../lib/chess.js/chess.js';

/**
 * Starts a game.
 * @param {string | null | undefined} fen Starting position; the standard one when empty.
 * @returns {{ snapshot: () => string, move: (from: string, to: string, promotion?: string | null) => string | null }}
 *   An object whose functions close over the game. Plain closures, not class methods: Blazor
 *   calls a function found on an object reference without binding `this`.
 */
export function createGame(fen) {
    const chess = fen ? new Chess(fen) : new Chess();
    return {
        snapshot: () => JSON.stringify(describe(chess)),
        move: (from, to, promotion) => {
            try {
                chess.move({ from, to, promotion: promotion || undefined });
            } catch {
                // chess.js throws on an illegal move; the caller gets null and keeps its position.
                return null;
            }
            return JSON.stringify(describe(chess));
        },
    };
}

/**
 * Describes a position in the shape GameSnapshotJson reads on the C# side.
 * @param {Chess} chess The game.
 */
export function describe(chess) {
    return {
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
        })),
    };
}
