// Hot paths visees (digest Toub .NET 11) : traduction des arborescences d'expressions
// LINQ vers les AST Z3 (allocations par noeud), resolution SMT, extraction des modeles.
// Tout passe par l'API publique (Z3Context.NewTheorem + Where + Solve).
//
// Environnements = records (voie supportee par ConstructFromModel, backlog DSL B9) :
// l'extraction ValueTuple leve NotSupportedException sur ce pin (3b42bcc) — constat
// documente dans benchmarks/BASELINE-net9.0.md, non contourne.
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using Z3.Linq;

namespace Z3.Linq.Benchmarks;

public record Lin3
{
    public int X { get; init; }
    public int Y { get; init; }
    public int Z { get; init; }
}

public record SendMoreMoneyEnv
{
    public int S { get; init; }
    public int E { get; init; }
    public int N { get; init; }
    public int D { get; init; }
    public int M { get; init; }
    public int O { get; init; }
    public int R { get; init; }
    public int Y { get; init; }
}

public record Sudoku4Env
{
    public int A { get; init; }
    public int B { get; init; }
    public int C { get; init; }
    public int D { get; init; }
    public int E { get; init; }
    public int F { get; init; }
    public int G { get; init; }
    public int H { get; init; }
    public int I { get; init; }
    public int J { get; init; }
    public int K { get; init; }
    public int L { get; init; }
    public int M { get; init; }
    public int N { get; init; }
    public int O { get; init; }
    public int P { get; init; }
}

// InProcess : evite le spawn d'un process enfant par benchmark (bloque par Defender
// sur certains postes Windows), et garde la mesure dans le meme runtime (cf. tranche
// MetaGeneticSharp, EPIC CoursIA 18770).
[MemoryDiagnoser]
[Config(typeof(InProcessShortConfig))]
public class TheoremBenchmarks
{
    private class InProcessShortConfig : ManualConfig
    {
        public InProcessShortConfig()
            => AddJob(Job.ShortRun
                .WithToolchain(InProcessEmitToolchain.Instance)
                .WithInvocationCount(1)
                .WithUnrollFactor(1)
                .WithWarmupCount(5)
                .WithIterationCount(15));
    }

    // --- Lineaire trivial (Bart De Smet, TechEd 2012) : traduction minimale ---
    [Benchmark(Baseline = true)]
    public Lin3 Linear_TechEd()
    {
        using var ctx = new Z3Context();
        return (from t in ctx.NewTheorem(new Lin3())
                where t.X - t.Y >= 1
                where t.X - t.Y <= 3
                where t.X == (2 * t.Z) + t.Y
                select t).Solve();
    }

    // --- Cryptarithme SEND + MORE = MONEY : Distinct 8 lettres + arithmetique lourde ---
    [Benchmark]
    public SendMoreMoneyEnv SendMoreMoney()
    {
        using var ctx = new Z3Context();
        return (from t in ctx.NewTheorem(new SendMoreMoneyEnv())
                where t.S >= 0 && t.S <= 9
                where t.E >= 0 && t.E <= 9
                where t.N >= 0 && t.N <= 9
                where t.D >= 0 && t.D <= 9
                where t.M >= 1 && t.M <= 9
                where t.O >= 0 && t.O <= 9
                where t.R >= 0 && t.R <= 9
                where t.Y >= 0 && t.Y <= 9
                where Z3Methods.Distinct(t.S, t.E, t.N, t.D, t.M, t.O, t.R, t.Y)
                where 1000 * t.S + 100 * t.E + 10 * t.N + t.D
                    + 1000 * t.M + 100 * t.O + 10 * t.R + t.E
                    == 10000 * t.M + 1000 * t.O + 100 * t.N + 10 * t.E + t.Y
                select t).Solve();
    }

    // --- Mini-Sudoku 4x4 : 16 variables, 12 clauses Distinct + 4 indices donnes ---
    // Grille (0 = vide) ; indices non conflictuels (lignes/colonnes/blocs disjoints).
    //   . 3 . .  |  . . . 2  |  . . 4 .  |  1 . . .
    [Benchmark]
    public Sudoku4Env MiniSudoku4x4()
    {
        using var ctx = new Z3Context();
        return (from t in ctx.NewTheorem(new Sudoku4Env())
                // domaine 1..4 pour chaque cellule
                where (t.A >= 1 && t.A <= 4) && (t.B >= 1 && t.B <= 4) && (t.C >= 1 && t.C <= 4) && (t.D >= 1 && t.D <= 4)
                where (t.E >= 1 && t.E <= 4) && (t.F >= 1 && t.F <= 4) && (t.G >= 1 && t.G <= 4) && (t.H >= 1 && t.H <= 4)
                where (t.I >= 1 && t.I <= 4) && (t.J >= 1 && t.J <= 4) && (t.K >= 1 && t.K <= 4) && (t.L >= 1 && t.L <= 4)
                where (t.M >= 1 && t.M <= 4) && (t.N >= 1 && t.N <= 4) && (t.O >= 1 && t.O <= 4) && (t.P >= 1 && t.P <= 4)
                // lignes
                where Z3Methods.Distinct(t.A, t.B, t.C, t.D)
                where Z3Methods.Distinct(t.E, t.F, t.G, t.H)
                where Z3Methods.Distinct(t.I, t.J, t.K, t.L)
                where Z3Methods.Distinct(t.M, t.N, t.O, t.P)
                // colonnes
                where Z3Methods.Distinct(t.A, t.E, t.I, t.M)
                where Z3Methods.Distinct(t.B, t.F, t.J, t.N)
                where Z3Methods.Distinct(t.C, t.G, t.K, t.O)
                where Z3Methods.Distinct(t.D, t.H, t.L, t.P)
                // blocs 2x2
                where Z3Methods.Distinct(t.A, t.B, t.E, t.F)
                where Z3Methods.Distinct(t.C, t.D, t.G, t.H)
                where Z3Methods.Distinct(t.I, t.J, t.M, t.N)
                where Z3Methods.Distinct(t.K, t.L, t.O, t.P)
                // indices donnes
                where t.B == 3
                where t.H == 2
                where t.K == 4
                where t.M == 1
                select t).Solve();
    }
}
