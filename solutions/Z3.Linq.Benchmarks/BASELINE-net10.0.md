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
- Deltas vers la jumelle .NET 8, **mesurés 3 fois** (même machine, même
  protocole, même jour) :
  - `Mean` — **non concluant**. Le run 1 donnait .NET 10 plus rapide sur
    5 shapes sur 6 (`ScalarSymbols` -32 %, `Collection` -20 %) ; le run 2 donne
    10.52 vs 11.67 ms sur `ScalarSymbols` (-10 %) et `Collection` **plus lent**
    (11.85 vs 10.27 ms). Le `Mean` de la jumelle .NET 8 varie lui-même de -32 %
    à -4 % d'un run à l'autre, et les `RatioSD` (0.14 à 0.20) recouvrent 1.0 :
    l'amplitude du bruit dépasse l'écart inter-TFM. Aucune conclusion de vitesse.
  - **Allocations — reproduites.** Hausse sur les six shapes dans les trois
    runs, +37 % à +101 % selon la shape (`ScalarSymbols` +37 %, `ValueTuple`
    +95…+101 %, `NestedObject` +73…+74 %). Les valeurs par opération sont
    stables à ±3 % entre runs pour ce TFM (6.09 KB identique sur `ScalarSymbols`
    au run 3 aussi), donc cette régression-là est un fait mesuré, pas du bruit.
    Ce seuil ne vaut en revanche pas pour le TFM .NET 8, dont `Optimize` varie
    de 6.55 à 5.94 KB (-9 %). Voir
    [`BASELINE-runtime-crosscheck.md`](BASELINE-runtime-crosscheck.md) : le
    contrôle croisé isole le runtime de la cible de compilation et montre que
    **la composition du processus** est le facteur qui change l'ampleur de ce
    delta (+1.64 KB à composition 6 charges, +7.88 KB à composition 14).
