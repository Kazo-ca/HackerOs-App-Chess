> [Version française](readme-fr.md)

# Os-App-Chess

Chess app for [Kazo OS](https://github.com/Kazo-ca/Os), first app of the Kazo OS Store catalog:
an SVG board in the colours of the OS theme, rules by [chess.js](https://github.com/jhlywa/chess.js),
two players on the same screen or against the computer
([stockfish.js](https://github.com/nmrugg/stockfish.js), 5 levels), clock, undo, saved games (`.pgn`),
mouse, touch and keyboard.

```bash
git clone --recurse-submodules https://github.com/Kazo-ca/Os-App-Chess.git
cd Os-App-Chess
dotnet run --project src/Kazo.Chess.Os -c Release   # a Kazo OS with Chess in the Applications menu
```

In Kazo OS, Chess is installed from the Kazo OS Store, or in the Terminal with
`sudo kazopkg install Kazo-ca/Os-App-Chess` (this repository's `kazoos-packages` branch), once
the first version is published.

The repository follows the `kazoos-solution` template: `external/KazoOs` is Kazo OS as a
submodule on its release tag (3.0.0), which sets the version of every Kazo OS package.

- Plan: [English](doc/en/plan.md) · [Français](doc/fr/plan.md)
- Architecture, build and tests: [English](doc/en/architecture.md) · [Français](doc/fr/architecture.md)
- License: GPL-3.0 (bundles stockfish.js). Third-party files: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
