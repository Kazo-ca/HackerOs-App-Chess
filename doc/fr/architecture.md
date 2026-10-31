> [English version](../en/architecture.md)

# Architecture de l'app Chess

## But

Chess est une app fenêtrée de HackerOS : un échiquier dessiné en SVG aux
couleurs du thème de l'OS, les règles du jeu par
[chess.js](https://github.com/jhlywa/chess.js), deux joueurs sur le même écran
ou une partie contre l'ordinateur (Stockfish 19 par stockfish.js, dans un Web
Worker). Le but et les décisions sont dans le
[plan](plan.md).

## Solution

```text
HackerOs-App-Chess/
├─ external/HackerOs/        # sous-module : HackerOS sur le tag v2.2.0 (la version du dépôt)
├─ Directory.Build.props     # racine du sous-module, profil, import de Versions.props
├─ Directory.Build.targets   # mode source en Debug, HOSAPP0070 sans sous-module
├─ global.json
├─ Kazo.Chess.slnx
├─ src/
│  ├─ Kazo.Chess/            # l'app
│  └─ Kazo.Chess.Os/         # hôte : HackerOS, ses apps de base et Chess
├─ tests/
│  ├─ Kazo.Chess.Tests/      # tests unitaires C# (xUnit)
│  ├─ js/                    # tests des modules JavaScript (node --test)
│  └─ e2e/                   # parcours dans l'hôte (node --test + Playwright)
├─ store/                    # fiche du Hack Store : icône, descriptions, captures, store.json
├─ .github/workflows/        # ci.yml, publish-to-hack-store.yml
└─ package.json              # outils des tests JavaScript
```

**Structure du modèle `hackeros-solution`** (HackerOS 2.2.0, ADR 0176 et
0207). Le dépôt suit la sortie du modèle : `external/HackerOs` est HackerOS en
sous-module, exactement sur un tag de release, et son `build/Versions.props`
donne **la** version du dépôt. Changer de version de HackerOS, c'est déplacer le
sous-module sur un autre tag. On ne modifie jamais le sous-module : la CI refuse
un sous-module modifié ou hors de son tag (`.github/scripts/check-hackeros-submodule.sh`).

| Profil | HackerOS vient de | Usage |
| --- | --- | --- |
| `Debug` (`HackerOsUseSource=true`) | les projets du sous-module (on entre dans le SDK et l'OS) | développement, débogage |
| `Release` (`HackerOsUseSource=false`) | les paquets `HackerOs.*` de nuget.org, à la version du sous-module | CI, tests E2E, Hack Store |

Cloner : `git clone --recurse-submodules`, ou `git submodule update --init`
après coup (sans sous-module, le build s'arrête sur `HOSAPP0070`). En Debug, le
build compile HackerOS depuis les sources du sous-module (2 à 3 minutes la
première fois).

**Hôte.** `Kazo.Chess.Os` est un HackerOS complet avec les apps de base (dont le
Hack Store) et Chess en `ProjectReference` : ses fichiers `wwwroot/` sont servis
à `_content/Kazo.Chess/`.

## L'app `Kazo.Chess`

| Dossier ou fichier | Rôle |
| --- | --- |
| `ChessApp.cs` | Manifeste (`[AppManifest]`) : id `ca.kazo.chess`, version `1.0.0`, éditeur `pub.kazo`, catégorie `entertainment` (liste fermée du catalogue), bureau et mobile, fenêtre 860×640 (minimum 360×420). Capacités `dialogs.file-open`, `dialogs.file-save`, `filesystem.user-home.read` et `.write` ; ouvre les `.pgn` (`application/x-chess-pgn`). |
| `Pgn/PgnTags.cs` | Les balises d'une partie enregistrée et son résultat PGN (`1-0`, `0-1`, `1/2-1/2`, `*`). |
| `ChessWindow.razor` (+ `.cs`, `.css`) | La fenêtre : barre d'outils (« Nouvelle partie »), plateau, panneau d'état (tour, joueurs, « L'ordinateur réfléchit… »). Elle affiche la `GameSession` et lui passe ce que fait le joueur. |
| `Components/NewGameDialog.razor` (+ `.cs`, `.css`) | Le dialogue « Nouvelle partie », dans la fenêtre : deux joueurs ou contre l'ordinateur, niveau, couleur. Il remplace une boîte de confirmation : il dit quand la partie en cours sera abandonnée. |
| `Game/GameSetup.cs` | Comment la partie est montée : mode, niveau, couleur du joueur ; côté affiché en bas ; qui peut bouger. |
| `Game/GameSession.cs` | Une partie : les règles, l'ordinateur quand il joue, le tour. Joue la réponse de l'ordinateur après chaque coup du joueur. |
| `Engine/` | `EngineLevel` (5 niveaux), `IChessEngine`, `EnginePosition` (départ + coups), `StockfishEngine` (stockfish.js par JS interop, lecture de `bestmove`). |
| `Clock/` | `TimeControl` (5+0, 10+0, 15+10, personnalisée), `ChessClock` (sur un `TimeProvider`), `ClockFormat` (affichage d'une vraie pendule). |
| `Components/MoveList.razor` | La feuille de partie : les coups par paires, le dernier en évidence. |
| `Components/ChessBoard.razor` (+ `.cs`, `.css`, `.js`) | Le plateau SVG. Il ne connaît aucune règle : il dessine le snapshot et transforme souris, toucher et clavier en `MoveRequest`. |
| `Components/SvgText.cs` | Un élément SVG `<text>` (Razor réserve la balise `<text>`). |
| `Interaction/BoardInteraction.cs` | La main du joueur au-dessus du plateau, immuable : pièce choisie, curseur clavier, promotion en attente. |
| `Model/` | `Square`, `Piece`, `BoardPosition` (lecture du FEN), `MoveRequest` (notation UCI), `GameSnapshot`, `GameStatus`. |
| `Rules/` | `IChessRules`, `ChessJsRules` (chess.js par JS interop) et `GameSnapshotJson` (lecture du snapshot). |
| `wwwroot/js/chess-rules.js` | Adapte chess.js : une partie par `createGame`, et après chaque coup un snapshot JSON complet. |
| `wwwroot/js/chess-engine.js` | Lance Stockfish dans un Web Worker et lui parle en UCI. |
| `wwwroot/engine/` | Stockfish 19 « lite, single-threaded » de stockfish.js 19.0.1 (GPL-3.0) : `.js` (28 Ko) et `.wasm` (1,8 Mo). |
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

### Partie contre l'ordinateur

« Nouvelle partie » ouvre un dialogue : deux joueurs sur cet écran, ou contre
l'ordinateur avec un niveau et une couleur (Blancs, Noirs, au hasard). Contre
l'ordinateur, le plateau montre le côté du joueur en bas et se verrouille
pendant que l'ordinateur joue.

| Niveau | `Skill Level` UCI | Temps de réflexion (`go movetime`) |
| --- | --- | --- |
| Débutant | 0 | 300 ms |
| Facile | 5 | 400 ms |
| Moyen | 10 | 600 ms |
| Difficile | 15 | 900 ms |
| Expert | 20 | 1 500 ms |

Le moteur est Stockfish 19 « lite, single-threaded » (stockfish.js) : il tourne
dans un Web Worker, sans isolation cross-origin, et ne se télécharge qu'à la
première partie contre l'ordinateur (1,8 Mo). `chess-engine.js` ne fait que
parler UCI (`uci`, `isready`, `ucinewgame`, `setoption name Skill Level`,
`position fen`, `go movetime`, `stop`) et met les recherches en file.

`GameSession` fait passer chaque changement de la partie par une seule porte :
un coup et une nouvelle partie ne se croisent jamais. Chaque nouvelle partie
prend son numéro **avant** d'attendre la porte et annule la recherche en cours
(Stockfish s'arrête tout de suite) : tout ce qui attendait derrière la porte
pour une partie plus ancienne (un départ, un coup, le tour de l'ordinateur) voit
un numéro plus récent et abandonne. Rien d'une ancienne partie n'arrive dans la
nouvelle.

Pannes :

- chaque demande au worker a une **échéance** (20 s pour la poignée de main
  UCI, le temps de réflexion plus 5 s pour une recherche) ; un worker muet est
  arrêté et l'erreur s'affiche, la fenêtre ne se fige jamais ;
- un moteur qui ne démarre pas est jeté et **un** moteur neuf est essayé ;
- une nouvelle partie qui échoue **laisse la partie en cours telle quelle** (le
  moteur et les règles sont prêts avant que la fenêtre change de partie), et
  l'ordinateur reprend son tour s'il réfléchissait ;
- un worker qui meurt en cours de partie est signalé ; la nouvelle partie
  suivante en lance un neuf ;
- un worker créé après la fermeture de la fenêtre est arrêté aussitôt.

Depuis le chantier 3, Stockfish reçoit la position de départ et tous les coups
(voir plus haut) : il voit les répétitions.

### Historique, annuler, retourner, pendule

- **Feuille de partie** : les coups par paires (`1. e4 e5`), le dernier en
  évidence ; la liste défile seule pour garder le dernier coup en vue (sans
  script, par `flex-direction: column-reverse`).
- **Annuler le coup** : un coup entre deux joueurs ; contre l'ordinateur, sa
  réponse aussi, pour revenir au tour du joueur. Désactivé avant le premier coup
  du joueur, pendant que l'ordinateur réfléchit et **avec une pendule** (annuler
  rendrait du temps). chess.js défait les coups (`undo`).
- **Retourner le plateau** : jusqu'à la partie suivante.
- **Pendule** (Q32) : sans, 5+0, 10+0, 15+10 ou personnalisée (1 à 180 min,
  0 à 60 s d'incrément).

  | Règle | Comportement |
  | --- | --- |
  | Départ | Après le premier coup des Blancs (gratuit, comme sur les serveurs en ligne). |
  | Incrément | Ajouté à un camp après chacun de ses coups chronométrés. |
  | Affichage | `1:05:00`, `4:59`, puis les dixièmes sous dix secondes (`9.4`) ; la pendule qui tourne est éclairée, rouge sous dix secondes. |
  | Temps écoulé | Le camp perd ; nulle si l'adversaire n'a plus que son roi. La recherche de l'ordinateur s'arrête si c'est lui. |
  | Fin de partie | La pendule s'arrête au mat, au pat ou à la nulle. |
  | Drapeau pendant le coup | Si le temps s'épuise pendant que le coup est joué, le temps l'emporte, sans incrément. |
  | Dialogue « Nouvelle partie » | La pendule continue, comme une vraie : ouvrir le dialogue ne met pas la partie en pause. |

  `ChessClock` lit le temps d'un `TimeProvider` : il mesure le temps réel écoulé
  (un rendu lent ne vole pas de secondes) et les tests avancent le temps à la
  main. La fenêtre redessine la pendule tous les dixièmes de seconde, et seulement
  quand elle tourne.
- **Stockfish voit la partie** : il reçoit la position de départ et tous les
  coups (`position fen <départ> moves e2e4 e7e5 …`), donc les répétitions.

### Fichiers `.pgn`

- **Enregistrer** : le dialogue d'enregistrement du SDK propose
  `chess-AAAA-MM-JJ.pgn` (ou le nom de la partie ouverte) ; le fichier reçoit
  les sept balises (`Event "Casual game"`, `Site "HackerOS"`, la date, les
  joueurs, le résultat) puis les coups, écrits par chess.js. Contre
  l'ordinateur, les joueurs sont l'identifiant de connexion et
  `Stockfish 19 lite (Skill N)` ; entre deux joueurs, `?`. Le résultat suit la
  partie : mat, nulle, temps écoulé, ou `*` pour une partie en cours.
- **Ouvrir** : le dialogue d'ouverture du SDK, ou un double-clic sur un `.pgn`
  dans l'explorateur, ou « Ouvrir avec » (le manifeste déclare le type). La
  partie s'ouvre entre deux joueurs, sans pendule, à son dernier coup ; ses coups
  sont rejoués (feuille de partie, annuler et Stockfish les voient). Une partie
  partant d'une position (`[FEN]`) garde ce départ. Une partie enregistrée comme
  terminée hors de l'échiquier (abandon, nulle d'accord) reste terminée jusqu'à
  ce qu'on annule un coup. Un fichier illisible laisse la partie en cours et dit
  pourquoi (chess.js nomme le premier coup illégal).
- Le titre de la fenêtre devient `partie.pgn — Échecs`.
- **Garde-fous** : chaque valeur de balise est nettoyée avant l'écriture (une
  ligne, pas de guillemet ni de barre oblique inverse, 255 caractères au plus) :
  un identifiant comme `Jo"hn` ne casse pas le fichier. Un fichier de plus de
  64 Ko ou une partie de plus de 1 000 demi-coups est refusé (chess.js tourne
  sur le fil de la page et mettrait des secondes). D'une collection, la première
  partie est chargée. Un chargement raté pendant que l'ordinateur réfléchit lui
  rend son tour.
- **Limites** : une partie rouverte puis enregistrée reçoit de nouvelles balises
  (`Casual game`, la date du jour) ; les autres balises qu'elle avait sont
  gardées. Les fichiers sont lus et écrits par `HackFile` avec les droits du
  dossier personnel ; un fichier choisi ailleurs dans le dialogue est refusé
  (le handle choisi n'est pas encore utilisé). Les dialogues reçoivent toujours
  un dossier de départ : sans lui, ceux de la 2.1.0 (pas revérifié en 2.2.0) devinent un dossier
  personnel à partir de l'identifiant interne de l'utilisateur, qui n'existe
  pas, et l'OS s'arrête sur l'erreur.

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

## Publication au Hack Store

Le workflow `publish-to-hack-store.yml` du modèle tourne sur un tag `v<version>`
(la `Version` du manifeste) : il vérifie le sous-module, compile l'app en Release,
la met en paquet (`HackerOsPack`), assemble `apps/ca.kazo.chess_<version>/` depuis
`store/` et le manifeste, et ouvre la pull request vers
`Kazo-ca/HackerOs-AppMarketPlace` avec le secret `HACK_STORE_TOKEN` (sinon il
laisse le dossier en artefact).

**Ce qui bloque encore (HackerOS 2.2.0).** Une app installée depuis le Store
reçoit seulement les fichiers `wwwroot/` **déclarés** dans les `Assets` de son
manifeste (ADR 0206). Pour Chess, le paquet `HackerOsPack` ne contient donc que
`icons/app.svg` : chess.js, les pièces, les modules et Stockfish manquent. Deux
choses ne sont pas couvertes du tout par l'OS : le JavaScript collocalisé d'un
composant (`Components/ChessBoard.razor.js`) n'est pas mis en paquet, et le CSS
isolé de l'app (`Kazo.Chess.bundle.scp.css`) n'est jamais chargé pour une app
installée. Toute app tierce écrite selon les règles du projet (CSS dans
`.razor.css`) est concernée. Benoit a choisi le 2026-10-31 de
corriger HackerOS : `HackerOsPack` mettra en paquet toutes les ressources
statiques d'une app, et l'OS chargera le CSS isolé d'une app installée. Le tag
`v1.0.0` attend la version de HackerOS qui l'apporte ; Benoit fusionne
`chess/base` dans `main`, ajoute le secret `HACK_STORE_TOKEN` et crée le tag.

## Construire, lancer et tester

```bash
git submodule update --init                # une fois, si le dépôt n'a pas été cloné avec --recurse-submodules
dotnet build Kazo.Chess.slnx -c Release    # profil des paquets (Debug : depuis les sources du sous-module)
dotnet run --project src/Kazo.Chess.Os -c Release   # puis ouvrir l'adresse affichée
dotnet test Kazo.Chess.slnx -c Release --no-build   # tests unitaires C#
npm ci && npm test                         # module des règles sous node
npx playwright install chromium            # une fois
npm run test:e2e                           # parcours dans l'hôte
```

Au premier démarrage, HackerOS demande de créer l'administrateur ; Chess est
dans le menu Applications (« Toutes les applications » sur mobile).

| Suite | Ce qu'elle couvre |
| --- | --- |
| `tests/Kazo.Chess.Tests` | `Square`, `BoardPosition`, `MoveRequest`, `GameStatus`, lecture des snapshots (sur des snapshots réels de chess.js dans `Fixtures/`, écrits par `tests/js/write-fixtures.mjs`), `ChessJsRules` (sur un faux runtime JS : une nouvelle partie qui échoue garde l'ancienne) toute l'interaction du plateau, `GameSetup`, `GameSession` (avec un faux moteur : réponse de l'ordinateur, coup refusé, nouvelle partie pendant qu'il réfléchit, moteur qui ne démarre pas) et `StockfishEngine` (faux worker : un seul démarrage, options UCI, `stop` à l'annulation, jeton déjà annulé, fermeture pendant le démarrage) ; courses de `GameSession` : deux nouvelles parties qui se chevauchent, coup demandé pendant un départ, coup trouvé au moment d'une nouvelle partie, départ qui échoue, abonné de `Changed` qui lève une exception ; la pendule (sur un faux `TimeProvider`), le temps écoulé, l'annulation, la position envoyée au moteur ; le PGN (résultats, balises, partie chargée, résultat enregistré, fichier trop grand, chargement raté pendant que l'ordinateur réfléchit). 199 tests. |
| `tests/js` | Le module `chess-rules.js` avec chess.js : coups légaux et illégaux, mat, pat, promotion, matériel insuffisant, règle des 50 coups. Le module `chess-engine.js` avec un faux worker : ordre UCI, file des recherches, `stop`, échéances, erreur du worker, `terminate`. Annuler, position de départ, promotion dans l'historique ; PGN aller-retour, position de départ `[FEN]`, texte invalide, nom hostile, collection, plus de 1 000 demi-coups. 26 tests. |
| `tests/e2e` | L'app dans l'hôte, dans Chromium : coups au clic, au glisser et au clavier ; mat du berger (plateau verrouillé) ; promotion (annulée au bouton, puis choisie au clavier avec le focus sur le sélecteur) ; écran de téléphone (Pixel 7) et toucher ; contre Stockfish avec les Noirs (il ouvre, on répond, il rejoue) ; Échap ferme le dialogue et rend le focus ; une nouvelle partie pendant que Stockfish réfléchit (son coup n'est pas joué) ; annuler et retourner à deux, annuler contre l'ordinateur, pendule 5+0 ; enregistrer une partie en `.pgn` et la rouvrir. 11 parcours. `CHESS_BASE_URL` réutilise un hôte déjà lancé. |

La CI (`.github/workflows/ci.yml`) fait tout cela à chaque push et à chaque PR.

## Décisions prises pendant le chantier

- **Id et éditeur** : `ca.kazo.chess` et `pub.kazo` (confirmés par Benoit le
  2026-10-31).
- **Jeu de pièces** : « celtic » (MIT). Parmi les jeux libres repris par
  lichess, seuls « celtic », « spatial », « fantasy » (MIT) et « rhosgfx »
  (CC0) respectent la décision Q15 ; « celtic » est le plus lisible et va avec
  l'esthétique gothique.
- **Nulles par triple répétition et règle des 50 coups** : la partie s'arrête
  d'elle-même, comme chess.js le signale (`isDraw()`) et comme sur chess.com.
  Aux règles de la FIDE ces nulles se réclament ; Benoit a choisi l'arrêt
  automatique le 2026-10-31.
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

Chantier 2 (`chess/2-engine`) :

- [x] Stockfish 19 « lite, single-threaded » dans un Web Worker, chargé à la demande.
- [x] 5 niveaux (`Skill Level` 0, 5, 10, 15, 20), choix de la couleur (dont au hasard).
- [x] Dialogue « Nouvelle partie » dans la fenêtre, plateau tourné vers le joueur.
- [x] `GameSession` : réponses de l'ordinateur, annulation à la nouvelle partie.
- [x] Tests C# (faux moteur et faux worker) et de bout en bout contre Stockfish.
- [x] Relecture indépendante : numéro de partie pris avant la porte, échéances du
  worker, départ raté sans effet sur la partie en cours, focus et clavier du
  dialogue (Échap, Tab, retour au bouton).

Chantier 3 (`chess/3-history-and-clock`) :

- [x] Feuille de partie, annuler le coup, retourner le plateau.
- [x] Pendule : sans, 5+0, 10+0, 15+10, personnalisée ; temps écoulé.
- [x] Stockfish reçoit le départ et tous les coups.
- [x] Tests C# (pendule sur un faux `TimeProvider`), node et de bout en bout.

Chantier 4 (`chess/4-pgn-files`) :

- [x] Enregistrer en `.pgn` (balises, résultat), ouvrir un `.pgn`.
- [x] Association des `.pgn` dans le manifeste, ouverture du fichier de lancement.
- [x] Tests C#, node et de bout en bout (enregistrer puis rouvrir).

Chantier 5 (`chess/5-solution-template`) :

- [x] Sous-module `external/HackerOs` sur `v2.2.0`, `Directory.Build.props/targets`,
  `global.json`, vérification du sous-module, profils Debug (sources) et Release
  (paquets) vérifiés.
- [x] CI sur le profil Release (sous-module vérifié, C#, node, E2E) ; workflow
  `publish-to-hack-store.yml`.
- [x] Dossier `store/` : icône 256×256, descriptions courtes et longues (fr, en),
  captures, `store.json` (catégorie `entertainment`, licence GPL-3.0).
- [x] Version `1.0.0` dans le manifeste, catégorie `entertainment`.
- [ ] Ressources de l'app installée par le Store : attend la correction de HackerOS
  (voir « Publication » plus bas), puis le sous-module passe sur la nouvelle version.
- [ ] Tag `v1.0.0` (créé par Benoit après la fusion dans `main`) et pull request
  vers le catalogue (issue #3).
