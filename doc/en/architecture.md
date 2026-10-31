> [Version française](../fr/architecture.md)

# Architecture of the Chess app

## Purpose

Chess is a HackerOS window app: a chessboard drawn in SVG in the colours of
the OS theme, the rules of the game from
[chess.js](https://github.com/jhlywa/chess.js), and (work item 2) a game
against the computer with stockfish.js. The goal and the decisions are in the
[plan](plan.md).

## Solution

```text
HackerOs-App-Chess/
├─ Directory.Build.props     # HackerOsVersion pinned once (2.1.0)
├─ Kazo.Chess.slnx
├─ src/
│  ├─ Kazo.Chess/            # the app (hackeros-window)
│  └─ Kazo.Chess.Os/         # debug host (hackeros-host) that references the app
├─ tests/
│  ├─ Kazo.Chess.Tests/      # C# unit tests (xUnit)
│  ├─ js/                    # tests of the rules module (node --test)
│  └─ e2e/                   # runs inside the host (node --test + Playwright)
└─ package.json              # tools of the JavaScript tests
```

**Starting on the NuGet 2.1.0 packages.** The `hackeros-solution` template
(Kazo-ca/HackerOs#246) does not exist yet; on 2026-10-30 Benoit chose to start
anyway. The solution therefore comes from `dotnet new hackeros-window` and
`dotnet new hackeros-host` (nuget.org packages). It will move to the
`hackeros-solution` layout (`external/HackerOs` submodule, Debug and Release
profiles) once that template exists.

**Debug host.** `Kazo.Chess.Os` is a full HackerOS with the base apps and
Chess as a `ProjectReference`. Because the app is a referenced project, its
`wwwroot/` files are served at `_content/Kazo.Chess/`: chess.js, the pieces and
the rules module already work. Installing through `hackpkg` or the Hack Store
waits for the OS to serve the `wwwroot/` files of installed apps
(Kazo-ca/HackerOs#247).

## The `Kazo.Chess` app

| Folder or file | Role |
| --- | --- |
| `ChessApp.cs` | Manifest (`[AppManifest]`): id `ca.kazo.chess`, publisher `pub.kazo`, category `games`, desktop and mobile, 860×640 window (minimum 360×420). |
| `ChessWindow.razor` (+ `.cs`, `.css`) | The window: toolbar ("New game"), board, status panel. It holds the game (`IChessRules`) and hands each `GameSnapshot` to the board. |
| `Components/ChessBoard.razor` (+ `.cs`, `.css`, `.js`) | The SVG board. It knows no rule: it draws the snapshot and turns mouse, touch and keyboard into `MoveRequest`s. |
| `Components/SvgText.cs` | An SVG `<text>` element (Razor reserves the `<text>` tag). |
| `Interaction/BoardInteraction.cs` | The player's hand over the board, immutable: selected piece, keyboard cursor, pending promotion. |
| `Model/` | `Square`, `Piece`, `BoardPosition` (FEN reading), `MoveRequest` (UCI notation), `GameSnapshot`, `GameStatus`. |
| `Rules/` | `IChessRules`, `ChessJsRules` (chess.js through JS interop) and `GameSnapshotJson` (snapshot reader). |
| `wwwroot/js/chess-rules.js` | Adapts chess.js: one game per `createGame`, and after each move a full JSON snapshot. |
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

## Building, running and testing

```bash
dotnet build Kazo.Chess.slnx
dotnet run --project src/Kazo.Chess.Os     # then open the address shown
dotnet test tests/Kazo.Chess.Tests         # C# unit tests
npm ci && npm test                         # rules module under node
npx playwright install chromium            # once
npm run test:e2e                           # runs inside the host
```

On first boot, HackerOS asks to create the administrator; Chess is in the
Applications menu ("All applications" on mobile).

| Suite | What it covers |
| --- | --- |
| `tests/Kazo.Chess.Tests` | `Square`, `BoardPosition`, `MoveRequest`, `GameStatus`, snapshot reading (on real chess.js snapshots in `Fixtures/`, written by `tests/js/write-fixtures.mjs`), `ChessJsRules` (on a fake JS runtime: a failed new game keeps the current one) and the whole board interaction. 91 tests, 98% of the lines of `Model/`, `Interaction/` and `Rules/`. |
| `tests/js` | The `chess-rules.js` module with chess.js: legal and illegal moves, mate, stalemate, promotion, insufficient material, fifty-move rule. |
| `tests/e2e` | The app in the host, in Chromium: moves by click, by drag and by keyboard; fool's mate (board locked); promotion (cancelled with the button, then chosen with the keyboard, the focus on the picker); phone screen (Pixel 7) and touch. `CHESS_BASE_URL` reuses a host already running. |

The CI (`.github/workflows/ci.yml`) runs all of it on every push and PR.

## Decisions taken during the work item

- **Id and publisher**: `ca.kazo.chess` and `pub.kazo` (question asked to
  Benoit, can change until the first `v1.0.0` tag).
- **Piece set**: "celtic" (MIT). Among the free sets lichess ships, only
  "celtic", "spatial", "fantasy" (MIT) and "rhosgfx" (CC0) meet decision Q15;
  "celtic" is the most readable and fits the gothic look.
- **Draws by threefold repetition and the fifty-move rule**: the game stops by
  itself, as chess.js reports it (`isDraw()`). Under FIDE rules these two draws
  are claimed; only fivefold repetition and the 75-move rule are automatic.
  Question asked to Benoit.
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

Next in the plan: work item 2 (stockfish.js), then history, undo, flip the
board, clock, `.pgn` files (see the [plan](plan.md)).
