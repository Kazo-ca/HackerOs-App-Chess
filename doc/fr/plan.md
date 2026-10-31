> [English version](../en/plan.md)

# Plan : l'app Chess pour HackerOS

Plan **validé le 2026-10-29** par Benoit (fil « Planifier le Hack Store et
l'app Chess »). Ce document fixe la cible et l'ordre des choses à faire ;
l'avancement est en fin de page et le code est décrit dans
[`architecture.md`](architecture.md).

## But

Chess est la **première app du Hack Store** : une app tierce, dans son propre
dépôt, qui teste toute la chaîne de publication de HackerOS (dépôt de l'app →
PR au catalogue `Kazo-ca/HackerOs-AppMarketPlace` → vérification et
compilation par la CI du catalogue → installation par le Hack Store).
Le plan du Store est dans le dépôt HackerOs :
`doc/fr/wasm/dev/plans/hackeros-store.md` et les ADR 0188 à 0190.

## Décisions

| # | Sujet | Décision |
| --- | --- | --- |
| Q12 | Technologie | Garder **chess.js** (règles, licence BSD-2-Clause), servi comme ressource `wwwroot/` de l'app. Prérequis : le chantier HackerOS qui sert les ressources des apps installées. |
| Q13 | Départ du dépôt | Généré avec le modèle `hackeros-solution` (ADR 0176 de HackerOS) une fois ce modèle codé. |
| Q14 | Modes de jeu | Deux joueurs sur le même écran, et contre l'ordinateur. Moteur : **[stockfish.js](https://github.com/nmrugg/stockfish.js)**. Stockfish a déjà un réglage de force, donc pas de moteur maison. |
| Q30 | Licence | Le dépôt passe en **GPL-3.0** (obligatoire pour embarquer stockfish.js). |
| Q31 | Version du moteur | Version « lite » mono-thread (environ 7 Mo, pas d'isolation cross-origin), dans un Web Worker. **5 niveaux** nommés (Débutant, Facile, Moyen, Difficile, Expert) mappés sur `Skill Level` (0, 5, 10, 15, 20). |
| Q15 | Plateau | Dessiné par nous (Razor et SVG), couleurs prises dans les jetons du thème de l'OS ; pièces d'un jeu libre CC0 ou MIT (choisi au chantier, licence notée dans le dépôt). |
| Q16 | Fonctions | Annuler, historique des coups, retourner le plateau, enregistrer et ouvrir une partie `.pgn` (Chess associé aux `.pgn` dans « Ouvrir avec »), **pendule**. |
| Q32 | Pendule | Sans pendule, 5+0, 10+0, 15+10, ou personnalisée. |
| Q17 | Mobile | Bureau et interface mobile : toucher la pièce puis la case, ou glisser. |

## Choses à faire, dans l'ordre

1. Attendre les prérequis HackerOS : modèle `hackeros-solution`, service des
   ressources `wwwroot/` des apps installées, catalogue et Hack Store
   (plan du Store, chantiers 1 à 5).
2. Générer la solution avec `dotnet new hackeros-solution` (sous-module
   `external/HackerOs` sur le tag de la version visée).
3. Plateau SVG, saisie des coups (souris, toucher, clavier), règles par
   chess.js via JS interop.
4. Partie contre l'ordinateur : stockfish.js lite dans un Web Worker, 5
   niveaux, choix de la couleur.
5. Historique, annuler, retourner le plateau, pendule.
6. Fichiers `.pgn` : enregistrer, ouvrir, association de fichiers dans le
   manifeste.
7. Tests (règles, PGN, pendule) et vérification sur mobile.
8. Dossier `store/` (icône 256×256, descriptions fr et en, captures,
   catégorie et étiquettes) et premier tag `v1.0.0` : le workflow du modèle
   ouvre la PR vers le catalogue.

## Licences

- Ce dépôt : GPL-3.0.
- stockfish.js : GPL-3.0. chess.js : BSD-2-Clause. Pièces : CC0 ou MIT.

## Avancement

Le 2026-10-30, Benoit a choisi de commencer avant les prérequis HackerOS : la
solution part des paquets NuGet 2.1.0 (`hackeros-window` et `hackeros-host`)
et passera au modèle `hackeros-solution` quand il existera. Le travail se fait
dans la série `chess/base` (une branche et une PR par chantier).

| Chantier | Branche | État |
| --- | --- | --- |
| 1. Plateau SVG, saisie (souris, toucher, clavier), règles par chess.js, deux joueurs | `chess/1-board-and-rules` | Fait (pièces « celtic », MIT) |
| 2. Contre l'ordinateur : stockfish.js lite, 5 niveaux, choix de la couleur | `chess/2-engine` | Fait (Stockfish 19 lite, 1,8 Mo) |
| 3. Historique, annuler, retourner le plateau, pendule | | À faire |
| 4. Fichiers `.pgn`, association de fichiers | | À faire |
| 5. Passage au modèle `hackeros-solution`, dossier `store/`, tag `v1.0.0` | | Attend Kazo-ca/HackerOs#246 à #249 |
