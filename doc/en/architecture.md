> [Version française](../fr/architecture.md)

# Architecture of the Chess app

## Purpose

Chess is a HackerOS window app: a chessboard drawn in SVG in the colours of
the OS theme, the rules of the game from
[chess.js](https://github.com/jhlywa/chess.js), two players on the same
screen or a game against the computer (Stockfish 19 from stockfish.js, in a Web
Worker). The goal and the decisions are in the
[plan](plan.md).

## Solution

```text
HackerOs-App-Chess/
├─ external/HackerOs/        # submodule: HackerOS on tag v2.2.0 (the repository's version)
├─ Directory.Build.props     # submodule root, profile, import of Versions.props
├─ Directory.Build.targets   # source mode in Debug, HOSAPP0070 without the submodule
├─ global.json
├─ Kazo.Chess.slnx
├─ src/
│  ├─ Kazo.Chess/            # the app
│  └─ Kazo.Chess.Os/         # host: HackerOS, its base apps and Chess
├─ tests/
│  ├─ Kazo.Chess.Tests/      # C# unit tests (xUnit)
│  ├─ js/                    # tests of the JavaScript modules (node --test)
│  └─ e2e/                   # runs inside the host (node --test + Playwright)
├─ store/                    # Hack Store listing: icon, descriptions, screenshots, store.json
├─ .github/workflows/        # ci.yml, publish-to-hack-store.yml
└─ package.json              # tools of the JavaScript tests
```

**`hackeros-solution` layout** (HackerOS 2.2.0, ADRs 0176 and 0207). The
repository follows the template's output: `external/HackerOs` is HackerOS as a
submodule, exactly on a release tag, and its `build/Versions.props` gives
**the** version of the repository. Changing the HackerOS version means moving
the submodule to another tag. The submodule is never modified: CI refuses a
modified submodule or one off its tag (`.github/scripts/check-hackeros-submodule.sh`).

| Profile | HackerOS comes from | Use |
| --- | --- | --- |
| `Debug` (`HackerOsUseSource=true`) | the submodule's projects (step into the SDK and the OS) | development, debugging |
| `Release` (`HackerOsUseSource=false`) | the `HackerOs.*` packages from nuget.org, at the submodule's version | CI, E2E tests, Hack Store |

Clone with `git clone --recurse-submodules`, or run `git submodule update
--init` afterwards (without the submodule the build stops on `HOSAPP0070`). In
Debug the build compiles HackerOS from the submodule's sources (2 to 3 minutes
the first time).

**Host.** `Kazo.Chess.Os` is a full HackerOS with the base apps (the Hack Store
among them) and Chess as a `ProjectReference`: its `wwwroot/` files are served
at `_content/Kazo.Chess/`.

## The `Kazo.Chess` app

| Folder or file | Role |
| --- | --- |
| `ChessApp.cs` | Manifest (`[AppManifest]`): id `ca.kazo.chess`, version `1.0.0`, publisher `pub.kazo`, category `entertainment` (the catalog's closed list), desktop and mobile, 860×640 window (minimum 360×420). Capabilities `dialogs.file-open`, `dialogs.file-save`, `filesystem.user-home.read` and `.write`; opens `.pgn` files (`application/x-chess-pgn`). |
| `Pgn/PgnTags.cs` | The tags of a saved game and its PGN result (`1-0`, `0-1`, `1/2-1/2`, `*`). |
| `ChessWindow.razor` (+ `.cs`, `.css`) | The window: toolbar ("New game"), board, status panel (turn, players, "The computer is thinking…"). It shows the `GameSession` and forwards what the player does. |
| `Components/NewGameDialog.razor` (+ `.cs`, `.css`) | The "New game" dialog, inside the window: two players or against the computer, level, colour. It replaces a confirmation box: it says when the current game will be abandoned. |
| `Game/GameSetup.cs` | How the game is set up: mode, level, the player's colour; side shown at the bottom; who may move. |
| `Game/GameSession.cs` | One game: the rules, the computer when it plays, the turn. Plays the computer's reply after each of the player's moves. |
| `Engine/` | `EngineLevel` (5 levels), `IChessEngine`, `EnginePosition` (start + moves), `StockfishEngine` (stockfish.js through JS interop, `bestmove` reading). |
| `Clock/` | `TimeControl` (5+0, 10+0, 15+10, custom), `ChessClock` (over a `TimeProvider`), `ClockFormat` (the face of a real clock). |
| `Components/MoveList.razor` | The score sheet: moves in pairs, the last one highlighted. |
| `Components/ChessBoard.razor` (+ `.cs`, `.css`, `.js`) | The SVG board. It knows no rule: it draws the snapshot and turns mouse, touch and keyboard into `MoveRequest`s. |
| `Components/SvgText.cs` | An SVG `<text>` element (Razor reserves the `<text>` tag). |
| `Interaction/BoardInteraction.cs` | The player's hand over the board, immutable: selected piece, keyboard cursor, pending promotion. |
| `Model/` | `Square`, `Piece`, `BoardPosition` (FEN reading), `MoveRequest` (UCI notation), `GameSnapshot`, `GameStatus`. |
| `Rules/` | `IChessRules`, `ChessJsRules` (chess.js through JS interop) and `GameSnapshotJson` (snapshot reader). |
| `wwwroot/js/chess-rules.js` | Adapts chess.js: one game per `createGame`, and after each move a full JSON snapshot. |
| `wwwroot/js/chess-engine.js` | Starts Stockfish in a Web Worker and speaks UCI to it. |
| `wwwroot/engine/` | Stockfish 19 "lite, single-threaded" from stockfish.js 19.0.1 (GPL-3.0): `.js` (28 KB) and `.wasm` (1.8 MB). |
| `wwwroot/lib/chess.js/` | chess.js 1.4.0 (BSD-2-Clause), ESM build of the npm package. |
| `wwwroot/pieces/celtic/` | The "celtic" pieces by Maurizio Monge (MIT). |
| `Resources/` | English and French texts (`Strings`, `AppManifest.fr`). |

### The contract with chess.js

Every rule comes from chess.js. After each call, the `chess-rules.js` module
returns **one** JSON string that describes the whole position; the C# side
never asks chess.js twice for one render:

```json
{
  "fen": "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1",
  "turn": "b",
  "inCheck": false,
  "checkmate": false, "stalemate": false,
  "insufficientMaterial": false, "threefoldRepetition": false, "fiftyMoves": false,
  "moves":   [{ "from": "b8", "to": "c6", "san": "Nc6", "promotion": null }],
  "history": [{ "from": "e2", "to": "e4", "san": "e4", "color": "w" }]
}
```

`GameSnapshotJson.Parse` reads it with a generated `System.Text.Json` context
(safe for the host published trimmed). A mate wins over a draw rule reported
at the same time. An illegal move returns `null` and the position does not
change.

### Playing against the computer

"New game" opens a dialog: two players on this screen, or against the computer
with a level and a colour (White, Black, random). Against the computer, the
board shows the player's side at the bottom and locks while the computer plays.

| Level | UCI `Skill Level` | Thinking time (`go movetime`) |
| --- | --- | --- |
| Beginner | 0 | 300 ms |
| Easy | 5 | 400 ms |
| Medium | 10 | 600 ms |
| Hard | 15 | 900 ms |
| Expert | 20 | 1,500 ms |

The engine is Stockfish 19 "lite, single-threaded" (stockfish.js): it runs in
a Web Worker, with no cross-origin isolation, and is only downloaded for the
first game against the computer (1.8 MB). `chess-engine.js` only speaks UCI
(`uci`, `isready`, `ucinewgame`, `setoption name Skill Level`, `position fen`,
`go movetime`, `stop`) and queues the searches.

`GameSession` sends every change of the game through one gate: a move and a new
game never interleave. Each new game takes its number **before** waiting for
the gate and cancels the running search (Stockfish stops at once): anything
waiting behind the gate for an older game (a start, a move, the computer's
turn) sees a newer number and gives up. Nothing of an old game reaches the new
one.

Failures:

- every request to the worker has a **deadline** (20 s for the UCI handshake,
  the thinking time plus 5 s for a search); a silent worker is ended and the
  error shows, the window never freezes;
- an engine that does not start is dropped and **one** fresh engine is tried;
- a new game that fails **leaves the current game as it was** (the engine and
  the rules are ready before the window switches games), and the computer takes
  its turn again if it was thinking;
- a worker that dies mid-game is reported; the next new game starts a fresh one;
- a worker created after the window closed is ended at once.

Since work item 3, Stockfish receives the start position and every move (see
above): it sees repetitions.

### History, undo, flip, clock

- **Score sheet**: moves in pairs (`1. e4 e5`), the last one highlighted; the
  list scrolls by itself to keep the last move in view (no script,
  `flex-direction: column-reverse`).
- **Undo move**: one move between two players; against the computer, its reply
  too, so it is the player's turn again. Disabled before the player's first
  move, while the computer thinks and **with a clock** (undoing would hand time
  back). chess.js takes the moves back (`undo`).
- **Flip board**: until the next game.
- **Clock** (Q32): none, 5+0, 10+0, 15+10 or custom (1 to 180 min, 0 to 60 s of
  increment).

  | Rule | Behaviour |
  | --- | --- |
  | Start | After White's first move (free, as on online servers). |
  | Increment | Added to a side after each of its timed moves. |
  | Display | `1:05:00`, `4:59`, then tenths below ten seconds (`9.4`); the running clock is lit, red below ten seconds. |
  | Out of time | That side loses; draw if the opponent has only its king. The computer's search stops if it is the computer. |
  | End of game | The clock stops on mate, stalemate or a draw. |
  | Flag during the move | If time runs out while the move is being played, time wins, with no increment. |
  | "New game" dialog | The clock keeps running, as a real one does: opening the dialog does not pause the game. |

  `ChessClock` reads the time from a `TimeProvider`: it measures real elapsed
  time (a slow render steals no seconds) and the tests move time by hand. The
  window redraws the clock every tenth of a second, and only while it runs.
- **Stockfish sees the game**: it receives the start position and every move
  (`position fen <start> moves e2e4 e7e5 …`), hence the repetitions.

### `.pgn` files

- **Save**: the SDK's save dialog suggests `chess-YYYY-MM-DD.pgn` (or the name
  of the open game); the file gets the seven tags (`Event "Casual game"`,
  `Site "HackerOS"`, the date, the players, the result) then the moves, written
  by chess.js. Against the computer the players are the login name and
  `Stockfish 19 lite (Skill N)`; between two players, `?`. The result follows
  the game: mate, draw, out of time, or `*` for a game still going.
- **Open**: the SDK's open dialog, a double-click on a `.pgn` in the file
  explorer, or "Open with" (the manifest declares the type). The game opens
  between two players, without a clock, at its last move; its moves are
  replayed (the score sheet, undo and Stockfish see them). A game started from a
  position (`[FEN]`) keeps that start. A game saved as over off the board
  (resignation, agreed draw) stays over until a move is taken back. An
  unreadable file leaves the current game and says why (chess.js names the
  first illegal move).
- The window title becomes `game.pgn — Chess`.
- **Safeguards**: every tag value is cleaned before writing (one line, no quote
  or backslash, at most 255 characters): a login name such as `Jo"hn` cannot
  break the file. A file over 64 KB or a game over 1,000 half-moves is refused
  (chess.js runs on the page's thread and would take seconds). Of a collection,
  the first game is loaded. A load that fails while the computer thinks gives
  it its turn back.
- **Limits**: a reopened game saved again gets new tags (`Casual game`, today's
  date); the other tags it had are kept. Files are read and written by
  `HackFile` with the home folder's
  rights; a file chosen elsewhere in the dialog is refused (the chosen handle is
  not used yet). The dialogs always get a starting folder: without it, the 2.1.0 (not checked again in 2.2.0)
  dialogs guess a home folder from the user's internal id, which does not exist,
  and the OS stops on the error.

### Entering moves

Two ways to play, as on a real board, with the mouse as with a finger:

- **touch the piece, then the square**: the reachable squares show (a dot on
  an empty square, a ring on a capture);
- **drag the piece** and drop it on the square; dropped elsewhere, it goes back
  and stays selected.

With the **keyboard**, the board takes the focus: the arrows move a cursor (in
screen direction, whatever side is at the bottom), Enter or Space picks the
piece then plays it, Escape cancels. The cursor also follows the last touch.
On a **promotion**, a Queen, Rook, Bishop, Knight picker opens and takes the
focus (keys `q`, `r`, `b`, `n` on the keyboard, a "Cancel" button or Escape to
go back). Keys with Ctrl, Alt or Meta are left to the OS and the browser. The
`aria-live` region announces the cursor square and the selected piece with its
number of moves.

Every decision is taken in C# by `BoardInteraction`. `ChessBoard.razor.js`
only does what C# cannot: turn pointer coordinates into square names, capture
the pointer, and draw the dragged piece under the finger with no .NET round
trip per pointer move.

### Theme

The colours come from the OS theme tokens (`--hos-*`): both square colours are
tinted by the theme's accent (over ivory and over slate, so they stay readable
in a dark theme as in a light one); last move in `--hos-warning`, check in
`--hos-error`, selection and cursor in `--hos-accent`. The board changes colour
with the theme (green on the default desktop, blue on the mobile theme).

### Mobile

`MobileEntryPoint` is the same window. When the window is narrower than
620 px, the panel moves under the board; the board keeps the largest square
that fits (`container-type: size`). `touch-action: none` on the board: a swipe
plays, it does not scroll the page.

## Publishing to the Hack Store

The template's `publish-to-hack-store.yml` workflow runs on a `v<version>` tag
(the manifest's `Version`): it checks the submodule, builds the app in Release,
packs it (`HackerOsPack`), assembles `apps/ca.kazo.chess_<version>/` from
`store/` and the manifest, and opens the pull request to
`Kazo-ca/HackerOs-AppMarketPlace` with the `HACK_STORE_TOKEN` secret (otherwise
it leaves the folder as an artifact).

**What still blocks (HackerOS 2.2.0).** An app installed from the Store only gets
the `wwwroot/` files **declared** in its manifest's `Assets` (ADR 0206). For
Chess, the `HackerOsPack` package therefore holds only `icons/app.svg`: chess.js,
the pieces, the modules and Stockfish are missing. Two things are not covered by
the OS at all: a component's collocated JavaScript
(`Components/ChessBoard.razor.js`) is not packed, and the app's scoped CSS
(`Kazo.Chess.bundle.scp.css`) is never loaded for an installed app. Every
third-party app written by the project's rules (CSS in `.razor.css`) is
affected. On 2026-10-31 Benoit chose to fix
HackerOS: `HackerOsPack` will pack every static resource of an app, and the OS
will load an installed app's scoped CSS. The `v1.0.0` tag waits for the
HackerOS version that brings it; Benoit merges `chess/base` into `main`, adds
the `HACK_STORE_TOKEN` secret and creates the tag.

## Building, running and testing

```bash
git submodule update --init                # once, if the repository was not cloned with --recurse-submodules
dotnet build Kazo.Chess.slnx -c Release    # packages profile (Debug: from the submodule's sources)
dotnet run --project src/Kazo.Chess.Os -c Release   # then open the address shown
dotnet test Kazo.Chess.slnx -c Release --no-build   # C# unit tests
npm ci && npm test                         # rules module under node
npx playwright install chromium            # once
npm run test:e2e                           # runs inside the host
```

On first boot, HackerOS asks to create the administrator; Chess is in the
Applications menu ("All applications" on mobile).

| Suite | What it covers |
| --- | --- |
| `tests/Kazo.Chess.Tests` | `Square`, `BoardPosition`, `MoveRequest`, `GameStatus`, snapshot reading (on real chess.js snapshots in `Fixtures/`, written by `tests/js/write-fixtures.mjs`), `ChessJsRules` (on a fake JS runtime: a failed new game keeps the current one) the whole board interaction, `GameSetup`, `GameSession` (with a fake engine: the computer's reply, a refused move, a new game while it thinks, an engine that fails to start) and `StockfishEngine` (fake worker: one start, UCI options, `stop` on cancellation, token already cancelled, close during start); `GameSession` races: two overlapping new games, a move asked during a start, a move found as a new game starts, a failed start, a `Changed` subscriber that throws; the clock (on a fake `TimeProvider`), time-out, undo, the position sent to the engine; PGN (results, tags, loaded game, recorded result, file too large, failed load while the computer thinks). 199 tests. |
| `tests/js` | The `chess-rules.js` module with chess.js: legal and illegal moves, mate, stalemate, promotion, insufficient material, fifty-move rule. The `chess-engine.js` module with a fake worker: UCI order, search queue, `stop`, deadlines, worker error, `terminate`. Undo, start position, promotion in the history; PGN round trip, `[FEN]` start, invalid text, hostile name, collection, over 1,000 half-moves. 26 tests. |
| `tests/e2e` | The app in the host, in Chromium: moves by click, by drag and by keyboard; fool's mate (board locked); promotion (cancelled with the button, then chosen with the keyboard, the focus on the picker); phone screen (Pixel 7) and touch; against Stockfish as Black (it opens, the player answers, it plays again); Escape closes the dialog and gives the focus back; a new game while Stockfish thinks (its move is not played); undo and flip between two players, undo against the computer, a 5+0 clock; save a game as `.pgn` and open it again. 11 runs. `CHESS_BASE_URL` reuses a host already running. |

The CI (`.github/workflows/ci.yml`) runs all of it on every push and PR.

## Decisions taken during the work item

- **Id and publisher**: `ca.kazo.chess` and `pub.kazo` (confirmed by Benoit on
  2026-10-31).
- **Piece set**: "celtic" (MIT). Among the free sets lichess ships, only
  "celtic", "spatial", "fantasy" (MIT) and "rhosgfx" (CC0) meet decision Q15;
  "celtic" is the most readable and fits the gothic look.
- **Draws by threefold repetition and the fifty-move rule**: the game stops by
  itself, as chess.js reports it (`isDraw()`) and as on chess.com. Under FIDE
  rules these draws are claimed; Benoit chose the automatic stop on
  2026-10-31.
- **Errors**: an error of the rules (JavaScript) shows in the panel and the
  game stays as it was; if the board script does not load, the keyboard still
  plays.
- **Third-party files in the repository**: chess.js and the pieces are copied
  into `wwwroot/` (no download at build time), licenses next to them and in
  [`THIRD-PARTY-NOTICES.md`](../../THIRD-PARTY-NOTICES.md).

## Task list

Work item 1 (`chess/1-board-and-rules`):

- [x] Solution generated by `hackeros-window` and `hackeros-host` (NuGet 2.1.0).
- [x] chess.js served by the app, `chess-rules.js` module, snapshot reader.
- [x] SVG board in the theme colours, "celtic" pieces.
- [x] Mouse (click and drag), touch, keyboard, promotion.
- [x] Two players on the same screen, game status, "New game".
- [x] C#, node and end-to-end tests; mobile checked (Pixel 7).
- [x] GitHub Actions CI.

Work item 2 (`chess/2-engine`):

- [x] Stockfish 19 "lite, single-threaded" in a Web Worker, loaded on demand.
- [x] 5 levels (`Skill Level` 0, 5, 10, 15, 20), colour choice (random included).
- [x] "New game" dialog inside the window, board turned to the player.
- [x] `GameSession`: the computer's replies, cancellation on a new game.
- [x] C# tests (fake engine and fake worker) and end to end against Stockfish.
- [x] Independent review: game number taken before the gate, worker deadlines,
  a failed start leaves the current game alone, dialog focus and keyboard
  (Escape, Tab, back to the button).

Work item 3 (`chess/3-history-and-clock`):

- [x] Score sheet, undo move, flip board.
- [x] Clock: none, 5+0, 10+0, 15+10, custom; out of time.
- [x] Stockfish receives the start and every move.
- [x] C# (clock on a fake `TimeProvider`), node and end-to-end tests.

Work item 4 (`chess/4-pgn-files`):

- [x] Save as `.pgn` (tags, result), open a `.pgn`.
- [x] `.pgn` association in the manifest, the launch file opened.
- [x] C#, node and end-to-end tests (save then open again).

Work item 5 (`chess/5-solution-template`):

- [x] `external/HackerOs` submodule on `v2.2.0`, `Directory.Build.props/targets`,
  `global.json`, submodule check, Debug (sources) and Release (packages) profiles
  checked.
- [x] CI on the Release profile (submodule checked, C#, node, E2E);
  `publish-to-hack-store.yml` workflow.
- [x] `store/` folder: 256×256 icon, short and long descriptions (fr, en),
  screenshots, `store.json` (category `entertainment`, GPL-3.0 licence).
- [x] Version `1.0.0` in the manifest, category `entertainment`.
- [ ] Resources of the app when installed by the Store: waits for the HackerOS
  fix (see "Publishing" below), then the submodule moves to the new version.
- [ ] `v1.0.0` tag (created by Benoit after the merge into `main`) and pull
  request to the catalog (issue #3).
