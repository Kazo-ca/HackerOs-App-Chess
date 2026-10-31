> [Version française](../fr/plan.md)

# Plan: the Chess app for HackerOS

Plan **approved on 2026-10-29** by Benoit ("Plan the Hack Store and the Chess
app" thread). This document sets the target and the order of the work; the
progress is at the end of the page and the code is described in
[`architecture.md`](architecture.md).

## Goal

Chess is the **first Hack Store app**: a third-party app, in its own
repository, that tests the whole HackerOS publishing chain (app repository →
PR to the `Kazo-ca/HackerOs-AppMarketPlace` catalog → checks and build by the
catalog CI → install by the Hack Store). The Store plan is in the HackerOs
repository: `doc/en/wasm/dev/plans/hackeros-store.md` and ADRs 0188 to 0190.

## Decisions

| # | Topic | Decision |
| --- | --- | --- |
| Q12 | Technology | Keep **chess.js** (rules, BSD-2-Clause license), served as a `wwwroot/` asset of the app. Prerequisite: the HackerOS work item that serves the assets of installed apps. |
| Q13 | Starting point | Generated from the `hackeros-solution` template (HackerOS ADR 0176) once that template is coded. |
| Q14 | Game modes | Two players on the same screen, and against the computer. Engine: **[stockfish.js](https://github.com/nmrugg/stockfish.js)**. Stockfish already has a strength setting, so no home-made engine. |
| Q30 | License | The repository switches to **GPL-3.0** (required to bundle stockfish.js). |
| Q31 | Engine build | Single-threaded "lite" build (about 7 MB, no cross-origin isolation), in a Web Worker. **5 named levels** (Beginner, Easy, Medium, Hard, Expert) mapped to `Skill Level` (0, 5, 10, 15, 20). |
| Q15 | Board | Drawn by us (Razor and SVG), colors taken from the OS theme tokens; pieces from a free CC0 or MIT set (chosen during the work, license recorded in the repository). |
| Q16 | Features | Undo, move history, flip the board, save and open a `.pgn` game (Chess associated with `.pgn` in "Open with"), **clock**. |
| Q32 | Clock | No clock, 5+0, 10+0, 15+10, or custom. |
| Q17 | Mobile | Desktop and mobile interface: tap the piece then the square, or drag. |

## What to do, in order

1. Wait for the HackerOS prerequisites: `hackeros-solution` template, serving
   the `wwwroot/` assets of installed apps, catalog and Hack Store (Store plan,
   work items 1 to 5).
2. Generate the solution with `dotnet new hackeros-solution` (submodule
   `external/HackerOs` on the tag of the targeted version).
3. SVG board, move input (mouse, touch, keyboard), rules by chess.js through
   JS interop.
4. Game against the computer: stockfish.js lite in a Web Worker, 5 levels,
   color choice.
5. History, undo, flip the board, clock.
6. `.pgn` files: save, open, file association in the manifest.
7. Tests (rules, PGN, clock) and a check on mobile.
8. `store/` folder (256×256 icon, French and English descriptions,
   screenshots, category and tags) and a first `v1.0.0` tag: the template
   workflow opens the PR to the catalog.

## Licenses

- This repository: GPL-3.0.
- stockfish.js: GPL-3.0. chess.js: BSD-2-Clause. Pieces: CC0 or MIT.

## Decisions of 2026-10-31

| Topic | Decision |
| --- | --- |
| Identifiers | Id `ca.kazo.chess`, publisher `pub.kazo`. |
| Threefold repetition and fifty-move draws | Automatic (as chess.js and chess.com), not claimed. |
| History for Stockfish | The engine receives the start position and every move (work item 3), so it sees repetitions. |

## Progress

On 2026-10-30, Benoit chose to start before the HackerOS prerequisites: the
solution starts from the NuGet 2.1.0 packages (`hackeros-window` and
`hackeros-host`) and will move to the `hackeros-solution` template once it
exists. The work goes through the `chess/base` series (one branch and one PR
per work item).

| Work item | Branch | State |
| --- | --- | --- |
| 1. SVG board, input (mouse, touch, keyboard), rules by chess.js, two players | `chess/1-board-and-rules` | Done ("celtic" pieces, MIT) |
| 2. Against the computer: stockfish.js lite, 5 levels, colour choice | `chess/2-engine` | Done (Stockfish 19 lite, 1.8 MB) |
| 3. History, undo, flip the board, clock | `chess/3-history-and-clock` | Done (Stockfish also receives the history) |
| 4. `.pgn` files, file association | `chess/4-pgn-files` | Done |
| 5. Move to the `hackeros-solution` template, `store/` folder, `v1.0.0` tag | | #246 to #249 done in `todo/base`; waits for them to reach `main` and the 2.2.0 release (ADR 0207 §11) |
