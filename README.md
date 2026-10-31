> [Version française](readme-fr.md)

# HackerOs-App-Chess

Chess app for [HackerOS](https://github.com/Kazo-ca/HackerOs), first app of the Hack Store catalog:
an SVG board in the colours of the OS theme, rules by [chess.js](https://github.com/jhlywa/chess.js),
two players on the same screen or against the computer
([stockfish.js](https://github.com/nmrugg/stockfish.js), 5 levels), clock, undo, saved games (`.pgn`),
mouse, touch and keyboard.

```bash
git clone --recurse-submodules https://github.com/Kazo-ca/HackerOs-App-Chess.git
cd HackerOs-App-Chess
dotnet run --project src/Kazo.Chess.Os -c Release   # a HackerOS with Chess in the Applications menu
```

The repository follows the `hackeros-solution` template: `external/HackerOs` is HackerOS as a
submodule on its release tag (2.2.0), which sets the version of every HackerOS package.

- Plan: [English](doc/en/plan.md) · [Français](doc/fr/plan.md)
- Architecture, build and tests: [English](doc/en/architecture.md) · [Français](doc/fr/architecture.md)
- License: GPL-3.0 (bundles stockfish.js). Third-party files: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
