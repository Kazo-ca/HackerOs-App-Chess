> [English version](README.md)

# Os-App-Chess

App d'échecs pour [Kazo OS](https://github.com/Kazo-ca/Os), première app du catalogue du Kazo OS
Store : un plateau SVG aux couleurs du thème de l'OS, les règles par
[chess.js](https://github.com/jhlywa/chess.js), deux joueurs sur le même écran ou contre l'ordinateur
([stockfish.js](https://github.com/nmrugg/stockfish.js), 5 niveaux), pendule, annuler, parties enregistrées
(`.pgn`), souris, toucher et clavier.

```bash
git clone --recurse-submodules https://github.com/Kazo-ca/Os-App-Chess.git
cd Os-App-Chess
dotnet run --project src/Kazo.Chess.Os -c Release   # un Kazo OS avec Chess dans le menu Applications
```

Dans Kazo OS, Chess s'installe depuis le Kazo OS Store, ou dans le Terminal avec
`sudo kazopkg install Kazo-ca/Os-App-Chess` (branche `kazoos-packages` de ce dépôt), une fois
la première version publiée.

Le dépôt suit le modèle `kazoos-solution` : `external/KazoOs` est Kazo OS en sous-module sur
son tag de release (3.0.0), qui fixe la version de tous les paquets Kazo OS.

- Plan : [Français](doc/fr/plan.md) · [English](doc/en/plan.md)
- Architecture, build et tests : [Français](doc/fr/architecture.md) · [English](doc/en/architecture.md)
- Licence : GPL-3.0 (embarque stockfish.js). Fichiers tiers : [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
