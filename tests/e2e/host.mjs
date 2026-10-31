// Test harness of the end-to-end runs: starts the Chess host (src/Kazo.Chess.Os, built beforehand
// with `dotnet build -c Release`, or the configuration named by CHESS_CONFIGURATION) on a free port,
// or uses CHESS_BASE_URL when it points to one already running, and opens the Chess app in a fresh
// HackerOS profile.
import { spawn } from 'node:child_process';
import { createServer } from 'node:net';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const hostProject = path.join(root, 'src', 'Kazo.Chess.Os');

// The first WebAssembly boot downloads and compiles the whole OS: give it time.
export const BOOT_TIMEOUT_MS = 120_000;

const freePort = () => new Promise((resolve, reject) => {
    const server = createServer();
    server.listen(0, '127.0.0.1', () => {
        const { port } = server.address();
        server.close(() => resolve(port));
    });
    server.on('error', reject);
});

const waitForHttp = async (url, timeoutMs) => {
    const deadline = Date.now() + timeoutMs;
    while (Date.now() < deadline) {
        try {
            const response = await fetch(url);
            if (response.ok) {
                return;
            }
        } catch {
            // Not listening yet.
        }
        await new Promise(resolve => setTimeout(resolve, 500));
    }
    throw new Error(`${url} did not answer within ${timeoutMs} ms.`);
};

/**
 * Starts the host, or reuses CHESS_BASE_URL.
 * @returns {Promise<{ baseUrl: string, stop: () => void }>}
 */
export async function startHost() {
    if (process.env.CHESS_BASE_URL) {
        return { baseUrl: process.env.CHESS_BASE_URL, stop: () => {} };
    }
    const port = await freePort();
    const baseUrl = `http://127.0.0.1:${port}/`;
    const configuration = process.env.CHESS_CONFIGURATION ?? 'Release';
    const server = spawn('dotnet', ['run', '--no-build', '-c', configuration, '--urls', baseUrl], { cwd: hostProject, stdio: 'ignore' });
    await waitForHttp(baseUrl, 60_000);
    return { baseUrl, stop: () => server.kill() };
}

/**
 * Boots HackerOS in a fresh browser context (first boot: the administrator is created) and opens
 * the Chess app: from the Applications menu on the desktop, from "All applications" on a phone.
 * @param {import('playwright').Page} page A page of a new context.
 * @param {string} baseUrl The host's address.
 * @param {{ mobile?: boolean }} [options] Whether the page runs the mobile shell.
 */
export async function openChess(page, baseUrl, { mobile = false } = {}) {
    await page.goto(baseUrl);
    const inputs = page.locator('input');
    await inputs.first().waitFor({ timeout: BOOT_TIMEOUT_MS });
    await inputs.nth(0).fill('neo');
    await inputs.nth(1).fill('Neo');
    await inputs.nth(2).fill('matrix123');
    await inputs.nth(3).fill('matrix123');
    await page.getByRole('button', { name: 'Create local profile' }).click();
    const launcher = mobile
        ? page.getByRole('button', { name: 'All applications' })
        : page.getByText('Applications', { exact: true });
    await launcher.click({ timeout: 60_000 });
    await page.getByText('Chess', { exact: true }).first().click();
    await page.locator('.chess-board .piece').first().waitFor({ timeout: 30_000 });
}

/**
 * Returns the page coordinates of the centre of a square, White at the bottom unless told otherwise.
 * @param {import('playwright').Page} page The page.
 * @param {string} square Square name, such as e2.
 * @param {'white' | 'black'} orientation The side at the bottom.
 */
export async function squareCenter(page, square, orientation = 'white') {
    const box = await page.locator('.board-svg').boundingBox();
    const file = square.charCodeAt(0) - 97;
    const rank = Number(square[1]) - 1;
    const column = orientation === 'white' ? file : 7 - file;
    const row = orientation === 'white' ? 7 - rank : rank;
    return { x: box.x + ((column + 0.5) * box.width) / 8, y: box.y + ((row + 0.5) * box.height) / 8 };
}
