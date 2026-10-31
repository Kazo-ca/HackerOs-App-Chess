> [English version](README.md)

# HackerOs-App-Chess

App d'échecs pour [HackerOS](https://github.com/Kazo-ca/HackerOs), première app du catalogue du Hack
Store : un plateau SVG aux couleurs du thème de l'OS, les règles par
[chess.js](https://github.com/jhlywa/chess.js), deux joueurs sur le même écran ou contre l'ordinateur
([stockfish.js](https://github.com/nmrugg/stockfish.js), 5 niveaux), pendule, annuler, parties enregistrées
(`.pgn`), souris, toucher et clavier.

```bash
git clone --recurse-submodules https://github.com/Kazo-ca/HackerOs-App-Chess.git
cd HackerOs-App-Chess
dotnet run --project src/Kazo.Chess.Os -c Release   # un HackerOS avec Chess dans le menu Applications
```

Le dépôt suit le modèle `hackeros-solution` : `external/HackerOs` est HackerOS en sous-module sur
son tag de release (2.2.0), qui fixe la version de tous les paquets HackerOS.

- Plan : [Français](doc/fr/plan.md) · [English](doc/en/plan.md)
- Architecture, build et tests : [Français](doc/fr/architecture.md) · [English](doc/en/architecture.md)
- Licence : GPL-3.0 (embarque stockfish.js). Fichiers tiers : [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
