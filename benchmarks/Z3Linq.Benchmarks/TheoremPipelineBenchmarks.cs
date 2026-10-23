using BenchmarkDotNet.Attributes;
using Z3.Linq;

namespace Z3Linq.Benchmarks;

/// <summary>
/// Bancs du pipeline public LINQ vers Z3 (issue CoursIA #18770, fille de l'EPIC #18695).
/// Chaque itération paie le pipeline complet qu'un appelant paie : création du contexte,
/// réécriture de l'arbre d'expression, construction des AST Z3, solve et extraction du modèle.
///
/// Les théorèmes utilisent les deux chemins de matérialisation **fonctionnels à la tête
/// mesurée** : types anonymes (démontrés par le démo du dépôt) et <see cref="Symbols{T1,T2,T3,T4,T5}"/>
/// (prouvé par ce banc). Le chemin ValueTuple du démo lève
/// <c>NotSupportedException</c> à la matérialisation (voir issue amont) — il n'est pas
/// mesurable en l'état.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(BenchConfig))]
public class TheoremPipelineBenchmarks
{
    /// <summary>Théorème booléen trivial : plancher de coût du pipeline (réécriture + AST + solve).</summary>
    [Benchmark(Description = "Solve bool pair (pipeline floor)")]
    public object SolveBoolPair()
    {
        using var ctx = new Z3Context();
        var theorem = from t in ctx.NewTheorem(new { x = default(bool), y = default(bool) })
                      where t.x || t.y
                      where !t.x || !t.y
                      select t;
        return theorem.Solve()!;
    }

    /// <summary>Entiers anonymes avec contraintes linéaires : chemin noyau LINQ vers Z3.</summary>
    [Benchmark(Description = "Solve ints (linear constraints)")]
    public object SolveInts()
    {
        using var ctx = new Z3Context();
        var theorem = from t in ctx.NewTheorem(new { x = default(int), y = default(int), z = default(int) })
                      where t.x + t.y + t.z == 30
                      where t.x >= 2 * t.y
                      where t.z - t.x <= 4
                      where t.x >= 0 && t.x <= 20
                      where t.y >= 0 && t.y <= 20
                      where t.z >= 0 && t.z <= 20
                      select t;
        return theorem.Solve()!;
    }

    /// <summary>
    /// Contraintes réelles (problème d'achat pétrolier du démo) : chemin arithmétique réel.
    /// Via <see cref="Symbols{T1,T2}"/> (X1 = vz, X2 = sa) — la matérialisation du type
    /// anonyme ne supporte que bool/int à la tête mesurée (Theorem.cs:1128).
    /// </summary>
    [Benchmark(Description = "Solve reals (oil purchase)")]
    public Symbols<double, double> SolveOilPurchase()
    {
        using var ctx = new Z3Context();
        var theorem = from t in ctx.NewTheorem<Symbols<double, double>>()
                      where 0.3 * t.X2 + 0.4 * t.X1 >= 1900
                      where 0.4 * t.X2 + 0.2 * t.X1 >= 1500
                      where 0.2 * t.X2 + 0.3 * t.X1 >= 500
                      where 0 <= t.X2 && t.X2 <= 9000
                      where 0 <= t.X1 && t.X1 <= 6000
                      where t.X2 + t.X1 <= 11000
                      select t;
        return theorem.Solve();
    }

    /// <summary>
    /// Théorème à 5 variables via <see cref="Symbols{T1,T2,T3,T4,T5}"/> (membres X1..X5) :
    /// croissance du coût de réécriture avec l'arité, chemin de matérialisation distinct
    /// du type anonyme. Contraintes reprises de la forme démontrée du démo.
    /// </summary>
    [Benchmark(Description = "Solve Symbols 5 vars")]
    public Symbols<int, int, int, int, int> SolveFiveSymbols()
    {
        using var ctx = new Z3Context();
        var theorem = from t in ctx.NewTheorem<Symbols<int, int, int, int, int>>()
                      where t.X1 - t.X2 >= 1
                      where t.X1 - t.X2 <= 3
                      where t.X1 == (2 * t.X3) + t.X5
                      where t.X3 == t.X5
                      where t.X2 == 6 * t.X4
                      select t;
        return theorem.Solve();
    }
}
