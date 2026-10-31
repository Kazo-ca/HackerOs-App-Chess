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
