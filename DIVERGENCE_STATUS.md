# Z3.Linq — divergence status (CoursIA fork vs endjin upstream)

> État au 2026-09-26, dans le cadre de l'EPIC #16609 (réintégration avant divergence consommée, plan 4 phases).

## Résumé

- **Fork** : `MyIntelligenceAgency/Z3.Linq` (`origin/main` = `fddd586`).
- **Upstream** : `endjin/Z3.Linq` (`upstream/main` = `f937287`, PR #37).
- **Ahead** : 51 commits (forkahead, 0 behind).
- **PRs endjin ouvertes** : 28 (#74 → #112), aucune mergée depuis 2024-04-02.

## Cartographie ahead → PRs endjin candidates

| # | Commit fork | Résumé | PR endjin candidate | État endjin |
|---|---|---|---|---|
| 1 | `fc44dfa` | partial-eval cross-submission capture | endjin #43 | OPEN (notre PR) |
| 2 | `6eab957` | DateTime Utc ticks round-trip | endjin #95 | OPEN |
| 3 | `a89cfc6` | bound scalar symbols | endjin #98 | OPEN |
| 4 | `5ad0c7b` | Int16 read-back | endjin #84 | OPEN |
| 5 | `689c714` | List<T>/IList<T> indexers | endjin #90 | OPEN |
| 6 | `420118b` | unconstrained indices default | endjin #93 | OPEN |
| 7 | `62c277f` | CollectionHandling Constants mode | endjin #91 | OPEN |
| 8 | `096a638` | nested int[][] array support | endjin #90 | OPEN (collections) |
| 9 | `689cf3f` | fixed-width bit-vector theory | endjin #101 | OPEN (uint/ulong) |
| 10 | `8bd75c0` | weighted MaxSAT soft constraints | endjin #96 | OPEN (bounded solve) |
| 11 | `7b04ced` | Explain() + UNSAT-core | endjin #86 | OPEN |
| 12 | `9ea81dc` | witness evaluation | endjin #108 | OPEN (Solver/Optimize dispatch) |
| 13 | `6c67ce0` | Sum variadic | endjin #99 | OPEN (ternaries) |
| 14 | `2d213b8` | Conditional ternary → MkITE | endjin #99 | OPEN |
| 15 | `a54ba1b` | exact rational constants | endjin #99 | OPEN |
| 16 | `2ea8fa0` | record environments via ctor | endjin #91 | OPEN (anonymous envs) |
| 17 | `a593323` | configurable solver kind/logic/params | endjin #106 | OPEN (C# 14 idioms) |
| 18 | `e057090` | Z3Context.Tactics pipeline | endjin #105 | OPEN (allocation cuts) |
| 19 | `3ca4627` | weighted Pseudo-Boolean bands | endjin #74 | OPEN (drop dodge constraints) |
| 20 | `0cfc210` | unweighted Pseudo-Boolean | endjin #74 | OPEN |
| 21 | `b7ce44f` | README example + pb-bench harness | (docs) | n/a |
| 22 | `c4fd415` | thread running value VisitMember | endjin #100 | OPEN (visitor internal) |
| 23 | `b68438c` | strip probeAddresses banner | (security, local) | n/a |

## PRs endjin SANS contrepartie fork

| PR | Titre | Statut |
|---|---|---|
| #74 | Drop constraints dodging #51 | OPEN — équivalent à notre PB |
| #86 | Report satisfiability separately | OPEN — équivalent Explain/UNSAT-core |
| #88 | Make short and enum symbols work | OPEN — pas dans le fork |
| #90 | Give collections same sorts as scalars | OPEN — équivalent partiel |
| #91 | Marshal anonymous environments | OPEN — équivalent record envs |
| #92 | Numeric conversions by sort | OPEN — pas dans le fork |
| #93 | Size collections from instance | OPEN — équivalent default-fill |
| #94 | Generate XML documentation | OPEN — pas dans le fork |
| #95 | DateTime as ticks | OPEN — équivalent Utc ticks |
| #96 | Bounded solve + UNKNOWN | OPEN — équivalent MaxSAT soft |
| #98 | Bound integer symbol to type range | OPEN — équivalent bound scalar |
| #99 | Translate ternaries | OPEN — équivalent Conditional + exact rationals |
| #100 | Visitor internal instance class | OPEN — refactor du VisitMember |
| #101 | uint/ulong bit-vector | OPEN — équivalent bitvec theory |
| #102 | byte/sbyte/ushort bounded | OPEN — extension |
| #103 | Fix solve-limits defects | OPEN |
| #104 | BenchmarkDotNet suite | OPEN — pas dans le fork |
| #105 | Cut per-solve allocations | OPEN |
| #106 | Modernise solve path C# 14 | OPEN — équivalent configurable solver |
| #107 | Extract MemberClrType | OPEN |
| #108 | Extract Assert for dispatch | OPEN — équivalent witness eval |
| #109 | Merge scalar/element marshallers | OPEN |
| #110 | Convert demos Spectre.Console | OPEN — pivot participation |
| #111 | Add xUnit test project | OPEN — base PR de tests (grain P1) |
| #112 | Port DateTime round-trip suite | OPEN — équivalent de notre #26 |

## Plan d'action EPIC #16609

### P0 — débloquer le pend ✅ (ce document)

- Cartographie des 51 commits fork → PRs endjin candidates
- Aucun commentaire PR endjin posté (à faire en P1)

### P1 — PR de tests contre `endjin/main`

- endjin #111 « xUnit test project » = base
- Porter nos suites vertes (DateTime round-trip, bound scalars, etc.) sur `endjin/main`
- Chaque suite rouge sur main vanilla = candidat test pour PR amont spécifique

### P2 — branche d'intégration (participation)

- Merger le stack amont (28 PRs endjin) dans une branche d'intégration
- Lancer nos suites, publier résultats sur endjin #110
- Carte commits-fork × PRs-amont en conflit

### P3 — rebaser à la consommation

- Quand le stack amont merge : rebase des apports uniques
- Supprimer nos versions locales des doublons (4 REDONDANT G1-bis #16070)

## Anti-divergence (HARD)

Aucun commit nouveau sur fichiers d'overlap (`Theorem.cs`, `ExpressionVisitor.cs`, `TheoremSolving.cs`) sans PR amont correspondante ouverte. Besoins propres CoursIA = cherry-pick de la ligne amont, jamais réimplémentation.

## Non-goals

- Merger/close les PRs d'Howard (ses reviews idg10) — nous validons, nous ne jugeons pas.
- Réécrire nos 51 commits en bloc — découpe one-concern-per-PR.

## Liens

- EPIC parent : #14169
- G1 #16050 · G1-bis #16053 (PR #16070)
- Amont : endjin/Z3.Linq#29, #43, #110, #111
- Epic #1206
- Issue CoursIA : #16609
