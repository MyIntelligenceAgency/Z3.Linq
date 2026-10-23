# Baseline .NET 8 — bancs BenchmarkDotNet Z3.Linq

Baseline de référence pour la mesure des gains runtime .NET 10/11 (issue CoursIA #18770, fille de l'EPIC #18695). Banc : `benchmarks/Z3Linq.Benchmarks` (BenchmarkDotNet 0.14.0), exerçant l'**API publique** du provider LINQ-to-Z3 sur le pipeline complet de théorème et sur la constante « numerics » `Rational`. Remplace le banc console brut `pb-bench` (net8.0, sans BDN ni document de référence) comme point de mesure du dépôt.

## Environnement de mesure

| Élément | Valeur |
| --- | --- |
| Commit mesuré | `8a2562d` (pointe de la branche par défaut, ligne suivie par le submodule CoursIA) |
| Runtime | .NET 8.0.32 (8.0.3226.50405), X64 RyuJIT AVX2 |
| SDK | 10.0.113 |
| BenchmarkDotNet | 0.14.0, InProcessEmitToolchain, Job.ShortRun (résolution par défaut : 3 warmup / 3 itérations) |
| Machine | Windows 11 Pro 10.0.26300, Intel Alder Lake (Family 6 Model 154), 64 GB RAM, GC client |

## Résultats (net8.0, lib net8.0, solveur Z3 natif lié par la lib)

### Pipeline de théorème (par `Solve()` — contexte, réécriture d'arbre, AST Z3, solve, extraction du modèle)

| Benchmark | Mean | Error | Allocated |
| --- | ---: | ---: | ---: |
| `Solve bool pair (pipeline floor)` — type anonyme bool ×2 | 15,45 ms | 8,173 ms | 6,36 KB |
| `Solve ints (linear constraints)` — type anonyme int ×3, 6 contraintes | 19,86 ms | 38,052 ms | 18,09 KB |
| `Solve reals (oil purchase)` — `Symbols<double,double>`, 6 contraintes | 16,43 ms | 21,271 ms | 17,53 KB |
| `Solve Symbols 5 vars` — `Symbols<int×5>`, 5 contraintes | 19,75 ms | 20,650 ms | 16,11 KB |

### Constante `Rational` (micro-bancs)

| Benchmark | Mean | Error | Allocated |
| --- | ---: | ---: | ---: |
| `Rational.Of (reduction)` — GCD sur ~7M/3M | 8,94 ns | 2,24 ns | 0 B |
| `Rational Equals (reduced pairs)` — 64 paires (32 égales, 32 inégales) | 633,10 ns | 52,43 ns | 0 B |
| `Rational compare vs double` — 4 comparaisons sur constantes | ~0 ns | — | 0 B |
| `Rational.ToString (literal emission)` — `"num/den"` | 63,25 ns | 69,60 ns | 64 B |

## Lecture des hot paths

- **Le plancher du pipeline domine** : ~15,5 ms pour deux booléens — création de contexte, réécriture de l'arbre, construction AST, `check` et extraction. La croissance avec la complexité est modérée (15,5 → 19,9 ms pour 3 entiers/6 contraintes ; 19,8 ms pour 5 variables `Symbols`). Le coût est quasi intégralement **natif Z3 + réflexion de matérialisation** : les allocations managées (6-18 KB par solve) sont négligeables devant le temps.
- **`Rational` est gratuit en relatif** : toutes les opérations sont ns et quasi sans allocation (struct à deux `long`). `ToString` (63 ns, 64 B) reste le seul chemin allouant — c'est le chemin historique d'émission des littéraux vers `MkReal`.
- **`compare vs double` est un `ZeroMeasurement`** : sur constantes `static readonly`, le JIT élime les comparaisons (~0 ns mesuré, warning BDN). L'information utile : le coût des prédicats `where` réels vit dans la réécriture de l'arbre, pas dans l'opérateur. Pour une mesure non éliminée, le banc devrait dépendre d'opérandes mutables (`[Params]`) — laissé tel quel documenté.
- **Erreurs relatives élevées** (ShortRun, 3 itérations) : normales pour des charges à ~15-20 ms dominées par le solveur natif ; suffisant pour détecter les gains d'un ordre de grandeur attendus du runtime .NET 10/11 sur les parties managées. Un run `Job.Default` complète si une précision supérieure devient nécessaire.

## Chemins de matérialisation couverts — et les deux gaps mesurés à cette tête

Les bancs couvrent les deux chemins de matérialisation **fonctionnels** à `8a2562d` :

- **types anonymes** — `bool` et `int` uniquement : la branche anonyme de `GetSolution` (Theorem.cs:1118-1130) ne dispatche que ces deux types ; un membre `double` lève `NotSupportedException` (« Unsupported parameter type ») ;
- **`Symbols<...>`** — `int` et `double` (le problème d'achat pétrolier à membres réels passe par `Symbols<double,double>` pour cette raison).

Deux gaps sont rapportés en issue amont dédiée :

1. **ValueTuple** : `NotSupportedException` dans `ConstructFromModel:1219` — l'appariement des noms de paramètres de ctor en `StringComparer.Ordinal` ne matche jamais les champs `Item1/Item2` contre les params `item1/item2` camel du ctor `ValueTuple`. `git log -S "ValueTuple" -- solutions/Z3.Linq/` est vide : jamais implémenté. **Le démo du dépôt s'arrête net à sa propre section ValueTuple** (Program.cs:79) — les sections suivantes (Record, Bart, Sudoku, Oil, Warehouse) ne sont jamais atteintes.
2. **Anonyme `double`** : cf. ci-dessus — le problème oil purchase du démo n'est matérialisable par **aucun** chemin ValueTuple/anonyme à cette tête.

## Périmètre honnête de la constante `Rational`

`Rational` n'expose **aucun opérateur arithmétique ration-ration (`+`, `-`, `*`, `/`, unaire `-`)** : c'est un porteur de constante exacte (numérateur/dénominateur `long`, forme réduite par GCD à la construction) destiné à l'arbre d'expression LINQ, où les opérations sont réécrites vers Z3 sans être évaluées en CLR. Les bancs couvrent donc uniquement les quatre chemins réellement publics : `Of` (réduction), `Equals` structurel, comparaisons contre `double` (chemin des prédicats `where`), `ToString` (émission de littéral). Une mesure d'arithmétique ration-ration exigerait d'ajouter ces opérateurs à l'API publique — hors périmètre d'une baseline.

## Protocole de reproduction

```bash
dotnet build benchmarks/Z3Linq.Benchmarks -c Release
dotnet run --project benchmarks/Z3Linq.Benchmarks -c Release --no-build
```

Les résultats s'impriment en console et dans `BenchmarkDotNet.Artifacts/` (ignoré par git). Chaque itération de pipeline paie le coût complet d'un appelant : création du `Z3Context`, réécriture de l'arbre d'expression, construction AST Z3, `check` + extraction du modèle.
