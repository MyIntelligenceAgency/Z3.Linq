# Baseline .NET 9.0 — Z3.Linq.Benchmarks

Mesures de référence de l'API publique de Z3.Linq sur .NET 9, point de départ des
comparaisons .NET 10 / .NET 11 (EPIC gains runtime — CoursIA issues 18770 et 18695,
digest Stephen Toub 2026-09-15). Suite de la tranche MetaGeneticSharp (PR amont
jsboige/MetaGeneticSharp#58), même protocole.

## Environnement

| Élément | Valeur |
| --- | --- |
| Date | 2026-10-02 |
| OS | Windows 11 Pro (10.0.26300) |
| Runtime | .NET 9.0.20 (9.0.2026.41315), X64 RyuJIT AVX2 |
| SDK (build) | 10.0.204 |
| BenchmarkDotNet | 0.14.0 |
| Toolchain | InProcessEmitToolchain, `InvocationCount=1`, `UnrollFactor=1`, 5 warmup / 15 itérations |
| Z3 | Microsoft.Z3 4.12.2 (lib z3 native x64), paramètres par défaut, monothread |
| Machine | poste de travail portable (laptop), alimentation secteur |

## Charges mesurées

Chaque itération = le cycle complet consommé par un carnet : `new Z3Context()` +
construction du théorème LINQ (traduction expressions → AST Z3) + `Solve()` +
extraction du modèle + disposition du contexte. Z3 déterministe (mêmes contraintes,
même modèle à chaque itération).

| Benchmark | Charge |
| --- | --- |
| `Linear_TechEd` (baseline) | 3 variables entières, 3 contraintes linéaires (exemple Bart De Smet, TechEd 2012) |
| `SendMoreMoney` | cryptarithme SEND + MORE = MONEY : 8 lettres, bornes, `Z3Methods.Distinct`, égalité arithmétique 5 chiffres |
| `MiniSudoku4x4` | 16 cellules : domaine 1..4, 12 clauses `Distinct` (lignes/colonnes/blocs), 4 indices donnés |

Les environnements de théorème sont des **records** (`Lin3`, `SendMoreMoneyEnv`,
`Sudoku4Env`) — voir « Constat API » ci-dessous.

## Résultats

| Method | Mean | Error | StdDev | Ratio | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Linear_TechEd | 36.20 ms | 3.249 ms | 3.039 ms | 1.01 | 13.14 KB | 1.00 |
| SendMoreMoney | 57.26 ms | 10.276 ms | 9.612 ms | 1.59 | 49.48 KB | 3.77 |
| MiniSudoku4x4 | 46.59 ms | 2.787 ms | 2.607 ms | 1.30 | 104.54 KB | 7.96 |

Lecture hot-path (axes Toub visés) :

- **Le temps est dominé par le natif Z3, pas par le managé** : 36 ms pour un théorème
  trivial (3 variables) dont 13 KB d'allocations managées seulement — l'essentiel est
  l'initialisation du contexte Z3 natif + la résolution. Le managé (traduction
  d'expressions, arbre LINQ → AST) est le second poste : il croît avec le nombre de
  clauses (49 KB pour 10 contraintes + Distinct, 105 KB pour 32 contraintes).
- **Scaling clauses** : x3,8 allocations de Linear → Sudoku (13 → 105 KB) pour x1,3
  temps — la traduction alloue, le solveur native domine asymptotiquement.
- **Impact runtime .NET** : la marge d'amélioration runtime se joue sur la partie
  traduction/extraction managée (allocations par nœud d'expression), identifiable par
  la colonne Allocated ; la partie native est insensible à la version du runtime.

## Protocole / reproduction

```bash
cd solutions/Z3.Linq.Benchmarks
dotnet run -c Release -- --filter '*'
```

- Configuration Release, InProcess (évite le spawn d'un process enfant par
  benchmark, bloqué par Windows Defender sur le poste de mesure — cf. tranche
  MetaGeneticSharp).
- Z3 monothread, paramètres par défaut, aucun hasard managé : résultats
  reproductibles à la variance du scheduler près.

## Constat API documenté (non contourné)

L'extraction de modèle sur **ValueTuple** lève `NotSupportedException` sur ce pin
(3b42bcc) : `Theorem.ConstructFromModel` — « Type ``ValueTuple`3`` has no parameterless
constructor and no constructor whose parameters match the theorem environment
members (Item1, Item2, Item3) » — alors que `Z3.Linq.Demo/Program.cs` montre des
exemples ValueTuple. Vérifié : **0 couverture** de `NewTheorem<(` ValueTuple dans
`Z3.Linq.Tests` (grep), et aucune branche ValueTuple dans `ConstructFromModel` — le
chemin montré par le Demo n'est pas testé. Les environnements de ces benchmarks
utilisent donc des **records** (voie supportée, reconstruction par constructeur
primaire — backlog DSL B9). Le constat est signalé ici sans être fixé (hors
périmètre du banc) ; à tracker en issue dédiée côté fork si confirmé voulu.

## Limites connues (honnêteté des mesures)

- `MinIterationTime` : chaque itération dure 31–51 ms (< 100 ms recommandé par
  BDN). Des instances plus lourdes (Sudoku 9x9) sortiraient du temps de carnet
  pédagogique ; assumé.
- Error ≈ 6–18 % du Mean : poste partagé (GUI active), ShortRun borné. Suffisant
  pour détecter un delta runtime majeur ; ne pas trancher un delta < 20 % sur ces
  chiffres seuls.
- Une seule machine ; la comparaison .NET 10/11 devra rejouer sur le MÊME poste,
  même toolchain, pour être comparable.
