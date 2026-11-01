// Chess as the Hack Store installs it: `npm run test:installed -- <HackerOS wwwroot> <packages>`.
//
// <HackerOS wwwroot>: a published HackerOS (for example the output of
//   external/HackerOs/wasm2/HackerOs/build/publish-trimmed.sh, artifacts/publish-trimmed/publish/wwwroot),
//   the trimmed OS users run.
// <packages>: the folder `dotnet build src/Kazo.Chess -c Release -t:HackerOsPack` writes
//   (default src/Kazo.Chess/bin/hackeros-packages), the content of the hackeros-packages branch.
//
// The run serves the OS, answers the GitHub addresses of this repository's packages from <packages>,
// installs Chess with `sudo hackpkg install` (its API check against the trimmed runtime included),
// opens it, and plays against Stockfish: the board's styles, scripts, piece images and the engine's
// Web Worker and .wasm all come from the installed package. Not part of CI: it needs a HackerOS
// publish (about ten minutes to build).
import { createServer } from 'node:http';
import { readFile, stat } from 'node:fs/promises';
import path from 'node:path';
import { chromium } from 'playwright';

const [wwwroot, packages] = process.argv.slice(2).map(argument => argument && path.resolve(argument));
if (!wwwroot || !packages) {
    console.error('usage: npm run test:installed -- <HackerOS wwwroot> <packages folder>');
    process.exit(2);
}

const REPOSITORY = 'Kazo-ca/HackerOs-App-Chess';
const LOGIN = 'neo';
const PASSWORD = 'matrix123';
const STEP_MS = 120_000;
const TYPES = {
    '.html': 'text/html', '.js': 'text/javascript', '.mjs': 'text/javascript', '.css': 'text/css',
    '.json': 'application/json', '.wasm': 'application/wasm', '.svg': 'image/svg+xml', '.png': 'image/png',
    '.webmanifest': 'application/manifest+json', '.dat': 'application/octet-stream', '.blat': 'application/octet-stream',
};

const log = message => console.log(`[installed] ${message}`);

// A plain static server, as a release host is: no COOP/COEP, no cache.
const server = createServer(async (request, response) => {
    const relative = decodeURIComponent(new URL(request.url, 'http://host').pathname);
    let file = path.join(wwwroot, relative);
    if (!file.startsWith(wwwroot)) {
        response.writeHead(403).end();
        return;
    }
    try {
        if ((await stat(file)).isDirectory()) file = path.join(file, 'index.html');
        const body = await readFile(file);
        response.writeHead(200, { 'Content-Type': TYPES[path.extname(file)] ?? 'application/octet-stream', 'Cache-Control': 'no-store' });
        response.end(body);
    } catch {
        response.writeHead(404).end();
    }
});
await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
const baseUrl = `http://127.0.0.1:${server.address().port}/`;

const waitForText = async (locator, predicate, what) => {
    const deadline = Date.now() + STEP_MS;
    while (Date.now() < deadline) {
        const text = (await locator.innerText()).replace(/ /g, ' ');
        if (predicate(text)) return text;
        await new Promise(resolve => setTimeout(resolve, 250));
    }
    throw new Error(`timed out waiting for ${what}`);
};

const browser = await chromium.launch();
const errors = [];
let page;
try {
    const context = await browser.newContext({ viewport: { width: 1280, height: 800 } });
    // raw.githubusercontent.com, as hackpkg reads it (ADR 0182 of HackerOS), answered locally.
    await context.route(new RegExp(`^https://raw\\.githubusercontent\\.com/${REPOSITORY}/hackeros-packages/`), async route => {
        const relative = decodeURIComponent(new URL(route.request().url()).pathname).split('/hackeros-packages/')[1];
        const file = path.join(packages, relative);
        const headers = { 'Access-Control-Allow-Origin': '*' };
        try {
            if (!file.startsWith(packages)) throw new Error('outside the packages folder');
            await route.fulfill({ status: 200, body: await readFile(file), headers: { ...headers, 'Content-Type': 'application/octet-stream' } });
            log(`served ${relative}`);
        } catch {
            await route.fulfill({ status: 404, headers });
        }
    });
    page = await context.newPage();
    page.on('console', message => { if (message.type() === 'error') errors.push(message.text()); });
    page.on('pageerror', error => errors.push(String(error)));

    // First boot: the administrator is created.
    await page.goto(baseUrl, { waitUntil: 'domcontentloaded' });
    await page.getByRole('heading', { name: 'Create administrator' }).waitFor({ timeout: 180_000 });
    await page.getByLabel('Login name').fill(LOGIN);
    await page.getByLabel('Display name').fill('Neo');
    await page.getByLabel('Password', { exact: true }).fill(PASSWORD);
    await page.getByLabel('Confirm password').fill(PASSWORD);
    await page.getByRole('button', { name: 'Create local profile' }).click();
    const openApp = async name => {
        await page.getByRole('button', { name: 'App launcher' }).click({ timeout: STEP_MS });
        await page.getByRole('combobox', { name: 'Search applications' }).fill(name);
        await page.getByRole('button', { name: `Launch ${name}`, exact: true }).click();
    };

    // Install, as an administrator.
    await openApp('Terminal');
    const rows = page.locator('[data-terminal-panel] .xterm-rows').first();
    await waitForText(rows, text => text.includes('$'), 'the shell prompt');
    await page.locator('[data-terminal-panel] .xterm-screen').first().click();
    await page.keyboard.type(`sudo hackpkg install ${REPOSITORY}`);
    await page.keyboard.press('Enter');
    const prompt = page.locator('.credential-prompt-dialog, .aaa-window').first();
    await prompt.waitFor({ state: 'visible', timeout: STEP_MS });
    const login = prompt.locator('input[type=text]');
    if (await login.count() > 0 && !(await login.inputValue())) await login.fill(LOGIN);
    await prompt.locator('input[type=password]').fill(PASSWORD);
    await prompt.locator('input[type=password]').press('Enter');
    const output = await waitForText(rows, text => text.includes('available now') || /does not have|refused|error/i.test(text), 'the end of the installation');
    if (!output.includes('available now')) throw new Error(`hackpkg did not install Chess:\n${output}`);
    log('hackpkg installed Chess.');

    // Open it: pieces drawn (chess.js, images, board script) and its scoped CSS linked by the OS.
    await openApp('Chess');
    await page.locator('.chess-board .piece').first().waitFor({ timeout: STEP_MS });
    const linked = await page.evaluate(() => [...document.querySelectorAll('link[data-hackeros-installed-app]')].map(link => link.getAttribute('href')));
    if (!linked.some(href => href.endsWith('_content/Kazo.Chess/Kazo.Chess.bundle.scp.css'))) {
        throw new Error(`the scoped CSS of Chess is not linked: ${JSON.stringify(linked)}`);
    }
    const display = await page.evaluate(() => getComputedStyle(document.querySelector('.chess-window')).display);
    if (display !== 'flex') throw new Error(`the Chess window is not styled (display: ${display})`);
    log('Chess opened, styled, with its pieces.');

    // Against the computer as Black: Stockfish opens, then answers Nf6.
    await page.getByRole('button', { name: 'New game' }).click();
    const dialog = page.getByRole('dialog', { name: 'New game' });
    await dialog.getByRole('radio', { name: 'Against the computer' }).check();
    await dialog.getByRole('radio', { name: 'Beginner' }).check();
    await dialog.getByRole('radio', { name: 'Black', exact: true }).check();
    await dialog.getByRole('button', { name: 'Start' }).click();
    await page.getByText('Black to move').waitFor({ timeout: STEP_MS });
    const box = await page.locator('.board-svg').boundingBox();
    // Black at the bottom: file h on the left, rank 1 at the top.
    const center = square => ({
        x: box.x + ((7 - (square.charCodeAt(0) - 97) + 0.5) * box.width) / 8,
        y: box.y + ((Number(square[1]) - 1 + 0.5) * box.height) / 8,
    });
    for (const square of ['g8', 'f6']) {
        const point = center(square);
        await page.mouse.click(point.x, point.y);
    }
    await page.waitForFunction(() => document.querySelectorAll('.move-row').length === 2, null, { timeout: STEP_MS });
    await page.getByText('Black to move').waitFor({ timeout: STEP_MS });
    log('Stockfish played and answered.');

    if (errors.length > 0) throw new Error(`console errors: ${errors.join(' | ')}`);
    log('PASS');
} catch (error) {
    console.error(`[installed] FAIL: ${error.message}`);
    await page?.screenshot({ path: 'installed-failure.png' }).catch(() => {});
    process.exitCode = 1;
} finally {
    await browser.close();
    server.close();
}
