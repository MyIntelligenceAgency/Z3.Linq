# OVERLAP_G1 — Verdict de recouvrement des 28 PRs amont endjin/Z3.Linq vs fork

**Grain G1 de l'EPIC #14169** (CoursIA) — issue #16050. Livré par la lane `myia-po-2025:CoursIA`, mesuré le 2026-10-04.

## Bases de la mesure

| Base | Valeur mesurée |
|---|---|
| Fork `MyIntelligenceAgency/Z3.Linq`, branche `main` | `24ed366` — **54 ahead / 0 behind** `endjin/main` (`f937287ca`, poussé 2026-09-02) |
| PRs amont ouvertes ce jour | **45** — dont **28 dans le périmètre G1** (la liste d'acceptance du 2026-09-01, toutes toujours ouvertes), 16 hors périmètre (§7), 1 nôtre (#43) |
| Banque d'intégration P2 | branche `integration/endjin-stack-20260921` (`a4e44e2`, merge `3f799a5`), rapport sur endjin#110 c.2026-09-21T12:39Z |

Méthode : pour chaque PR amont, le **diff réel** (pas le titre) est confronté au code du fork par lecture directe — grep ciblés, `git log -L`, `git show` sur les commits fork cités. Six blocs de verdicts établis en parallèle (build, runtime, tests, marshalling ×2, sémantique), chacun avec citations `fichier:ligne`. Les claims de `DIVERGENCE_STATUS.md` ont été re-vérifiés au passage (§6).

## Définitions des verdicts

- **REDONDANT** — la capacité existe dans le fork sous une forme sémantiquement équivalente (implémentation indépendante ou port explicite).
- **DIVERGENT** — la capacité existe mais avec des sémantiques différentes (souvent : le fork a *corrigé* ce qu'amont épingle comme cassé, ou répond par un autre mécanisme).
- **NOUVEAU-POUR-NOUS** — le fork ne possède pas la capacité ; le cas échéant le défaut amont est vivant côté fork.
- **EXCLU** — la PR ne livre pas de capacité (clause d'exclusion du corps de l'EPIC : tests amont seuls, doc seule).

## Tableau des 28 verdicts

| PR amont | Sujet | Verdict | Preuve fork principale |
|---|---|---|---|
| #44 | ZeroFailed (drop scaffold Endjin) | **NOUVEAU** | fork tourne sur le scaffold (`build.ps1:5,74,77,106-118`), `.zf/` 0 fichier, migration connue et différée (`build.yml:65-67`) |
| #45 | net10 + CPM + .slnx | **NOUVEAU** | TFM net8.0 par projet (csproj:4 ×4 + pb-bench), CPM 0 fichier, .sln classique 4 projets, CI `build.yml:57` 8.x |
| #47 | MiaPlaza ExpressionExtensions 1.3.1 | **NOUVEAU** | pin 1.2.0 (`Z3.Linq.csproj:22`) ; 10 sites `PartialEval` à adapter là où amont en patche 1 (`ExpressionVisitor.cs:335,387,511,654,657,817` + `Theorem.cs:200,271,299,458`) |
| #48 | tests Distinct/partial-eval | **NOUVEAU** | inline couvert incidemment (`CollectionHandlingTests.cs:55-56`) ; forme selector en contrainte, accord, UNSAT dédié : 0 hit |
| #59 | tests core/composition | **NOUVEAU** | SAT/UNSAT incidemment ; composition/diagnostic entier absent (NotSame, ToString, Log, Dispose : 0 hit) |
| #61 | runtime Z3 4.12.2→5.1.0 + RIDs | **NOUVEAU** | pin 4.12.2 (`Z3.Linq.csproj:23`), x64 4 csproj, linux/arm64 0 hit, mitigation `runsOn: windows-latest` (`build.yml:64`) — écart **connu et différé volontairement** (`build.yml:58-63` cite le fix amont) |
| #65 | tests marshalling types | **DIVERGENT** | mêmes scénarios, **attentes inversées** : short/DateTime/bool[]/culture **fonctionnent** fork (`Int16RoundTripTests.cs:29-48`, `DateTimeRoundTripTests.cs:44-63`, `CollectionHandlingTests.cs:179-193`) vs pins amont « broken » ; NOUVEAU partiel : float/decimal/string, envs anonymes/tuples/imbriqués 0 hit |
| #67 | tests Optimize/OrderBy | **NOUVEAU** | 0 test de l'API d'optimisation (l'API existe : `Theorem{T}.cs:86,142,150`) ; le défaut #58 est **présent** fork (`Theorem.cs:241-244` `default!`) |
| #69 | tests failure modes + rewriters | **NOUVEAU** | rewriters : API présente (`Theorem.cs:399-418`), 0 test ; ternaire **divergent** (fork supporte MkITE, `ConditionalIteTests.cs:25-90`) |
| #71 | tests acceptance Sudoku 9x9 / river crossing | **NOUVEAU** | 0 test ; types/membres identiques côté Examples (`SudokuTable.cs:5-8+`, `MissionariesAndCannibals.cs:18,96`) ; manque la ProjectReference Examples |
| #73 | model.Eval completion:true | **NOUVEAU** | fork évalue **sans** completion aux 5 sites (`Theorem.cs:740,925,933,1046,1117`) ; seul le witness fork l'a (`:202`, commit 9ea81dc) — défaut masqué incidemment par AssertBounds pour les bornés |
| #74 | drop dodge constraints (tests) | **EXCLU** | zéro code bibliothèque : 32 contraintes d'esquive retirées de 6 fichiers de **tests amont** ; aucun de ces fichiers n'existe fork ; le rôle « garder les symboles référés » est tenu par AssertBounds (`Theorem.cs:335-358`) |
| #77 | constantes real invariantes | **REDONDANT** | déjà corrigé fork `a54ba1b` (`ExpressionVisitor.cs:860-866`, InvariantCulture) ; résiduel fork-spécifique : Rational négatif sous cultures U+2212 (`Rational.cs:68` via `:847-850`) |
| #79 | lecture valeur champ collection | **REDONDANT** | le défaut n'a **jamais existé** fork (`Theorem.cs:720-725` lit `GetValue` d'emblée) ; divergence : collection null → longueur 0 (`:882`) là où amont jette |
| #80 | read-back float en float | **NOUVEAU** | défaut **présent** : `ParseRatNumAsDouble` (`Theorem.cs:1014-1024`) alimente les deux bras Single (`:767-768`, `:1042-1043`) — un float résolu puis rejeté au SetValue réflexif |
| #81 | lecture élément decimal sélectionné | **REDONDANT** | le défaut n'existe pas fork (`Theorem.cs:1044-1054` évalue `numValExpr`, l'élément) |
| #84 | read DateTime en UTC | **REDONDANT** | sémantique livrée par l'encodage ticks du port #95 (`ToDateTime` `Theorem.cs:833-842`, `Kind=Utc`) ; le mécanisme file-time n'a jamais existé fork (grep FileTime → commentaires seuls) |
| #86 | satisfiabilité séparée (TrySolve/OrNull, nullabilité honnête) | **DIVERGENT** | fork répond par un mécanisme plus riche en diagnostic — `Explain()` tri-état (`Explanation.cs:11-21`) + UNSAT core (`Theorem.cs:450-488`) — mais l'ambiguïté value-type de `Solve()` **reste** (`:148,156,159-162`) et `Optimize` rend `default!` (`:241-244`) |
| #88 | short + enum | **REDONDANT** | les deux moitiés présentes : traduction `ExpressionVisitor.cs:259-270` (a54ba1b, **antérieur** à la PR amont) + bras Int16 propre `Theorem.cs:747-749,851-860` (5ad0c7b) ; tests verts `Int16RoundTripTests.cs` ; gap : 0 test enum |
| #90 | collections = mêmes sorts que scalaires | **REDONDANT** | sémantique présente **depuis le commit initial** (`:663-692` domaine Int + range = sorts scalaires) ; nuance : 3 copies du mapping à maintenir manuellement |
| #91 | envs anonymes = marshaller complet | **NOUVEAU** | défaut **vivant** : `Theorem.cs:1087-1133` — Eval avant check de type + marshaller inline bool/int seulement ; anonymous+double/string/DateTime → NotSupportedException, +objet imbriqué → NRE ; 0 test |
| #92 | conversions par sort, pas par type cible | **REDONDANT** | dispatch sur le **sort Z3 réel de l'operand** (`ExpressionVisitor.cs:221-283`, a54ba1b) — les 16 formes amont passent ; écart : `(char)`-from-real jette (`:274-279`) |
| #93 | tailler collections depuis l'instance NewTheorem | **NOUVEAU** | mécanisme **inexistant** : `Z3Context.cs:139-142` ignore toujours `dummy` ; collection sans longueur → **vide silencieuse** (`Theorem.cs:880-886`) |
| #94 | docs XML générées (CS1591) | **NOUVEAU** | `GenerateDocumentationFile`/CS1591 : 0 hit tous csproj ; 134 `/// <summary>` sur 15 fichiers jamais validées compilateur ; le défaut spécifique (doc détachée) absent fork (`Theorem.cs:1067-1073` correctement attaché) |
| #95 | DateTime en ticks | **REDONDANT** | **port explicite** — commentaire « Port of endjin/Z3.Linq#95 » (`ExpressionVisitor.cs:1042-1044`, commit 6eab957) ; en plus : bounds #98 rendent le hors-plage UNSAT |
| #96 | solve borné (timeout/rlimit/token) + UNKNOWN | **NOUVEAU** | grep CancellationToken/Timeout/rlimit/Interrupt → 1 hit (UNKNOWN dans Explain() seul, `Theorem.cs:476`) ; Solve() colle UNKNOWN et UNSAT (`:156→159-162`) ; échappatoire `SetParameter` (`Z3Context.cs:86-95`) sans token ni exception |
| #98 | bornes entières des symboles | **REDONDANT** | **port explicite** — « Port of endjin/Z3.Linq#98 » (`Theorem.cs:287-289,310`) ; mêmes 4 types bornés (`:381-389`), mêmes rationale quantificateur/complétude, exclus bit-vectors (`:331-333`) |
| #99 | ternaires + bitwise + modulo réel | **DIVERGENT** | ternaire **présent** (MkITE, `ExpressionVisitor.cs:107-108,212-219`, 2d213b8) ; bitwise sort-aware **absent** (`:29-38` cast BoolExpr inconditionnel) ; modulo réel **absent** (`:62-63` MkRem inconditionnel) |

**Comptes par colonne** : **NOUVEAU-POUR-NOUS = 15** (#44, #45, #47, #48, #59, #61, #67, #69, #71, #73, #80, #91, #93, #96, + #94 ci-dessous) · **DIVERGENT = 3** (#65, #86, #99) · **REDONDANT = 9** (#77, #79, #81, #84, #88, #90, #92, #95, #98) · **EXCLU = 1** (#74) — total 28.

*Note de classement* : #94 (docs XML générées) est compté NOUVEAU ci-dessus — preuve : `GenerateDocumentationFile`/CS1591 = 0 hit tous csproj ; le défaut spécifique #94 (doc détachée) est **absent** fork (`Theorem.cs:1067-1073` correctement attaché) mais la capacité (validation compilateur des 134 `/// <summary>` existantes) manque.

Le verdict DIVERGENT de #86 (satisfiabilité séparée) : le fork répond par un **autre mécanisme, plus riche sur l'axe diagnostic** — `Explain()` tri-état (`Explanation.cs:11-21`) + UNSAT core par tracking literals (`Theorem.cs:450-488`) — mais l'ambiguïté value-type de `Solve()` **reste** (`:148,156,159-162`), `Optimize` rend `default!` (`:241-244`, le défaut exact de #86), et `DeferredSolvable` n'a que `Solve()` (`Theorem{T}.cs:153-163`). Équivalence claim « Explain/UNSAT-core » : partiellement confirmée.

## Notes de faisabilité (par NOUVEAU-POUR-NOUS)

Contexte commun, mesuré à la banque d'intégration P2 : le stack amont **ne se rebase pas** sur le fork (11 fichiers / 48 hunks en conflit — Theorem.cs 20, ExpressionVisitor.cs 15 ; banc 43 tests = 12 pass / 31 fail, échec dominant = extensions fork-only heurtant `VisitCall` unknown-method). **Tout port est donc sémantique, jamais mécanique.** Les fichiers overlap (`Theorem.cs`, `ExpressionVisitor.cs`, `TheoremSolving.cs`) relèvent du régime anti-divergence : un port citant la PR amont ouverte est légitime.

| PR | Coût | Notes de port |
|---|---|---|
| #80 | **trivial** | un `ParseRatNumAsSingle` (même strip du `?` terminal, `float.Parse` InvariantCulture) aux 2 bras Single |
| #91 | faible-modéré, 1 fichier | remplacer `Theorem.cs:1114-1130` par un appel à `ConvertZ3Expression` (garde null-Expr `:736-738` déjà correcte) ; conserver la boucle propriété→backing field `:1099-1111` ; répliquer le refus nommé des collections |
| #73 | simple | `completion:true` aux 5 sites (ou helper fork) ; le pattern existe déjà au witness `:202` |
| #99 (parties manquantes) | faible friction | `:29-38` et `:62-63` quasi byte-identiques au pré-change amont, la réécriture sort-aware se pose quasi verbatim ; arbitrer l'interaction avec `BitVecWidth` maison |
| #48/#59/#67/#69/#71 | port de tests, coût uniforme MSTest→xUnit | quasi rebasables (#67, #71 notamment) ; **attention** : les pins amont de défauts corrigés fork (#51, #52-partiel, #56, #63, #64-partiel) doivent être retournés en gardes positives ; #71 exige la ProjectReference Examples |
| #96 | modéré | exception + tests additifs ; le threading du token dans `Theorem.cs` divergé (witness `:134`, soft-optimizer `:141-157`, CreateSolver `:154`) est le chantier ; arbitrage : garder `Explain()` non-jetant, faire jeter `Solve()`/`Optimize()` |
| #93 | modéré, 4 fichiers | template en 4ᵉ paramètre de la chaîne ctor (`Theorem.cs:52-78`) ; **préserver** l'extension max-index Constants (`:890-895`) ; interdépendance : #91 d'abord si anonymous+collection |
| #61 | port requis, différé volontairement | 5/8 fichiers amont = ajouts purs ; le bump 4.12.2→5.1.0 est **faible risque mais non prouvé** pour la surface fork (51+ commits d'API au-delà de la surface validée amont) ; organe de décision : feed local + `dotnet test` ; **wrinkle** : les DLL `.deploy/` committées (décision #11431) consommées par le polyglot-repro resteraient désynchronisées |
| #44 | lourd | le patch amont réécrit `build.ps1` entier et effacerait les knobs fork (`$SkipTest=false` `build.yml`/`build.ps1:129-131` preuve #14445, `$SkipAnalysis=true` `:133-136`) ; `.zf/config.ps1` amont pose `$SkipTest=true` = **désactiverait les tests en silence** |
| #45 | mécanique + **arbitrage réel** | le `.slnx` amont **retire Tests de la solution** → régénérer localement, jamais copier ; net10-only casserait les consommateurs CoursIA (DLL consommée à net8.0, .NET Interactive épinglé) — c'est l'arbitrage, pas le geste |
| #47 | adaptation large | 10 sites `PartialEval` à adapter (cherry-pick = uncompilable) ; la suite xUnit fork vérifie la parité comportementale |
| #94 | dépend de #45 | pas de `Directory.Build.props` sinon ; NoWarn CS1591 à couvrir sur 5 projets dont pb-bench ; l'activation découvrira les CS1591 fork à documenter |

**Ordre de sucrage recommandé** (valeur/défaut vivant d'abord) : #80 → #91 → #73 → #99-parties → tests #67/#71 → #93 → #96 → #61 (avec organe empirique) → stack build #44→#45→#94 → #47.

## Corrections apportées à DIVERGENCE_STATUS.md (claims re-mesurés)

| Claim antérieur | Mesure G1 | Statut |
|---|---|---|
| #74 « équivalent à notre PB » (3 mentions) | le diff #74 ne contient **aucune** ligne Pseudo-Boolean — tests amont seuls ; le PB fork (`Z3Methods.cs:83-180`) est **fork-original sans candidate amont** | **REFUTÉ** |
| #96 « équivalent MaxSAT soft » | collision de titres seulement — le soft/MaxSAT fork (contraintes pondérées sacrifiables) n'est pas du bornage | **REFUTÉ** |
| #88 « pas dans le fork » | les deux moitiés présentes et testées (a54ba1b + 5ad0c7b) | **REFUTÉ** |
| #92 « pas dans le fork » | capacité présente (dispatch par sort réel, a54ba1b) — vrai seulement du refactor single-mapping | **REFUTÉ sur la capacité** |
| #91 « équivalent record envs » (2ea8fa0) | records ≠ anonymous — le défaut anonymous est vivant | **REFUTÉ comme équivalence** |
| #93 « équivalent default-fill » (420118b) | default-fill Constants ≠ template de taillage — `dummy` toujours ignoré | **REFUTÉ comme équivalence** |
| #84 (row 4 : 5ad0c7b) | 5ad0c7b est la contrepartie **#88** ; la capacité #84 vient de 6eab957 | **mal étiqueté** (capacité confirmée) |
| #90 « équivalent partiel » (689c714+096a638) | le point sorts est **entièrement** présent depuis le commit initial ; les 2 commits cités sont d'autres capacités (au-delà d'#90) | juste pour la mauvaise raison |
| #95 « équivalent Utc ticks » (6eab957) | port explicite confirmé aux deux chemins + garde | **CONFIRMÉ** |
| #98 « port » | port explicite confirmé (commentaire au point d'appel) | **CONFIRMÉ** |
| #86 « équivalent Explain/UNSAT-core » | plus riche sur le diagnostic, mais l'ambiguïté de `Solve()` reste et `Optimize` porte le défaut exact | **PARTIELLEMENT confirmé** |

Conséquence structurante : le PB fork (`ExactlyOne`/`AtMostOne`/`AtLeastOne`/`Weighted*` → `MkPBGe/Le/Eq`, `Z3Methods.cs:83-180`) n'a **aucune contrepartie amont dans le périmètre G1** — à la re-mesure du « 51 ahead », il reste un apport sans équivalent côté endjin.

## Les 16 PRs amont ouvertes hors périmètre G1 (arbitrage, pas mesure)

L'acceptance G1 du 2026-09-01 listait 28 PRs ; le plateau amont compte 45 ouvertes ce jour. Tri des 16 nouvelles, à l'attention du coordinateur (aucune mesurée en profondeur ici) :

- **#111, #112, #113 = NÔTRES** (jsboige) — #113 = fix DateTime static fields, branche `fix/datetime-static-fields-95` (3eddb36), suit le finding 1 de la banque d'intégration.
- **#40, #42 = bots** (Renovabot/dependabot).
- **#100–#110 = continuation du train de Howard** (même fil que #44–#99 ; la tête du stack est `feature/spectre-demos`, 61 commits). Ces PRs sont dans la continuité directe du périmètre mesuré — le verdict G1 sur leurs prédécesseurs (impossibilité de rebase, ports sémantiques) s'y projette, mais leur contenu propre devra être mesuré à la demande (candidat naturel : G2).

## Liens morts et bornes de la mesure

- Les références CoursIA `#16053` et `#16070` citées par `DIVERGENCE_STATUS.md` sont **inrésolvables** (issue/PR introuvables dans CoursIA, le fork et la recherche GitHub au 2026-10-04) — à renuméroter ou retirer à la prochaine retouche du document.
- Mesure bornée au code : les verdicts portent sur les diffs amont au SHA head relevé le 2026-10-04 et l'arbre fork `24ed366` ; aucun test n'a été exécuté pour ce doc (les exécutions citées sont celles des suites existantes, rapportées par les mesures de preuve).

## Liens

- EPIC CoursIA #14169 · grain G1 #16050 (claim : paths `Z3.Linq/OVERLAP_G1.md` + submodule)
- `DIVERGENCE_STATUS.md` (cartographie 54-ahead, précisée par le §6 ci-dessus)
- Banque d'intégration : branche `integration/endjin-stack-20260921`, rapport endjin#110 c.2026-09-21T12:39Z
- Amont : <https://github.com/endjin/Z3.Linq> (main `f937287ca`)
