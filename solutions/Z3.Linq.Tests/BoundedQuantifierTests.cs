namespace Z3.Linq.Tests;

using System;
using System.Linq;

using Xunit;

/// <summary>
/// Tests for bounded quantifiers <see cref="Z3Methods.ForAll{T}"/> / <see cref="Z3Methods.Exists{T}"/>
/// (DSL backlog item B8, #4616).
///
/// These are finite-domain quantifiers: the domain is evaluated in the host and the predicate is
/// unrolled into a conjunction (<c>ForAll</c> -> <c>MkAnd</c>) or disjunction (<c>Exists</c> ->
/// <c>MkOr</c>) over each element. They let a single <c>.Where(...)</c> express what previously
/// required a C# loop emitting one <c>.Where</c> per index (cf. CollectionHandlingTests, which
/// unrolls cell constraints by hand). They are NOT true SMT quantifiers over an unbounded sort.
/// </summary>
public class BoundedQuantifierTests
{
    public class ArrayEnv
    {
        public int[] Values { get; set; } = new int[4];
    }

    [Fact]
    public void ForAll_BoundsEveryElement()
    {
        // A single ForAll constrains all four array cells to [1, 9].
        using var ctx = new Z3Context();
        var result = ctx.NewTheorem<ArrayEnv>()
            .Where(e => Z3Methods.ForAll(Enumerable.Range(0, 4), i => e.Values[i] >= 1 && e.Values[i] <= 9))
            .Solve();

        Assert.NotNull(result);
        Assert.Equal(4, result!.Values.Length);
        Assert.All(result.Values, v => Assert.InRange(v, 1, 9));
    }

    [Fact]
    public void Exists_RequiresAtLeastOneWitness()
    {
        // Every element in [0, 5], and at least one element must equal exactly 5.
        using var ctx = new Z3Context();
        var result = ctx.NewTheorem<ArrayEnv>()
            .Where(e => Z3Methods.ForAll(Enumerable.Range(0, 4), i => e.Values[i] >= 0 && e.Values[i] <= 5))
            .Where(e => Z3Methods.Exists(Enumerable.Range(0, 4), i => e.Values[i] == 5))
            .Solve();

        Assert.NotNull(result);
        Assert.All(result!.Values, v => Assert.InRange(v, 0, 5));
        Assert.Contains(5, result.Values);
    }

    [Fact]
    public void ForAll_CombinesWithExists_ForExactlyConstrainedShape()
    {
        // All in [0, 10]; some element is 0 and some element is 10.
        using var ctx = new Z3Context();
        var result = ctx.NewTheorem<ArrayEnv>()
            .Where(e => Z3Methods.ForAll(Enumerable.Range(0, 4), i => e.Values[i] >= 0 && e.Values[i] <= 10))
            .Where(e => Z3Methods.Exists(Enumerable.Range(0, 4), i => e.Values[i] == 0))
            .Where(e => Z3Methods.Exists(Enumerable.Range(0, 4), i => e.Values[i] == 10))
            .Solve();

        Assert.NotNull(result);
        Assert.All(result!.Values, v => Assert.InRange(v, 0, 10));
        Assert.Contains(0, result.Values);
        Assert.Contains(10, result.Values);
    }

    [Fact]
    public void ForAll_EmptyDomain_IsVacuouslyTrue()
    {
        // The predicate is a contradiction (v > v); over a non-empty domain this would be UNSAT.
        // Over the empty domain the universal quantifier must be vacuously true, so the theorem,
        // constrained only by a satisfiable bound, stays SAT.
        using var ctx = new Z3Context();
        var result = ctx.NewTheorem<ArrayEnv>()
            .Where(e => Z3Methods.ForAll(Array.Empty<int>(), i => e.Values[i] > e.Values[i]))
            .Where(e => e.Values[0] == 7)
            .Solve();

        Assert.NotNull(result);
        Assert.Equal(7, result!.Values[0]);
    }

    [Fact]
    public void Exists_EmptyDomain_IsVacuouslyFalse()
    {
        // An existential over the empty domain is false, making the theorem unsatisfiable.
        using var ctx = new Z3Context();
        var result = ctx.NewTheorem<ArrayEnv>()
            .Where(e => Z3Methods.Exists(Array.Empty<int>(), i => e.Values[i] == 0))
            .Solve();

        Assert.Null(result);
    }
}
