> [English version](../en/architecture.md)

# Architecture de l'app Chess

## But

Chess est une app fenêtrée de HackerOS : un échiquier dessiné en SVG aux
couleurs du thème de l'OS, les règles du jeu par
[chess.js](https://github.com/jhlywa/chess.js), et (chantier 2) une partie
contre l'ordinateur par stockfish.js. Le but et les décisions sont dans le
[plan](plan.md).

## Solution

```text
HackerOs-App-Chess/
├─ Directory.Build.props     # HackerOsVersion épinglée une fois (2.1.0)
├─ Kazo.Chess.slnx
├─ src/
│  ├─ Kazo.Chess/            # l'app (hackeros-window)
│  └─ Kazo.Chess.Os/         # hôte de débogage (hackeros-host) qui référence l'app
├─ tests/
│  ├─ Kazo.Chess.Tests/      # tests unitaires C# (xUnit)
│  ├─ js/                    # tests du module des règles (node --test)
│  └─ e2e/                   # parcours dans l'hôte (node --test + Playwright)
└─ package.json              # outils des tests JavaScript
```

**Départ sur les paquets NuGet 2.1.0.** Le modèle `hackeros-solution`
(Kazo-ca/HackerOs#246) n'existe pas encore ; Benoit a choisi le 2026-10-30 de
commencer quand même. La solution vient donc de `dotnet new hackeros-window` et
`dotnet new hackeros-host` (paquets de nuget.org). Elle passera à la structure
`hackeros-solution` (sous-module `external/HackerOs`, profils Debug et Release)
quand ce modèle existera.

**Hôte de débogage.** `Kazo.Chess.Os` est un HackerOS complet avec les apps de
base et Chess en `ProjectReference`. Comme l'app est un projet référencé, ses
fichiers `wwwroot/` sont servis à `_content/Kazo.Chess/` : chess.js, les
pièces et le module des règles marchent déjà. L'installation par `hackpkg` ou
le Hack Store attend que l'OS serve les fichiers `wwwroot/` des apps installées
(Kazo-ca/HackerOs#247).

## L'app `Kazo.Chess`

| Dossier ou fichier | Rôle |
| --- | --- |
| `ChessApp.cs` | Manifeste (`[AppManifest]`) : id `ca.kazo.chess`, éditeur `pub.kazo`, catégorie `games`, bureau et mobile, fenêtre 860×640 (minimum 360×420). |
| `ChessWindow.razor` (+ `.cs`, `.css`) | La fenêtre : barre d'outils (« Nouvelle partie »), plateau, panneau d'état. Elle tient la partie (`IChessRules`) et passe chaque `GameSnapshot` au plateau. |
| `Components/ChessBoard.razor` (+ `.cs`, `.css`, `.js`) | Le plateau SVG. Il ne connaît aucune règle : il dessine le snapshot et transforme souris, toucher et clavier en `MoveRequest`. |
| `Components/SvgText.cs` | Un élément SVG `<text>` (Razor réserve la balise `<text>`). |
| `Interaction/BoardInteraction.cs` | La main du joueur au-dessus du plateau, immuable : pièce choisie, curseur clavier, promotion en attente. |
| `Model/` | `Square`, `Piece`, `BoardPosition` (lecture du FEN), `MoveRequest` (notation UCI), `GameSnapshot`, `GameStatus`. |
| `Rules/` | `IChessRules`, `ChessJsRules` (chess.js par JS interop) et `GameSnapshotJson` (lecture du snapshot). |
| `wwwroot/js/chess-rules.js` | Adapte chess.js : une partie par `createGame`, et après chaque coup un snapshot JSON complet. |
| `wwwroot/lib/chess.js/` | chess.js 1.4.0 (BSD-2-Clause), build ESM du paquet npm. |
| `wwwroot/pieces/celtic/` | Les pièces « celtic » de Maurizio Monge (MIT). |
| `Resources/` | Textes en anglais et en français (`Strings`, `AppManifest.fr`). |

### Le contrat avec chess.js

Toutes les règles viennent de chess.js. Après chaque appel, le module
`chess-rules.js` renvoie **une** chaîne JSON qui décrit toute la position ; le
C# ne pose jamais deux questions à chess.js pour un affichage :

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

`GameSnapshotJson.Parse` le lit avec un contexte `System.Text.Json` généré
(sûr pour l'hôte publié en mode trimmé). Un mat l'emporte sur une règle de
nulle signalée en même temps. Un coup illégal renvoie `null` et la position ne
change pas.

### Saisie des coups

Deux façons de jouer, comme sur un vrai plateau, avec la souris comme au
doigt :

- **toucher la pièce, puis la case** : les cases atteignables s'affichent
  (point pour une case vide, anneau pour une prise) ;
- **glisser la pièce** et la lâcher sur la case ; lâchée ailleurs, elle revient
  et reste choisie.

Au **clavier**, le plateau prend le focus : les flèches déplacent un curseur
(dans le sens de l'écran, quel que soit le côté affiché en bas), Entrée ou
Espace prend la pièce puis la joue, Échap annule. Le curseur suit aussi le
dernier toucher. À la **promotion**, un choix Dame, Tour, Fou, Cavalier
s'ouvre et prend le focus (touches `q`, `r`, `b`, `n` au clavier, bouton
« Annuler » ou Échap pour revenir). Les touches avec Ctrl, Alt ou Méta restent
à l'OS et au navigateur. La zone `aria-live` annonce la case du curseur et la
pièce choisie avec son nombre de coups.

Toutes les décisions sont prises en C# par `BoardInteraction`.
`ChessBoard.razor.js` ne fait que ce que le C# ne peut pas faire : convertir
les coordonnées du pointeur en nom de case, capturer le pointeur, et dessiner
la pièce glissée sous le doigt sans aller-retour .NET à chaque mouvement.

### Thème

Les couleurs viennent des jetons du thème de l'OS (`--hos-*`) : les deux
couleurs de case sont teintées par l'accent du thème (sur ivoire et sur
ardoise, pour rester lisibles en thème sombre comme en thème clair) ; dernier
coup en `--hos-warning`, échec en `--hos-error`, sélection et curseur en
`--hos-accent`. Le plateau change de couleur avec le thème (vert sur le bureau
par défaut, bleu sur le thème mobile).

### Mobile

`MobileEntryPoint` est la même fenêtre. Quand la fenêtre fait moins de 620 px
de large, le panneau passe sous le plateau ; le plateau garde la plus grande
taille carrée qui tient (`container-type: size`). `touch-action: none` sur le
plateau : un glissement joue, il ne fait pas défiler la page.

## Construire, lancer et tester

```bash
dotnet build Kazo.Chess.slnx
dotnet run --project src/Kazo.Chess.Os     # puis ouvrir l'adresse affichée
dotnet test tests/Kazo.Chess.Tests         # tests unitaires C#
npm ci && npm test                         # module des règles sous node
npx playwright install chromium            # une fois
npm run test:e2e                           # parcours dans l'hôte
```

Au premier démarrage, HackerOS demande de créer l'administrateur ; Chess est
dans le menu Applications (« Toutes les applications » sur mobile).

| Suite | Ce qu'elle couvre |
| --- | --- |
| `tests/Kazo.Chess.Tests` | `Square`, `BoardPosition`, `MoveRequest`, `GameStatus`, lecture des snapshots (sur des snapshots réels de chess.js dans `Fixtures/`, écrits par `tests/js/write-fixtures.mjs`), `ChessJsRules` (sur un faux runtime JS : une nouvelle partie qui échoue garde l'ancienne) et toute l'interaction du plateau. 91 tests, 98 % des lignes de `Model/`, `Interaction/` et `Rules/`. |
| `tests/js` | Le module `chess-rules.js` avec chess.js : coups légaux et illégaux, mat, pat, promotion, matériel insuffisant, règle des 50 coups. |
| `tests/e2e` | L'app dans l'hôte, dans Chromium : coups au clic, au glisser et au clavier ; mat du berger (plateau verrouillé) ; promotion (annulée au bouton, puis choisie au clavier avec le focus sur le sélecteur) ; écran de téléphone (Pixel 7) et toucher. `CHESS_BASE_URL` réutilise un hôte déjà lancé. |

La CI (`.github/workflows/ci.yml`) fait tout cela à chaque push et à chaque PR.

## Décisions prises pendant le chantier

- **Id et éditeur** : `ca.kazo.chess` et `pub.kazo` (question posée à Benoit,
  modifiable jusqu'au premier tag `v1.0.0`).
- **Jeu de pièces** : « celtic » (MIT). Parmi les jeux libres repris par
  lichess, seuls « celtic », « spatial », « fantasy » (MIT) et « rhosgfx »
  (CC0) respectent la décision Q15 ; « celtic » est le plus lisible et va avec
  l'esthétique gothique.
- **Nulles par triple répétition et règle des 50 coups** : la partie s'arrête
  d'elle-même, comme chess.js le signale (`isDraw()`). Aux règles de la FIDE,
  ces deux nulles se réclament ; seules la quintuple répétition et les 75 coups
  sont automatiques. Question posée à Benoit.
- **Erreurs** : une erreur des règles (JavaScript) s'affiche dans le panneau et
  la partie reste telle quelle ; si le script du plateau ne se charge pas, le
  clavier joue toujours.
- **Fichiers tiers dans le dépôt** : chess.js et les pièces sont copiés dans
  `wwwroot/` (pas de téléchargement au build), licences à côté et dans
  [`THIRD-PARTY-NOTICES.md`](../../THIRD-PARTY-NOTICES.md).

## Choses à faire

Chantier 1 (`chess/1-board-and-rules`) :

- [x] Solution générée par `hackeros-window` et `hackeros-host` (NuGet 2.1.0).
- [x] chess.js servi par l'app, module `chess-rules.js`, lecture du snapshot.
- [x] Plateau SVG aux couleurs du thème, pièces « celtic ».
- [x] Souris (clic et glisser), toucher, clavier, promotion.
- [x] Deux joueurs sur le même écran, état de la partie, « Nouvelle partie ».
- [x] Tests C#, node et de bout en bout ; mobile vérifié (Pixel 7).
- [x] CI GitHub Actions.

Suite du plan : chantier 2 (stockfish.js), puis historique, annuler, retourner
le plateau, pendule, fichiers `.pgn` (voir le [plan](plan.md)).
