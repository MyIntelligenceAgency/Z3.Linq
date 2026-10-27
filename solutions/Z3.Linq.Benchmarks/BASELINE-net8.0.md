# Baseline .NET 8.0 — Z3.Linq.Benchmarks

Mesures de référence de l'API publique de Z3.Linq sur .NET 8, point de départ
des comparaisons .NET 11 (EPIC gains runtime — CoursIA issues #18770 et #18695,
digest Stephen Toub 2026-09-15). Sœur des baselines MetaGeneticSharp
(.NET 9, 2026-10-02) et Automata (.NET 8/9, 2026-10-19) : même protocole,
même toolchain. Jumelle du baseline .NET 10 de cette même suite (même jour,
même machine) — les deltas inter-TFM se lisent entre les deux fichiers.

## Environnement

| Élément | Valeur |
| --- | --- |
| Date | 2026-10-27 |
| OS | Windows 11 Pro (10.0.26300.9711) |
| CPU | 11th Gen Intel Core i7-11800H 2.30GHz, 8 cœurs physiques / 16 logiques |
| Runtime | .NET 8.0.33 (8.0.3326.52219), X64 RyuJIT x86-64-v4 |
| SDK (build) | 10.0.114 |
| BenchmarkDotNet | 0.15.8 |
| Toolchain | InProcessEmitToolchain, `InvocationCount=1`, `UnrollFactor=1`, 5 warmup / 15 itérations |
| Machine | poste de travail portable (laptop), alimentation secteur |

## Charges mesurées

Toutes passent par l'API publique (`Solve`/`Optimize` du provider LINQ) — aucune
I/O, aucun réseau, charge **figée** en `Setup` (Z3 est déterministe : pas de RNG,
mêmes théorèmes à chaque itération ; la mesure isole le travail par-solve de la
lib — construction de l'environnement par réflexion, traduction des arbres
d'expressions, marshalling du modèle — le solveur Z3 lui-même décidant en
microsecondes).

| Benchmark | Charge |
| --- | --- |
| `ScalarSymbols` (baseline) | `Symbols<int,int>` plat |
| `ValueTuple` | chemin d'environnement value-tuple |
| `NestedObject` | marshalling récursif d'un objet imbriqué |
| `Collection` | boucle d'éléments d'un tableau |
| `WideSymbols` | `Symbols<int,int,int,int,int>` — plus de membres à réfléchir/traduire |
| `Optimize` | chemin de solve propre à l'optimiseur |

## Résultats (job InProcessEmitToolchain — référence)

```text
| Method        | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0      | Gen1      | Gen2      | Allocated | Alloc Ratio |
|---------------|---------:|---------:|---------:|------:|--------:|----------:|----------:|----------:|----------:|------------:|
| ScalarSymbols | 17.04 ms | 2.122 ms | 1.985 ms |  1.01 |    0.16 | 1000.0000 | 1000.0000 | 1000.0000 |   4.45 KB |        1.00 |
| ValueTuple    | 13.74 ms | 2.254 ms | 2.108 ms |  0.82 |    0.15 | 1000.0000 | 1000.0000 | 1000.0000 |   4.89 KB |        1.10 |
| NestedObject  | 14.42 ms | 1.693 ms | 1.584 ms |  0.86 |    0.13 | 1000.0000 | 1000.0000 | 1000.0000 |   6.66 KB |        1.50 |
| Collection    | 13.00 ms | 0.898 ms | 0.750 ms |  0.77 |    0.10 | 1000.0000 | 1000.0000 | 1000.0000 |   6.59 KB |        1.48 |
| WideSymbols   | 13.03 ms | 0.934 ms | 0.874 ms |  0.77 |    0.10 | 1000.0000 | 1000.0000 | 1000.0000 |   8.89 KB |        2.00 |
| Optimize      | 12.69 ms | 1.817 ms | 1.699 ms |  0.75 |    0.13 | 1000.0000 | 1000.0000 | 1000.0000 |   6.55 KB |        1.47 |
```

## Reproduire

```bash
cd solutions/Z3.Linq.Benchmarks
dotnet run -c Release -f net8.0 -- --filter '*'
```

Le protocole de référence (job InProcess ci-dessus) est la config par défaut de
la suite ; `DOTNET_MULTI_RUNTIME=1` le désactive au profit du toolchain process
enfant pour les runs delta multi-runtime (`--runtimes`).

## Lecture

- Le `Mean` ~13-17 ms est dominé par le travail par-solve de la lib (réflexion,
  traduction, marshalling), pas par le solveur — voir le README de la suite.
- Colonnes Gen0/Gen1/Gen2 à 1000.0/1000 op = 1 collection par opération : BDN
  force le GC entre itérations quand `InvocationCount=1` ; la comparabilité
  inter-TFM et inter-sœurs tient car toutes partagent ce protocole.
- Deltas mesurés vers la jumelle .NET 10 (même jour, même machine) :
  `ScalarSymbols` -32 % de Mean, mais allocations par solve en hausse partout
  (+37 % à +95 % selon la shape). Observation falsifiable d'un run unique sur
  une machine unique — à confirmer avant toute conclusion runtime.
