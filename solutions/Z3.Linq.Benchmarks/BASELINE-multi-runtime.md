# Deltas runtime .NET 8 / 9 / 10 / 11 — Z3.Linq.Benchmarks

Comparaison des **quatorze mêmes charges**, compilées une seule fois en `net8.0` et
exécutées sous quatre runtimes. L'exécutable net8 est lancé directement par le `dotnet`
de chaque racine runtime isolée : aucune recompilation, aucun job enfant, donc aucune
troisième jambe parasite. Référence : CoursIA #18770, fille de l'EPIC #18695.

## Pourquoi cette méthode

`BenchmarkDotNet` 0.15.8 ne sait pas créer un job pour le moniker du SDK .NET 11 RC
(`GetRuntimeVersion not implemented for NotRecognized`). La méthode `--runtimes` est
donc inutilisable pour cette jambe.

Une seconde voie, consistant à ajouter un job `ChildProcess` tout en passant
`--runtimes net9.0 net10.0`, a été **écartée après mesure** : elle produit une troisième
jambe net8 en plus des deux demandées, et le résultat ne serait pas la matrice annoncée.

La méthode retenue contourne les deux problèmes : le même binaire `net8.0` est exécuté
par le `dotnet.exe` de chaque runtime installé, en `InProcess`, avec
`DOTNET_ROLL_FORWARD=Major` parce que le `runtimeconfig.json` demande 8.0 et que chaque
racine isolée ne contient que sa propre version. Chaque journal porte la ligne `[Host]`
du runtime effectivement chargé ; c'est cette ligne, pas le nom du dossier, qui atteste
la jambe.

## Protocole

| Élément | Valeur |
| --- | --- |
| Binaire mesuré | `solutions/Z3.Linq.Benchmarks/bin/Release/net8.0` (une seule compilation) |
| Runtimes | .NET 8.0.33 · 9.0.22 · 10.0.14 · 11.0.0-rc.3.26522.148 |
| SDK de build | 10.0.114 |
| BenchmarkDotNet | 0.15.8 |
| Toolchain | `InProcessEmitToolchain`, `InvocationCount=1`, `UnrollFactor=1`, 5 warmup / 15 itérations, LaunchCount 1 |
| `DOTNET_MULTI_RUNTIME` | **absent** (le job InProcess de référence doit être actif) |
| `DOTNET_ROLL_FORWARD` | `Major` pour chaque racine isolée |
| Machine | Windows 11 (10.0.26300.9711), Intel Core i7-12700H 2.70 GHz, 14 cœurs physiques / 20 logiques, plan « Haute performance » |
| Date | 2026-10-27 |

Les quatre runs sont **séquentiels**, sur une machine au repos. Les valeurs `Mean` sont
donc comparables entre elles, mais chaque colonne conserve l'`Error` de son run.

## 1. Formes d'environnement — `SolveBenchmarks` (ms)

| Benchmark | .NET 8 | .NET 9 | .NET 10 | .NET 11 RC | 11 vs 8 |
| --- | ---: | ---: | ---: | ---: | ---: |
| `ScalarSymbols` | 12,85 | 10,996 | 10,25 | 13,629 | +6,1 % |
| `ValueTuple` | 14,29 | 11,560 | 13,29 | 13,502 | −5,5 % |
| `NestedObject` | 13,83 | 11,545 | 13,70 | 11,777 | −14,8 % |
| `Collection` | 11,60 | 9,571 | 12,95 | 9,459 | −18,5 % |
| `WideSymbols` | 14,03 | 7,955 | 13,32 | 11,667 | −16,8 % |
| `Optimize` | 13,14 | 10,021 | 12,45 | 10,295 | −21,7 % |

## 2. Pipeline complet — `TheoremPipelineBenchmarks` (ms)

| Benchmark | .NET 8 | .NET 9 | .NET 10 | .NET 11 RC | 11 vs 8 |
| --- | ---: | ---: | ---: | ---: | ---: |
| `Solve bool pair` | 9,960 | 10,91 | 10,20 | 9,390 | −5,7 % |
| `Solve ints` | 16,099 | 15,40 | 14,04 | 11,685 | −27,4 % |
| `Solve reals` | 11,320 | 13,77 | 10,88 | 8,343 | −26,3 % |
| `Solve Symbols 5 vars` | 11,700 | 15,37 | 11,28 | 11,233 | −4,0 % |

## 3. Constante `Rational` — `RationalBenchmarks` (µs)

| Benchmark | .NET 8 | .NET 9 | .NET 10 | .NET 11 RC | 11 vs 8 |
| --- | ---: | ---: | ---: | ---: | ---: |
| `Rational.Of (reduction)` | 1,707 | 0,615 | 0,937 | 0,547 | −68,0 % |
| `Rational Equals (reduced pairs)` | 5,887 | 2,797 | 3,133 | 3,671 | −37,6 % |
| `Rational compare vs double` | 2,783 | 0,857 | 0,967 | 1,140 | −59,0 % |
| `Rational.ToString` | 9,807 | 6,213 | 5,173 | 4,913 | −49,9 % |

## 4. Allocations managées par opération

| Benchmark | .NET 8 | .NET 9 | .NET 10 | .NET 11 RC |
| --- | ---: | ---: | ---: | ---: |
| `ScalarSymbols` | 5,15 KB | 13,58 KB | 13,59 KB | 14,22 KB |
| `ValueTuple` | 5,88 KB | 13,46 KB | 13,76 KB | 14,38 KB |
| `NestedObject` | 7,64 KB | 15,52 KB | 15,53 KB | 16,15 KB |
| `Collection` | 6,91 KB | 15,73 KB | 15,75 KB | 16,36 KB |
| `WideSymbols` | 9,55 KB | 17,42 KB | 17,44 KB | 18,04 KB |
| `Optimize` | 6,59 KB | 14,75 KB | 15,09 KB | 15,70 KB |
| `Solve bool pair` | 7,23 KB | 15,91 KB | 16,59 KB | 16,55 KB |
| `Solve ints` | 18,09 KB | 26,74 KB | 27,42 KB | 27,01 KB |
| `Solve reals` | 18,08 KB | 26,09 KB | 26,40 KB | 26,79 KB |
| `Solve Symbols 5 vars` | 16,09 KB | 24,76 KB | 25,02 KB | 24,96 KB |
| `Rational.Of` / `Equals` / `compare` | — | — | — | — |
| `Rational.ToString` | 600 B | 600 B | 600 B | — |

Sur .NET 11, `Rational.ToString` n'a **aucune allocation observée** par le diagnostic :
le tiret signifie « non mesuré », jamais « zéro octet garanti ». Les trois autres charges
`Rational` sont également à tiret sur les quatre runtimes. Ce point n'est pas converti en
gain.

## Lecture — bornée par les mesures

- **Les micro-chemins `Rational` sont nettement plus rapides à partir de .NET 9** :
  `Of` passe de 1,707 µs à 0,547 µs sur .NET 11 (−68 %), `compare vs double` de 2,783 à
  1,140 µs (−59 %), `ToString` de 9,807 à 4,913 µs (−50 %). Ces écarts dépassent
  largement les `Error` de leurs tables.
- **Le pipeline complet n'a pas un gain uniforme.** `Solve ints` et `Solve reals`
  baissent d'environ 27 % sur .NET 11, mais `Solve bool pair` et `Solve Symbols 5 vars`
  restent dans quelques pour cent. `ScalarSymbols` est même 6 % plus lent, écart non
  concluant au regard de son `Error`.
- **Les formes d'environnement sont instables entre runtimes** : `WideSymbols` est la
  plus rapide sur .NET 9 (7,955 ms) et remonte sur .NET 11 (11,667 ms), alors que
  `Collection` est plus rapide sur 9 et 11 qu'en 8. Un run unique par runtime ne permet
  pas de trancher si ces mouvements sont dus au JIT, au solveur natif ou au bruit de la
  machine.
- **Les allocations par solve augmentent fortement entre .NET 8 et les suivants**
  (environ ×2 sur les petites formes, +30 à +55 % sur le pipeline). Ce n'est pas un
  effet attendu d'un simple changement de version ; les tables le montrent mais ne
  l'expliquent pas. Toute optimisation qui s'appuierait dessus doit d'abord reproduire
  le phénomène et l'attribuer.
- **Avertissements conservés** : `MinIterationTime` sur les quatorze charges et
  distributions multimodales sur plusieurs formes, dans les quatre journaux. Les deltas
  inférieurs à l'`Error` ne sont pas des résultats.

## Reproduire une jambe

```powershell
dotnet build solutions/Z3.Linq.Benchmarks -c Release -f net8.0
$env:DOTNET_ROOT = '<racine-runtime-isolee>'
$env:DOTNET_ROLL_FORWARD = 'Major'
Remove-Item Env:DOTNET_MULTI_RUNTIME -ErrorAction SilentlyContinue
& '<racine-runtime-isolee>\dotnet.exe' 'solutions/Z3.Linq.Benchmarks/bin/Release/net8.0/Z3.Linq.Benchmarks.dll' --filter '*'
```

Vérifier dans la sortie la ligne `[Host] : .NET <version>` avant de retenir le moindre
chiffre. Un échec de l'hôte rend `0x80008096` et aucune mesure : ce n'est pas un run.
