# Baseline .NET 10.0 — Z3.Linq.Benchmarks

Mesures de référence de l'API publique de Z3.Linq sur .NET 10, point de départ
des comparaisons .NET 11 (EPIC gains runtime — CoursIA issues #18770 et #18695,
digest Stephen Toub 2026-09-15). Sœur des baselines MetaGeneticSharp
(.NET 9, 2026-10-02) et Automata (.NET 8/9, 2026-10-19) : même protocole,
même toolchain. Jumelle du baseline .NET 8 de cette même suite (même jour,
même machine) — les deltas inter-TFM se lisent entre les deux fichiers.

## Environnement

| Élément | Valeur |
| --- | --- |
| Date | 2026-10-27 |
| OS | Windows 11 Pro (10.0.26300.9711) |
| CPU | 11th Gen Intel Core i7-11800H 2.30GHz, 8 cœurs physiques / 16 logiques |
| Runtime | .NET 10.0.14 (10.0.1426.52326), X64 RyuJIT x86-64-v4 |
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
| ScalarSymbols | 11.64 ms | 0.690 ms | 0.646 ms |  1.00 |    0.08 | 1000.0000 | 1000.0000 | 1000.0000 |   6.09 KB |        1.00 |
| ValueTuple    | 12.23 ms | 0.860 ms | 0.762 ms |  1.05 |    0.09 | 1000.0000 | 1000.0000 | 1000.0000 |   9.54 KB |        1.57 |
| NestedObject  | 12.76 ms | 0.936 ms | 0.875 ms |  1.10 |    0.09 | 1000.0000 | 1000.0000 | 1000.0000 |  11.59 KB |        1.90 |
| Collection    | 10.43 ms | 0.473 ms | 0.419 ms |  0.90 |    0.06 | 1000.0000 | 1000.0000 | 1000.0000 |  11.81 KB |        1.94 |
| WideSymbols   | 13.09 ms | 1.989 ms | 1.860 ms |  1.13 |    0.17 | 1000.0000 | 1000.0000 | 1000.0000 |  13.22 KB |        2.17 |
| Optimize      | 11.18 ms | 0.639 ms | 0.598 ms |  0.96 |    0.07 | 1000.0000 | 1000.0000 | 1000.0000 |  11.16 KB |        1.83 |
```

## Reproduire

```bash
cd solutions/Z3.Linq.Benchmarks
dotnet run -c Release -f net10.0 -- --filter '*'
```

Le protocole de référence (job InProcess ci-dessus) est la config par défaut de
la suite ; `DOTNET_MULTI_RUNTIME=1` le désactive au profit du toolchain process
enfant pour les runs delta multi-runtime (`--runtimes`).

## Lecture

- Le `Mean` ~10-13 ms est dominé par le travail par-solve de la lib (réflexion,
  traduction, marshalling), pas par le solveur — voir le README de la suite.
- Colonnes Gen0/Gen1/Gen2 à 1000.0/1000 op = 1 collection par opération : BDN
  force le GC entre itérations quand `InvocationCount=1` ; la comparabilité
  inter-TFM et inter-sœurs tient car toutes partagent ce protocole.
- Deltas mesurés depuis la jumelle .NET 8 (même jour, même machine) : `Mean`
  plus rapide sur 5 shapes sur 6 (`ScalarSymbols` -32 %, `Collection` -20 %),
  mais allocations par solve en hausse partout (+37 % à +95 % selon la shape).
  Observation falsifiable d'un run unique sur une machine unique — à confirmer
  avant toute conclusion runtime.
