// Writes the snapshots chess.js gives for a few positions into tests/Kazo.Chess.Tests/Fixtures, so
// the C# tests read exactly what the rules module returns. Run again after updating chess.js:
// `node tests/js/write-fixtures.mjs`.
import { writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { createGame } from '../../src/Kazo.Chess/wwwroot/js/chess-rules.js';

const folder = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', 'Kazo.Chess.Tests', 'Fixtures');

const play = (fen, moves) => {
    const game = createGame(fen);
    let json = game.snapshot();
    for (const [from, to, promotion] of moves) {
        json = game.move(from, to, promotion);
        if (json === null) {
            throw new Error(`Illegal fixture move ${from}${to}.`);
        }
    }
    return json;
};

const fixtures = {
    'start.json': play(null, []),
    'after-e4.json': play(null, [['e2', 'e4']]),
    'fools-mate.json': play(null, [['f2', 'f3'], ['e7', 'e5'], ['g2', 'g4'], ['d8', 'h4']]),
    'promotion.json': play('8/P6k/8/8/8/8/8/K7 w - - 0 1', []),
    'stalemate.json': play('7k/5Q2/6K1/8/8/8/8/8 b - - 0 1', []),
    'insufficient-material.json': play('8/8/8/8/8/5k2/8/K1B5 b - - 0 1', []),
    'fifty-moves.json': play('8/8/8/8/8/5k2/8/KR6 w - - 100 80', []),
};

for (const [name, json] of Object.entries(fixtures)) {
    writeFileSync(path.join(folder, name), JSON.stringify(JSON.parse(json), null, 2) + '\n');
}
