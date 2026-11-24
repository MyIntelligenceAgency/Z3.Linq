# Z3.Linq — divergence status (CoursIA fork vs endjin upstream)

> Photographie au 2026-11-24, pour jsboige/CoursIA#19237. La ligne de fusion part de
> `a0477d9c5944` et fusionne `endjin/main` `964f17704e38` ; les historiques restent
> intacts (pas de rebase du fork).

## Résumé

- **Fork** : `MyIntelligenceAgency/Z3.Linq`, base de cette branche `a0477d9c5944`.
- **Upstream fusionné** : `endjin/Z3.Linq` `964f17704e38` (endjin#118).
- **Derrière amont après fusion** : 0 commit ; les apports du fork restent des commits propres.
- **Pile amont** : les PRs #44 à #110, puis #115 à #118 sont **MERGED** le 2026-10-28.
  Les tableaux qui les présentaient comme ouvertes sont donc historiques.
- **PRs amont ouvertes à cette date** : #120, #121, #122, #123, #124, #126, #127, #128
  (8). #125 n'est pas une PR.
- **Vérification de la fusion** : build Release net8.0/net10.0 sans avertissement ni erreur,
  suite MSTest/MTP 471/471 ; contrôle amont pur 368/368. Les 19 carnets consommateurs sont
  exécutés séparément avant la PR du fork, contre les artefacts `.deploy` reconstruits.

## Apports absorbés par amont

La resynchronisation adopte les formes amont qui couvrent le même comportement. Cette table
remplace le statut « candidate » des anciennes lignes ; elle ne prétend pas que chaque implémentation
est devenue byte-identique.

| Commit fork historique | Sujet | Contrepartie arrivée sur `endjin/main` | État |
|---|---|---|---|
| `6eab957` | DateTime UTC et ticks | #84, #95, #118 | absorbé |
| `a89cfc6` | bornes des scalaires | #98, #102 | absorbé |
| `5ad0c7b` | relecture Int16 | #84, #88 | absorbé |
| `689c714`, `420118b`, `62c277f` | collections, tailles, mode Constants | #90, #93, avec apports fork conservés | absorbé + extensions locales |
| `689cf3f` | bit-vectors de largeur fixe | #99, #101, avec attributs fork conservés | absorbé + extensions locales |
| `7b04ced` | satisfaisabilité / explication | #86 | absorbé |
| `9ea81dc` | lecture par témoin du modèle | endjin#127 reste OPEN ; surcharge fork conservée | suivi amont ouvert |
| `2d213b8` | conditionnel ternaire | #99 | absorbé |
| `c4fd415` | lecture des chaînes capturées | #118 | absorbé, adaptation ci-dessous |
| `2ea8fa0` | environnements par constructeur | endjin#127 reste OPEN ; branche fork conservée | suivi amont ouvert |
| `8bd75c0` | contraintes souples pondérées | endjin#128 reste OPEN | suivi amont ouvert |
| `096a638` | collections imbriquées | endjin#123 reste OPEN | suivi amont ouvert |

Les autres extensions du fork (logique et tactiques configurables, Pseudo-Boolean, `Sum`,
`Rational`, benchmarks et outillage de déploiement) ne sont pas supprimées par cette fusion.
Leur équivalence fonctionnelle ne se déduit pas d'un titre de PR amont.

## Résolutions de fusion qui changent le contrat

- **Chaîne capturée nulle.** Le fork plie chaque contrainte avant traduction. MiaPlaza
  ExpressionUtils 1.3.1 ne lève pas toujours une évaluation hôte en échec : il insère un appel
  `ExceptionClosure.Invoke`, qui ne porte plus la chaîne écrite. La fusion détecte ce marqueur
  public et traduit alors la contrainte d'origine : le visiteur amont nomme le lien nul comme le
  prévoit endjin#118, au lieu de produire un message d'interpréteur sans source.
- **Records positionnels.** Contrairement au refus épinglé sur la pile du 2026-10-25, la branche
  conserve `ConstructFromModel` : un type sans constructeur sans paramètre est construit par
  correspondance des paramètres nommés. Le test amont a été retourné vers cette capacité et
  `RecordEnvTheoryTests` reste vert. C'est un consommateur réel des carnets CoursIA, pas une
  ligne à perdre dans la fusion ; endjin#127 porte le sujet amont.
- **Template paresseux.** La lecture amont du modèle consulte le template sans l'aplatir au
  préalable, pour ne pas remplacer une collection explicitement vide. Les trois appelants du
  fork passent l'instance template ; la surcharge de réflexion `Solve(CancellationToken)` est
  sélectionnée par signature, car la surcharge de témoin du fork rend le nom seul ambigu.
- **Artefacts consommés.** `.deploy/Z3.Linq.dll` est publié en net10.0 (références
  `System.Linq.Expressions/System.Runtime/System.Linq` 10.0.0.0), avec Microsoft.Z3 5.3.2.0
  et son `libz3.dll` win-x64. `ExpressionUtils.dll` 1.3.1 reste byte-identique.

## G4 — mesure des PRs fork #37, #38, #40 sur `964f177`

Le défaut se mesure sur `endjin/main`, pas sur l'ancienne pile `b425802b`.

| PR fork | Mesure sur la ligne fusionnée | Geste |
|---|---|---|
| #37 ValueTuple | amont affecte les champs publics : le défaut de construction ValueTuple est absorbé | adopter amont ; aucun report du hunk fork |
| #38 types anonymes | amont délègue la lecture au marshaller partagé : le défaut est absorbé | adopter amont ; aucun report du hunk fork |
| #40 symbole `Rational` | `TheoremVariableTypeMappingAttribute` fournit la voie générique ; le support exact du fork reste local | aucune nouvelle PR symbole |
| #40 constante d'un type mappé | `ExpressionVisitor.VisitConvert` utilise encore `Type.GetTypeCode(expression.Type)` sans le mapping utilisateur | défaut distinct à reproduire sur `964f177`, puis PR amont à sujet unique s'il est confirmé |
| record positionnel | le fork conserve la construction par constructeur ; endjin#127 (OPEN) porte cette capacité et le témoin de modèle | pas de nouvelle PR amont dupliquée |

Les tests utiles des trois PRs restent des actifs de couverture à porter séparément ; ils ne
justifient pas de conserver les réimplémentations supplantées. Les contrats exacts et d'overflow
de `Rational` doivent être prouvés à travers la voie générique avant toute substitution.
La PR fork #43 conserve la matrice datée du 2026-10-25 : elle n'est ni fusionnée dans cette
branche ni écrasée ici.

## PRs amont ouvertes au 2026-11-24

| PR | Sujet | Base |
|---|---|---|
| #120 | exemples SAT/SMT by Example | `main` |
| #121 | test `Distinct` inline | `test/theorem-smoke-gaps` |
| #122 | capture d'une variable entre soumissions scripting | `main` |
| #123 | collections imbriquées comme symboles | `test/cross-submission-capture` |
| #124 | exemple Sudoku sur `int[][]` | `feature/nested-collection-symbols` |
| #126 | banc de formes de collection/capture du fork | `feature/nested-collection-symbols` |
| #127 | témoin de modèle et environnements par constructeur | `main` |
| #128 | `SoftWhere`, MaxSAT pondéré | `main` |

## Anti-divergence (HARD)

Aucun commit nouveau sur fichiers d'overlap (`Theorem.cs`, `ExpressionVisitor.cs`,
`TheoremSolving.cs`) sans mesure sur `endjin/main` et PR amont correspondante lorsque le défaut
y vit encore. Besoins propres CoursIA = adoption/cherry-pick de la ligne amont quand elle couvre
le comportement ; jamais de réimplémentation silencieuse d'un mécanisme amont.

## Historique et documents de mesure

- État initial au 2026-09-26, erratum au 2026-10-04 : [`OVERLAP_G1.md`](OVERLAP_G1.md).
  Cet erratum réfutait notamment les équivalences de titre #74/Pseudo-Boolean et #96/MaxSAT,
  et corrigeait la provenance DateTime/Int16. Les anciens comptes ahead/PRs ouvertes sont périmés.
- P0 : la cartographie et le reframe de endjin#43 ont été publiés en septembre 2026.
  endjin#43 est **CLOSED**, non mergée ; endjin#118 couvre la lecture des constantes capturées.
- P1 : #111, #112, #113 et #114 sont **CLOSED**, non mergées. Les suites de la pile amont
  utilisent MSTest/MTP + Shouldly ; les assertions ci-dessus ne se lisent pas comme des merges.
- P2 : intégration historique `integration/endjin-stack-20260921` (merge `3f799a5`, banc
  `a4e44e2`), rapportée sur endjin#110 le 2026-09-21. La pile est depuis arrivée sur `main`.
- P3 : la présente branche fusionne au lieu de réécrire les apports uniques ; la suppression des
  doublons reste conditionnée à une preuve comportementale, jamais à un rapprochement de titres.

## Non-goals

- Merger ou fermer les PRs d'Howard : la lane mesure et propose, elle ne juge pas à sa place.
- Réécrire en bloc les commits du fork : une PR par sujet.
- Déclarer les carnets validés sans exécution locale et sorties réelles.

## Liens

- EPIC parent : #14169 ; issue de resynchronisation : jsboige/CoursIA#19237.
- G1 #16050 · G1-bis #16053 (PR #16070) · Epic #1206 · issue historique #16609.
- Amont : #43, #110, #114, #118 ; suivi ouvert : #120 à #124, #126 à #128.
- Matrice G4 datée : PR fork #43 (`docs/g4-divergence-20261025`).
