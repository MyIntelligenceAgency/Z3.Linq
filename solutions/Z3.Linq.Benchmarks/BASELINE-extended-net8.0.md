# Baseline étendue .NET 8.0 — pipeline de théorème, formes et `Rational`

Mesures de référence du corpus étendu de `solutions/Z3.Linq.Benchmarks` sur .NET 8.0,
sur la pile consolidée de `main` (`.slnx`, Z3 5.1.0). Elles complètent la jumelle
[`BASELINE-net8.0.md`](BASELINE-net8.0.md) : celle-ci documente les six formes de
`SolveBenchmarks` sur le poste qui les a produites, tandis que la présente table fixe
la référence **même machine** des quatorze charges, utilisée pour les deltas runtime
de l'EPIC CoursIA #18770.

> **Les chiffres antérieurs à cette consolidation sont invalidés et ne sont pas
> réutilisés.** La baseline de la PR amont #36 portait quatre pipelines à ~35 ms,
> mesurés sur l'ancienne bibliothèque avant le port de `main` vers Z3 5.1.0. Après ce
> port, les mêmes sources compilées contre la pile courante donnent 9,96 à 16,10 ms.
> Aucune valeur de l'ancienne table n'est ré-étiquetée : c'est une autre implémentation
> et un autre solveur.

## Environnement de mesure

| Élément | Valeur |
| --- | --- |
| Branche mesurée | `bench/18770-z3linq-baseline` après intégration de `origin/main` et consolidation du banc |
| Runtime | .NET 8.0.33 (8.0.3326.52219), X64 RyuJIT x86-64-v3 |
| SDK (build) | 10.0.114 |
| BenchmarkDotNet | 0.15.8 |
| Toolchain | `InProcessEmitToolchain`, `InvocationCount=1`, `UnrollFactor=1`, 5 warmup / 15 itérations, LaunchCount 1 |
| Machine | Windows 11 (10.0.26300.9711), Intel Core i7-12700H 2.70 GHz, 14 cœurs physiques / 20 logiques, plan « Haute performance » |
| Durée observée | 1 à 3 s par classe (`InProcess`), 14 benchmarks exécutés au total |

La colonne `Allocated` est la mémoire managée allouée par opération, mesurée par
`[MemoryDiagnoser]`. Un tiret signifie que le diagnostic n'a **rien observé**, pas
« zéro octet garanti ».

## 1. Formes d'environnement — `SolveBenchmarks`

Chaque théorème est construit **une fois** dans `[GlobalSetup]` ; la mesure ne paie que
`Solve`/`Optimize` et son travail interne de reconstruction d'environnement et de
traduction.

| Benchmark | Mean | Error (IC 99,9 %) | Allocated |
| --- | ---: | ---: | ---: |
| `ScalarSymbols` (référence) — `Symbols<int,int>` | 12,85 ms | ± 1,297 ms | 5,15 KB |
| `ValueTuple` — environnement value-tuple | 14,29 ms | ± 1,678 ms | 5,88 KB |
| `NestedObject` — objet imbriqué | 13,83 ms | ± 1,819 ms | 7,64 KB |
| `Collection` — boucle d'éléments | 11,60 ms | ± 1,429 ms | 6,91 KB |
| `WideSymbols` — `Symbols<int×5>` | 14,03 ms | ± 1,413 ms | 9,55 KB |
| `Optimize` — chemin de l'optimiseur | 13,14 ms | ± 1,286 ms | 6,59 KB |

## 2. Pipeline complet — `TheoremPipelineBenchmarks`

Contrairement à la section 1, **chaque itération** crée le `Z3Context`, construit le
théorème, réécrit l'arbre d'expression, produit l'AST Z3, exécute `check` puis extrait le
modèle. C'est le coût d'un appelant qui ne réutilise rien.

| Benchmark | Mean | Error (IC 99,9 %) | Allocated |
| --- | ---: | ---: | ---: |
| `Solve bool pair (pipeline floor)` — anonyme `bool ×2` | 9,960 ms | ± 1,401 ms | 7,23 KB |
| `Solve ints (linear constraints)` — anonyme `int ×3`, 6 contraintes | 16,099 ms | ± 2,053 ms | 18,09 KB |
| `Solve reals (oil purchase)` — `Symbols<double,double>`, 6 contraintes | 11,320 ms | ± 1,740 ms | 18,08 KB |
| `Solve Symbols 5 vars` — `Symbols<int×5>`, 5 contraintes | 11,700 ms | ± 2,043 ms | 16,09 KB |

## 3. Constante `Rational` — `RationalBenchmarks`

`Rational` est un porteur de constante exacte (numérateur/dénominateur `long`, forme
réduite à la construction) : l'API publique n'expose aucune arithmétique ration-ration.
Les charges couvrent donc les quatre chemins réellement publics.

| Benchmark | Mean | Error (IC 99,9 %) | Allocated |
| --- | ---: | ---: | ---: |
| `Rational.Of (reduction)` — GCD sur `1_000_003 × 7 / 3` | 1,707 µs | ± 0,439 µs | — |
| `Rational Equals (reduced pairs)` — 32 paires égales + 32 inégales | 5,887 µs | ± 0,534 µs | — |
| `Rational compare vs double (seeded data)` — 64 comparaisons, RNG `Seed = 7` | 2,783 µs | ± 0,474 µs | — |
| `Rational.ToString (literal emission)` — 2 littéraux `num/den` | 9,807 µs | ± 2,088 µs | 600 B |

## Limites observées pendant le run

BenchmarkDotNet signale `MinIterationTime` sur les quatorze charges : leurs itérations
sont de l'ordre de la microseconde à quelques millisecondes, sous le seuil recommandé
de 100 ms. Les moyennes sont donc sensibles au bruit de la machine.

Il signale aussi une distribution multimodale pour `SolveBenchmarks.ValueTuple`,
`SolveBenchmarks.WideSymbols`, `Solve bool pair`, `Solve ints` et `Solve Symbols 5 vars`.
Ces avertissements sont conservés : ils interdisent de lire un écart inférieur à
l'`Error` de la table comme un changement réel.

## Périmètre de matérialisation

Les deux chemins exercés par le corpus sont ceux de l'API courante : types anonymes
(bool et int) et `Symbols<...>` (int et réel). `ValueTuple` est mesuré par
`SolveBenchmarks.ValueTuple` ; les constantes exactes passent par `Rational`. Aucun
« gap » de l'ancienne bibliothèque n'est reproduit ici sans une mesure qui le montre.

## Reproduire

```bash
dotnet run --project solutions/Z3.Linq.Benchmarks -c Release -f net8.0 -- --filter '*'
```

Le protocole InProcess vit dans `SolveBenchmarks.cs` et `PipelineConfig.cs`. Il est
identique à celui des baselines sœurs MetaGeneticSharp et Automata (CoursIA #18770).
Les trois jambes runtime .NET 9/10/11 RC, sur ce même corpus et ce même binaire, sont
dans [`BASELINE-multi-runtime.md`](BASELINE-multi-runtime.md).
