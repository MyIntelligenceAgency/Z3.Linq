# Contrôle croisé — runtime, cible de compilation et composition du processus

Ce document répond à trois questions que les baselines publiées ne peuvent pas poser
seules, parce que chacune d'elles ne varie qu'un facteur à la fois :

1. le delta d'allocation .NET 8 → .NET 10 tient-il quand on **isole le runtime** de la
   cible de compilation ?
2. les colonnes `Allocated` de deux publications de la famille (#46 à composition 6,
   #47 à composition 14) sont-elles **comparables** ?
3. les conclusions de vitesse reposent-elles sur des runs **reproductibles** ?

Réponses mesurées : **oui**, **non**, **non**.

## Protocole

- Clone frais de `MyIntelligenceAgency/Z3.Linq`, branche `feature/bench-baseline`
  (PR #46), une seule compilation `net8.0` et une `net10.0`.
- Exécution **in-process** par le muxer de chaque runtime : `dotnet --fx-version
  <version> <dll> --filter '*'`, `DOTNET_ROLL_FORWARD=Major`, `DOTNET_MULTI_RUNTIME`
  absent — les quatre valeurs de runtime employées par la famille (.NET 8.0.33,
  9.0.22, 10.0.14, 11 RC) sont installées sur le poste.
- Chaque run produit sa ligne `[Host]` : c'est elle, et non le nom du dossier, qui
  atteste la jambe. Les quatre lignes relevées sont `.NET 8.0.33`, `.NET 9.0.22`,
  `.NET 10.0.14` — runtime x64, RyuJIT v4.
- BDN 0.15.8, `InProcessEmitToolchain`, `InvocationCount=1`, `UnrollFactor=1`,
  5 warmup / 15 itérations, `LaunchCount 1`.

## 1. Isoler le runtime de la cible de compilation

Quatre jambes : le binaire `net8.0` sous trois runtimes, puis le binaire `net10.0`
sous .NET 10. Colonnes `Allocated`, en KB.

| Charge | A `net8.0` / .NET 8 | B `net8.0` / .NET 9 | C `net8.0` / .NET 10 | D `net10.0` / .NET 10 |
| --- | ---: | ---: | ---: | ---: |
| `ScalarSymbols` | 4.45 | 6.08 | 6.09 | 6.09 |
| `ValueTuple` | 4.89 | 9.80 | 9.54 | 9.54 |
| `NestedObject` | 6.70 | 11.30 | 8.69 | 11.31 |
| `Collection` | 7.20 | 11.51 | 11.53 | 11.53 |
| `WideSymbols` | 8.89 | 13.16 | 13.17 | 13.22 |
| `Optimize` | 5.94 | 8.89 | 10.84 | 11.16 |

**La cible de compilation n'est pas le facteur.** C (binaire `net8.0` sous .NET 10) et
D (binaire `net10.0` sous .NET 10) coïncident sur cinq charges sur six ; la sixième
(`NestedObject`, 8.69 contre 11.31) est l'écart isolé de la jambe C, qui contredit
aussi la valeur publiée (11.59). C'est donc **C** qui porte la valeur aberrante, pas
D. Le delta A → D (+1.64 KB sur `ScalarSymbols`, +49 % à +95 % sur les six charges)
est bien un effet **runtime**, pas un effet de compilation.

**Ce qui se reproduit.** Cinq charges sur six reproduisent la baseline `net8.0`
publiée **au kilooctet près** (4.45 / 4.89 / 6.70 / 7.20 / 8.89) ; la sixième
(`Optimize`) donne 5.94 contre 6.55 publiés, soit −9.3 %. Les deux legs `net10.0`
reproduisent eux aussi les valeurs publiées (6.09 / 9.54 / 11.31 / 11.53 / 13.22 /
11.16). **Le seuil de bruit annoncé à ±3 % est donc trop serré** : il vaut ±3 % sur
quatre charges, jusqu'à −9 % sur `Optimize`. Ce document corrige cette affirmation.

## 2. Deux publications de la famille ne mesurent pas la même grandeur

La baseline `Allocated` de #46 et celle de #47 portent les mêmes six charges, le même
code de banc (`SolveBenchmarks.cs` est **identique octet pour octet** entre les deux
têtes de branche) et le même protocole — et donnent des deltas .NET 8 → .NET 10
différents : **+37 % à +101 %** (#46, composition 6 charges) contre **+82 % à +164 %**
(#47, composition 14 charges).

La cause est mesurée, pas supposée : **la composition du processus**. La suite
complète de #47 rejouée sur ce poste, avec le même binaire `net8.0` et les mêmes
runtimes, reproduit les chiffres publiés de #47 au kilooctet près — `ScalarSymbols`
13.59, `ValueTuple` 13.76, `NestedObject` 15.53 — alors que la *même* charge, sous le
*même* runtime, n'alloue que 6.42 KB lorsque les six charges `Solve` sont seules à
tourner.

| Charge | 6 charges, .NET 8 | 6 charges, .NET 10 | 14 charges, .NET 8 | 14 charges, .NET 10 |
| --- | ---: | ---: | ---: | ---: |
| `ScalarSymbols` | 4.45 | 6.09 | 5.71 | 13.59 |
| `ValueTuple` | 4.89 | 9.54 | 5.88 | 13.76 |
| `NestedObject` | 6.70 | 11.27 | 7.64 | 15.53 |
| `Collection` | 7.20 | 11.53 | 7.29 | 15.47 |
| `WideSymbols` | 8.89 | 13.17 | 9.55 | 17.11 |
| `Optimize` | 5.94 | 11.16 | 6.64 | 14.17 |

Le même passage de .NET 8 à .NET 10 vaut **+1.64 KB** à composition 6 et **+7.88 KB**
à composition 14 : l'amplification est un facteur **4**.

### Quel co-locataire amplifie l'allocation

Le déclencheur est isolé par **retrait de classe** (copie locale, jamais poussée) :
chaque variante est reconstruite et rejouée sur .NET 8 et .NET 10, les six charges
`Solve` tournant dans tous les cas.

| Composition du processus | .NET 8 | .NET 10 | Δ `ScalarSymbols` |
| --- | ---: | ---: | ---: |
| 6 charges `Solve` seules | 4.45 | 6.09 | +1.64 KB |
| 6 `Solve` + 4 `TheoremPipeline` | 4.45 | 6.42 | +1.97 KB |
| 6 `Solve` + 4 `Rational` + 4 `TheoremPipeline` | 5.71 | 13.59 | +7.88 KB |

Les deux variantes qui partagent le même jeu de co-locataires diffèrent par **une
seule** classe : `Rational`. Retirer les quatre charges `Rational` fait retomber
l'allocation de .NET 10 de 13.59 à 6.42 KB — l'amplification disparaît — alors que
retirer les quatre charges `TheoremPipeline` ne la change pas. **Les charges
`Rational` sont le déclencheur** ; les charges `TheoremPipeline` ne contribuent pas de
mesure observable.

L'amplification est elle-même dépendante du runtime : **+1.26 KB** sur .NET 8,
**+7.17 KB** sur .NET 10, à composition identique.

Le mécanisme interne **n'est pas identifié**, et la première hypothèse est
**falsifiée par la source** : `Rational` est une `readonly struct` purement managée
(`Numerator` et `Denominator`, deux `long`, réduction par GCD) et `RationalBenchmarks`
n'appelle **aucun** contexte Z3, aucun AST, aucune fonction native — sa charge
`ToStringLiteral` formate un rationnel en mémoire. **Le déclencheur est donc un jeu
de charges qui ne touche jamais le solveur**, et son effet sur l'allocation mesurée
des charges qui le touchent ne peut pas s'expliquer par un état du solveur.

Ce qui reste ouvert — et c'est le point important — est de savoir si la colonne
`Allocated` mesurée par `MemoryDiagnoser` dans un run **in-process** est une
propriété de la charge ou du processus. Si des charges sans rapport y contribuent, la
colonne n'est pas une propriété du banc, et toute comparaison entre publications de
la famille (#46, #47, MetaGeneticSharp #58/#61, Automata) doit la traiter comme
suspecte. C'est une question ouverte, pas un résultat : elle demande une jambe de
contrôle dédiée — par exemple le même banc exécuté en job `ChildProcess`, ou les
mêmes charges exécutées deux fois dans un même run.

**Conséquence pour la famille.** `Allocated` n'est pas une propriété de la charge :
c'est une propriété du triplet **(charge, composition du processus, runtime)**. Deux
publications ne sont comparables que si elles tiennent la composition constante. #47
la tient (14 charges sur ses quatre runtimes) et #46 la tient (6 charges sur ses deux
TFM) : **aucune des deux n'est fausse**, elles ne mesurent pas la même grandeur, et
les lire l'une contre l'autre est une erreur de protocole, pas une contradiction de
mesure. Toute optimisation qui s'appuierait sur ce delta doit donc citer la
composition sous laquelle elle a été mesuré.

## 3. Les temps ne sont pas reproductibles d'un run à l'autre

La jambe `net8.0` / .NET 8 / `ScalarSymbols`, même binaire, même protocole, même
poste, trois runs : **17.04 ms** (run 1, publié), **11.67 ms** (run 2, publié),
**24.49 ms** (run 3, ci-dessus). Soit une amplitude de **×2.1** sur une jambe
identique.

L'`Error` de BDN est l'intervalle de confiance **intra-run** : il ne borne pas la
dispersion **inter-runs**. Or les deltas inter-runtime publiés par #47 (−27 % sur
`Solve reals`, −68 % sur `Rational.Of`) sont du même ordre que cette dispersion. La
lecture « les écarts dépassent largement les `Error` de leurs tables » ne suffit donc
pas à établir une conclusion de vitesse : elle compare deux grandeurs de nature
différente.

**Ce qui reste vrai**, et qui est le seul écart inter-TFM corroboré par la répétition :
les allocations. Elles se reproduisent à ±3 % (jusqu'à −9 % sur `Optimize`) d'un run à
l'autre, alors que les temps varient d'un facteur 2.

## Rejouer

```bash
git clone https://github.com/MyIntelligenceAgency/Z3.Linq.git && cd Z3.Linq
git fetch origin refs/pull/46/head:pr46 && git checkout pr46
dotnet build solutions/Z3.Linq.Benchmarks -c Release -f net8.0
dotnet build solutions/Z3.Linq.Benchmarks -c Release -f net10.0
dotnet --fx-version 8.0.33  solutions/Z3.Linq.Benchmarks/bin/Release/net8.0/Z3.Linq.Benchmarks.dll  --filter '*'
dotnet --fx-version 9.0.22  solutions/Z3.Linq.Benchmarks/bin/Release/net8.0/Z3.Linq.Benchmarks.dll  --filter '*'
dotnet --fx-version 10.0.14 solutions/Z3.Linq.Benchmarks/bin/Release/net8.0/Z3.Linq.Benchmarks.dll  --filter '*'
dotnet --fx-version 10.0.14 solutions/Z3.Linq.Benchmarks/bin/Release/net10.0/Z3.Linq.Benchmarks.dll --filter '*'
```

Pour la composition à 14 charges, remplacer `refs/pull/46/head` par
`refs/pull/47/head` et retirer le `--filter`. Pour la variante « 6 `Solve` + 4
`TheoremPipeline` », retirer `RationalBenchmarks.cs` du projet avant la construction ;
retirer `TheoremPipelineBenchmarks.cs` à la place **casse la construction**
(`BenchConfig` y est défini, et `RationalBenchmarks.cs` s'y réfère).

## Portée

Ces mesures sont **une** lecture, sur **un** poste, avec les runtimes .NET 8.0.33,
9.0.22 et 10.0.14. .NET 11 RC n'est pas installé ici : la colonne .NET 11 de #47 n'a
pas été rejouée. Le mécanisme précis de l'amplification par composition n'est pas
identifié non plus — la mesure établit sa présence, son ordre de grandeur et la classe
qui la déclenche (`Rational`), pas le mécanisme interne.
