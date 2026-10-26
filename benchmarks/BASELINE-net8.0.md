# Baseline .NET 8 — bancs BenchmarkDotNet Z3.Linq

Baseline de référence pour la mesure des deltas runtime .NET 9/10/11 (issue CoursIA #18770, fille de l'EPIC #18695). Banc : `benchmarks/Z3Linq.Benchmarks` (BenchmarkDotNet 0.14.0, projet intégré à `solutions/Z3.Linq.sln`), exerçant l'**API publique** du provider LINQ-to-Z3 sur le pipeline complet de théorème et sur la constante « numerics » `Rational`. **Nouvelle référence proposée** à côté du banc console `pb-bench` existant (net8.0, sans BDN ni document de référence) — aucune suppression ni migration du banc console n'accompagne ce document.

> **Re-mesure intégrale du 2026-10-27** (review PR #36) : la table précédente (protocole `Job.ShortRun` par défaut = 3 warmup / 3 itérations, alors que la doc annonçait 5/15) est **invalidée et remplacée** — aucun chiffre de l'ancienne table n'est réutilisé ni ré-étiqueté. Le protocole est désormais épinglé dans le code (`WithWarmupCount(5).WithIterationCount(15)`), et la mesure ci-dessous l'a exécuté tel quel.

## Environnement de mesure

| Élément | Valeur |
| --- | --- |
| Commit mesuré | `84de65c6` (branche `bench/18770-z3linq-baseline`, arbre du banc mesuré) |
| Runtime | .NET 8.0.33 (8.0.3326.52219), X64 RyuJIT AVX2 |
| SDK | 10.0.114 |
| BenchmarkDotNet | 0.14.0, InProcessEmitToolchain, 5 warmup / 15 itérations (épinglé), LaunchCount 1 |
| Machine | Windows 11 Pro 10.0.26300, Intel Alder Lake (Family 6 Model 154), 64 GB RAM, GC client, plan « Haute performance » |
| Durée du run | 2 min 14 s (8 bancs) |

## Résultats (net8.0, lib net8.0, solveur Z3 natif lié par la lib)

### Pipeline de théorème (par `Solve()` — contexte, réécriture d'arbre, AST Z3, solve, extraction du modèle)

| Benchmark | Mean | Error (CI 99,9 %) | Allocated |
| --- | ---: | ---: | ---: |
| `Solve bool pair (pipeline floor)` — type anonyme bool ×2 | 35,06 ms | ± 1,937 ms (5,5 %) | 6,42 KB |
| `Solve ints (linear constraints)` — type anonyme int ×3, 6 contraintes | 36,54 ms | ± 1,921 ms (5,3 %) | 18,15 KB |
| `Solve reals (oil purchase)` — `Symbols<double,double>`, 6 contraintes | 36,73 ms | ± 1,897 ms (5,2 %) | 17,59 KB |
| `Solve Symbols 5 vars` — `Symbols<int×5>`, 5 contraintes | 37,12 ms | ± 1,319 ms (3,6 %) | 16,17 KB |

### Constante `Rational` (micro-bancs)

| Benchmark | Mean | Error (CI 99,9 %) | Allocated |
| --- | ---: | ---: | ---: |
| `Rational.Of (reduction)` — GCD sur ~7M/3M | 10,73 ns | ± 0,217 ns (2,0 %) | 0 B |
| `Rational Equals (reduced pairs)` — 64 paires (32 égales, 32 inégales) | 832,95 ns | ± 47,27 ns (5,7 %) | 0 B |
| `Rational compare vs double (seeded data)` — 64 comparaisons sur tableaux RNG seedés | 127,41 ns | ± 4,225 ns (3,3 %) | 0 B |
| `Rational.ToString (literal emission)` — 2 × `"num/den"` | 127,51 ns | ± 2,119 ns (1,7 %) | 64 B |

## Lecture des mesures — limitée à ce que les tables portent

- **Plancher du pipeline re-mesuré à ~35 ms** : l'ancienne table 3/3 indiquait 15,5-19,9 ms — la mesure stabilisée (15 itérations après 5 warmups) **double le plancher constaté**. L'ancienne valeur n'est pas une variante de bruit : elle est invalidée, et toute comparaison future doit partir de la présente table.
- **La complexité ne domine pas** dans cette gamme : de 2 booléens (2 contraintes) à 5 variables entières (5 contraintes) ou 6 contraintes réelles, la moyenne reste dans ±6 % du plancher. Le coût marginal d'une contrainte ou d'une variable supplémentaire est petit devant le coût fixe du pipeline (création de contexte + réécriture + AST + `check` + extraction).
- **Allocations managées faibles devant le temps total** : 6-18 KB par `Solve()`. La répartition exacte natif/managé du temps de solve **n'est pas conclue par ces tables** — l'établir exigerait un profilage dédié, hors périmètre de la baseline.
- **`Rational` est ns et quasi sans allocation** (struct à deux `long`, 0 B sur trois chemins sur quatre) ; `ToString` (64 B par appel) reste le seul chemin allouant — chemin historique d'émission des littéraux vers `MkReal`.
- **`compare vs double` est désormais mesuré** (127 ns pour 64 comparaisons ≈ 2 ns/comparaison) : le chemin de l'opérateur de comparaison est négligeable devant le pipeline, **mesure à l'appui** — la version précédente du banc était une `ZeroMeasurement` (opérandes `static readonly` + littéraux, éliminées par le JIT) ; elle opère maintenant sur des tableaux générés au `GlobalSetup` (RNG seedé `Seed = 7`).

## Chemins de matérialisation couverts — et les deux gaps mesurés

Les bancs couvrent les deux chemins de matérialisation **fonctionnels** à cette tête :

- **types anonymes** — `bool` et `int` uniquement : la branche anonyme de `GetSolution` (Theorem.cs:1118-1130) ne dispatche que ces deux types ; un membre `double` lève `NotSupportedException` (« Unsupported parameter type ») ;
- **`Symbols<...>`** — `int` et `double` (le problème d'achat pétrolier à membres réels passe par `Symbols<double,double>` pour cette raison).

Deux gaps sont rapportés en issue amont dédiée (#35) :

1. **ValueTuple** : `NotSupportedException` dans `ConstructFromModel:1219` — l'appariement des noms de paramètres de ctor en `StringComparer.Ordinal` ne matche jamais les champs `Item1/Item2` contre les params `item1/item2` camel du ctor `ValueTuple`. `git log -S "ValueTuple" -- solutions/Z3.Linq/` est vide : jamais implémenté. **Le démo du dépôt s'arrête net à sa propre section ValueTuple** (Program.cs:79) — les sections suivantes (Record, Bart, Sudoku, Oil, Warehouse) ne sont jamais atteintes.
2. **Anonyme `double`** : cf. ci-dessus — le problème oil purchase du démo n'est matérialisable par **aucun** chemin ValueTuple/anonyme à cette tête.

## Périmètre honnête de la constante `Rational`

`Rational` n'expose **aucun opérateur arithmétique ration-ration (`+`, `-`, `*`, `/`, unaire `-`)** : c'est un porteur de constante exacte (numérateur/dénominateur `long`, forme réduite par GCD à la construction) destiné à l'arbre d'expression LINQ, où les opérations sont réécrites vers Z3 sans être évaluées en CLR. Les bancs couvrent donc uniquement les quatre chemins réellement publics : `Of` (réduction), `Equals` structurel, comparaisons contre `double` (chemin des prédicats `where`), `ToString` (émission de littéral). Une mesure d'arithmétique ration-ration exigerait d'ajouter ces opérateurs à l'API publique — hors périmètre d'une baseline.

## Protocole de reproduction

```bash
dotnet build benchmarks/Z3Linq.Benchmarks -c Release
dotnet run --project benchmarks/Z3Linq.Benchmarks -c Release --no-build
```

Le protocole (5 warmup / 15 itérations, InProcess) vit dans `BenchConfig.cs` — ce fichier est la source du protocole, la table ci-dessus la source des mesures. Les résultats s'impriment en console et dans `BenchmarkDotNet.Artifacts/` (ignoré par git). Chaque itération de pipeline paie le coût complet d'un appelant : création du `Z3Context`, réécriture de l'arbre d'expression, construction AST Z3, `check` + extraction du modèle.
