// End-to-end runs of the Chess app inside the HackerOS host: `npm run test:e2e` after
// `dotnet build Kazo.Chess.slnx`. Each test boots a fresh browser profile.
import { after, before, test } from 'node:test';
import assert from 'node:assert/strict';
import { chromium, devices } from 'playwright';
import { openChess, squareCenter, startHost } from './host.mjs';

let host;
let browser;

before(async () => {
    host = await startHost();
    browser = await chromium.launch();
});

after(async () => {
    await browser?.close();
    host?.stop();
});

const status = page => page.locator('.status-text');

const click = async (page, square) => {
    const point = await squareCenter(page, square);
    await page.mouse.click(point.x, point.y);
};

const drag = async (page, from, to) => {
    const a = await squareCenter(page, from);
    const b = await squareCenter(page, to);
    await page.mouse.move(a.x, a.y);
    await page.mouse.down();
    await page.mouse.move(a.x + 12, a.y - 12, { steps: 4 });
    await page.mouse.move(b.x, b.y, { steps: 12 });
    await page.mouse.up();
};

const pieceOn = (page, square) => page.locator(`.chess-board image[data-square="${square}"]`);

test('two players move by clicks, by drag and drop and with the keyboard', { timeout: 300_000 }, async () => {
    const page = await browser.newPage({ viewport: { width: 1280, height: 800 } });
    await openChess(page, host.baseUrl);
    assert.equal(await status(page).innerText(), 'White to move');

    // Click the pawn, then its target: the legal targets show while it is selected.
    await click(page, 'e2');
    assert.equal(await page.locator('.chess-board .target').count(), 2);
    await click(page, 'e4');
    await pieceOn(page, 'e4').waitFor();
    assert.equal(await status(page).innerText(), 'Black to move');

    // Drag and drop.
    await drag(page, 'e7', 'e5');
    await pieceOn(page, 'e5').waitFor();
    assert.equal(await pieceOn(page, 'e7').count(), 0);

    // Keyboard: the cursor follows the pointer, so it is on e5 after the drop; go to g1, pick the
    // knight, play it to f3.
    await page.locator('.chess-board').focus();
    const keys = ['ArrowRight', 'ArrowRight', 'ArrowDown', 'ArrowDown', 'ArrowDown', 'ArrowDown', 'Enter', 'ArrowLeft', 'ArrowUp', 'ArrowUp', 'Enter'];
    for (const key of keys) {
        await page.keyboard.press(key);
    }
    await pieceOn(page, 'f3').waitFor();
    assert.equal(await status(page).innerText(), 'Black to move');

    // An illegal target keeps the position: the knight on b8 cannot reach b6.
    await click(page, 'b8');
    await click(page, 'b6');
    assert.equal(await pieceOn(page, 'b8').count(), 1);
    await page.close();
});

test('fool\'s mate ends the game and locks the board', { timeout: 300_000 }, async () => {
    const page = await browser.newPage({ viewport: { width: 1280, height: 800 } });
    await openChess(page, host.baseUrl);
    for (const [from, to] of [['f2', 'f3'], ['e7', 'e5'], ['g2', 'g4'], ['d8', 'h4']]) {
        await click(page, from);
        await click(page, to);
        await pieceOn(page, to).waitFor();
    }
    await page.getByText('Checkmate: Black wins').waitFor();
    assert.equal(await page.locator('.chess-board .square.check').count(), 1);
    assert.equal(await page.locator('.chess-board image[data-movable="true"]').count(), 0);
    await page.close();
});

test('a pawn reaching the last rank asks for its new piece', { timeout: 300_000 }, async () => {
    const page = await browser.newPage({ viewport: { width: 1280, height: 800 } });
    await openChess(page, host.baseUrl);
    const moves = [['e2', 'e4'], ['d7', 'd5'], ['e4', 'd5'], ['c7', 'c6'], ['d5', 'c6'], ['g8', 'f6'], ['c6', 'b7'], ['b8', 'd7']];
    for (const [from, to] of moves) {
        await click(page, from);
        await click(page, to);
        await pieceOn(page, to).waitFor();
    }
    await click(page, 'b7');
    await click(page, 'a8');
    const picker = page.getByRole('dialog', { name: 'Promote to' });
    await picker.waitFor();

    // Cancel by pointer: the pawn stays where it was.
    await picker.getByRole('button', { name: 'Cancel' }).click();
    await picker.waitFor({ state: 'detached' });
    assert.equal(await pieceOn(page, 'b7').count(), 1);

    // Again, and choose with the keyboard: the picker takes the focus as it opens.
    await click(page, 'b7');
    await click(page, 'a8');
    await picker.waitFor();
    await page.waitForFunction(() => document.activeElement?.classList.contains('promotion-choice'));
    await page.keyboard.press('n');
    await page.locator('.chess-board image[data-square="a8"][href$="wN.svg"]').waitFor();
    await page.waitForFunction(() => document.activeElement?.classList.contains('chess-board'));
    assert.equal(await status(page).innerText(), 'Black to move');
    await page.close();
});

test('the board fits a phone screen and plays by touch', { timeout: 300_000 }, async () => {
    const context = await browser.newContext({ ...devices['Pixel 7'] });
    const page = await context.newPage();
    await openChess(page, host.baseUrl, { mobile: true });
    const board = await page.locator('.board-svg').boundingBox();
    const viewport = page.viewportSize();
    assert.ok(board.width > viewport.width * 0.8, `the board is ${board.width}px wide on a ${viewport.width}px screen`);
    assert.ok(Math.abs(board.width - board.height) < 2, 'the board stays square');

    for (const square of ['d2', 'd4']) {
        const point = await squareCenter(page, square);
        await page.touchscreen.tap(point.x, point.y);
    }
    await pieceOn(page, 'd4').waitFor();
    assert.equal(await status(page).innerText(), 'Black to move');
    await context.close();
});

test('against the computer as Black: Stockfish opens and answers', { timeout: 300_000 }, async () => {
    const page = await browser.newPage({ viewport: { width: 1280, height: 800 } });
    await openChess(page, host.baseUrl);
    await page.getByRole('button', { name: 'New game' }).click();
    const dialog = page.getByRole('dialog', { name: 'New game' });
    await dialog.getByRole('radio', { name: 'Against the computer' }).check();
    await dialog.getByRole('radio', { name: 'Beginner' }).check();
    await dialog.getByRole('radio', { name: 'Black', exact: true }).check();
    await dialog.getByRole('button', { name: 'Start' }).click();

    // The computer plays White's first move and the board faces the player's side.
    await page.getByText('Black to move').waitFor({ timeout: 60_000 });
    assert.equal(await page.locator('.board-svg').getAttribute('data-orientation'), 'black');
    assert.match(await page.locator('.side-panel').innerText(), /Computer \(Beginner\)/);

    // The knight to f6 is legal whatever White opened with; the computer then answers.
    const from = await squareCenter(page, 'g8', 'black');
    const to = await squareCenter(page, 'f6', 'black');
    await page.mouse.click(from.x, from.y);
    await page.mouse.click(to.x, to.y);
    await pieceOn(page, 'f6').waitFor();
    await page.getByText('Black to move').waitFor({ timeout: 60_000 });
    assert.equal(await page.locator('.move-row').count(), 2);
    await page.close();
});

test('the new game dialog closes on Escape and gives the focus back', { timeout: 300_000 }, async () => {
    const page = await browser.newPage({ viewport: { width: 1280, height: 800 } });
    await openChess(page, host.baseUrl);
    const button = page.getByRole('button', { name: 'New game' });
    await button.click();
    const dialog = page.getByRole('dialog', { name: 'New game' });
    await dialog.waitFor();
    await page.waitForFunction(() => document.activeElement?.type === 'radio' && document.activeElement.checked);

    await page.keyboard.press('Escape');
    await dialog.waitFor({ state: 'detached' });
    await page.waitForFunction(() => document.activeElement?.classList.contains('toolbar-button'));
    await page.close();
});

test('a new game while Stockfish thinks drops its move', { timeout: 300_000 }, async () => {
    const page = await browser.newPage({ viewport: { width: 1280, height: 800 } });
    await openChess(page, host.baseUrl);
    await page.getByRole('button', { name: 'New game' }).click();
    let dialog = page.getByRole('dialog', { name: 'New game' });
    await dialog.getByRole('radio', { name: 'Against the computer' }).check();
    await dialog.getByRole('radio', { name: 'Expert' }).check();
    await dialog.getByRole('radio', { name: 'Black', exact: true }).check();
    await dialog.getByRole('button', { name: 'Start' }).click();
    await page.getByText('The computer is thinking…').waitFor({ timeout: 60_000 });

    // Expert thinks 1.5 s: start a game for two players before it answers.
    await page.getByRole('button', { name: 'New game' }).click();
    dialog = page.getByRole('dialog', { name: 'New game' });
    await dialog.getByRole('radio', { name: 'Two players on this screen' }).check();
    await dialog.getByRole('button', { name: 'Start' }).click();

    await page.getByText('White to move').waitFor();
    await page.waitForTimeout(2_500);
    assert.equal(await status(page).innerText(), 'White to move');
    assert.equal(await page.locator('.chess-board .piece').count(), 32);
    assert.equal(await page.locator('.chess-board .square.last-move').count(), 0);
    assert.equal(await page.locator('.board-svg').getAttribute('data-orientation'), 'white');
    await page.close();
});

const playMoves = async (page, moves) => {
    for (const [from, to] of moves) {
        await click(page, from);
        await click(page, to);
        await pieceOn(page, to).waitFor();
    }
};

test('undo takes a move back and the board flips', { timeout: 300_000 }, async () => {
    const page = await browser.newPage({ viewport: { width: 1280, height: 800 } });
    await openChess(page, host.baseUrl);
    const undo = page.getByRole('button', { name: 'Undo move' });
    assert.equal(await undo.isDisabled(), true);

    await playMoves(page, [['e2', 'e4'], ['e7', 'e5']]);
    assert.deepEqual(await page.locator('.move-row').allInnerTexts(), ['1.\ne4\ne5']);
    await undo.click();
    await pieceOn(page, 'e7').waitFor();
    assert.equal(await status(page).innerText(), 'Black to move');
    assert.equal(await pieceOn(page, 'e5').count(), 0);

    await page.getByRole('button', { name: 'Flip board' }).click();
    assert.equal(await page.locator('.board-svg').getAttribute('data-orientation'), 'black');
    assert.equal(await page.getByRole('button', { name: 'Flip board' }).getAttribute('aria-pressed'), 'true');
    await page.close();
});

test('undo against the computer takes its reply back too', { timeout: 300_000 }, async () => {
    const page = await browser.newPage({ viewport: { width: 1280, height: 800 } });
    await openChess(page, host.baseUrl);
    await page.getByRole('button', { name: 'New game' }).click();
    const dialog = page.getByRole('dialog', { name: 'New game' });
    await dialog.getByRole('radio', { name: 'Against the computer' }).check();
    await dialog.getByRole('radio', { name: 'Beginner' }).check();
    await dialog.getByRole('radio', { name: 'White', exact: true }).check();
    await dialog.getByRole('button', { name: 'Start' }).click();

    // The engine starts first (the board waits for it), then White may move.
    await page.locator('.chess-board image[data-square="e2"][data-movable="true"]').waitFor({ timeout: 60_000 });
    await playMoves(page, [['e2', 'e4']]);
    await page.waitForFunction(() => document.querySelectorAll('.move-row .move').length === 2, null, { timeout: 60_000 });
    await page.getByRole('button', { name: 'Undo move' }).click();

    await pieceOn(page, 'e2').waitFor();
    await page.getByText('No moves yet').waitFor();
    assert.equal(await status(page).innerText(), 'White to move');
    await page.close();
});

test('a 5+0 clock starts after White\'s first move and runs for the side to move', { timeout: 300_000 }, async () => {
    const page = await browser.newPage({ viewport: { width: 1280, height: 800 } });
    await openChess(page, host.baseUrl);
    await page.getByRole('button', { name: 'New game' }).click();
    const dialog = page.getByRole('dialog', { name: 'New game' });
    await dialog.getByRole('radio', { name: '5+0' }).check();
    await dialog.getByRole('button', { name: 'Start' }).click();

    const faces = page.getByRole('timer');
    await page.waitForFunction(() => document.querySelectorAll('[role=timer]').length === 2);
    assert.deepEqual(await faces.allInnerTexts(), ['5:00', '5:00']);

    await playMoves(page, [['e2', 'e4']]);
    await page.waitForTimeout(2_200);
    // Black (top card) is thinking; White's first move was free.
    const [black, white] = await faces.allInnerTexts();
    assert.equal(white, '5:00');
    assert.match(black, /^4:5\d$/);
    assert.equal(await page.locator('.player-card.running .player-side').innerText(), 'Black');
    assert.equal(await page.getByRole('button', { name: 'Undo move' }).isDisabled(), true);
    await page.close();
});

test('a game saved as .pgn opens again with its moves', { timeout: 300_000 }, async () => {
    const page = await browser.newPage({ viewport: { width: 1280, height: 800 } });
    await openChess(page, host.baseUrl);
    await playMoves(page, [['e2', 'e4'], ['e7', 'e5'], ['g1', 'f3']]);

    // Save in the home folder under the suggested name.
    await page.getByRole('button', { name: 'Save' }).click();
    const saveDialog = page.getByRole('dialog').filter({ has: page.getByRole('button', { name: 'Save' }) }).last();
    const nameField = saveDialog.getByRole('textbox').last();
    await nameField.waitFor();
    const fileName = await nameField.inputValue();
    assert.match(fileName, /^chess-\d{4}-\d{2}-\d{2}\.pgn$/);
    await saveDialog.getByRole('button', { name: 'Save' }).click();
    await page.getByText(`Saved as ${fileName}.`).waitFor();

    // A fresh game, then open the file: the three moves come back, Black to move.
    await page.getByRole('button', { name: 'New game' }).click();
    await page.getByRole('dialog', { name: 'New game' }).getByRole('button', { name: 'Start' }).click();
    await page.getByText('No moves yet').waitFor();
    await page.getByRole('button', { name: 'Open', exact: true }).click();
    const openDialog = page.getByRole('dialog').filter({ hasText: fileName }).last();
    await openDialog.getByText(fileName, { exact: true }).click();
    await openDialog.getByRole('button', { name: 'Open', exact: true }).click();

    await pieceOn(page, 'f3').waitFor();
    assert.equal(await page.locator('.move-row').count(), 2);
    assert.equal(await status(page).innerText(), 'Black to move');
    await page.close();
});
