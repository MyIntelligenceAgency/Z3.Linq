namespace Z3.Linq.Tests;

using Xunit;

/// <summary>
/// Regression tests for the variadic <c>Z3Methods.Sum</c> magic method (DSL backlog item B3, #4616).
///
/// Before B3, the visitor only handled the binary <c>+</c> (pairwise <c>MkAdd</c>); summing an
/// arbitrary number of theorem-collection elements required manual per-element unrolling (as the
/// Sudoku row/column distinctness did, or a sum-of-indicators aggregation). B3 generalised the
/// <c>Distinct</c> machinery into a shared variadic path that also handles <c>Sum</c> → <c>MkAdd</c>.
/// </summary>
public class SumVariadicTests
{
    /// <summary>Env with a 3-element array, exercised in Constants mode.</summary>
    public class Triple
    {
        public int[] Values { get; set; } = new int[3];
    }

    private static Theorem<Triple> BuildInRange(Z3Context ctx)
    {
        var theorem = ctx.NewTheorem<Triple>();
        for (int i = 0; i < 3; i++)
        {
            int i1 = i;
            theorem = theorem.Where(t => t.Values[i1] >= 1 && t.Values[i1] <= 9);
        }
        return theorem;
    }

    [Theory]
    [InlineData(CollectionHandling.Array)]
    [InlineData(CollectionHandling.Constants)]
    public void Sum_OfCollection_EqualsTarget(CollectionHandling mode)
    {
        // Sum(Values) == 10 with each in [1,9]. Many solutions exist; we assert the invariant.
        using var ctx = new Z3Context { DefaultCollectionHandling = mode };
        var theorem = BuildInRange(ctx)
            .Where(t => Z3Methods.Sum(t.Values[0], t.Values[1], t.Values[2]) == 10);

        var result = theorem.Solve();

        Assert.NotNull(result);
        var sum = result!.Values[0] + result.Values[1] + result.Values[2];
        Assert.Equal(10, sum);
    }

    [Theory]
    [InlineData(CollectionHandling.Array)]
    [InlineData(CollectionHandling.Constants)]
    public void Sum_RespectsUpperBoundAndUnsatAboveIt(CollectionHandling mode)
    {
        // Each value in [1,3] => max sum = 9. Sum == 9 is SAT; Sum == 10 is UNSAT.
        using var ctx = new Z3Context { DefaultCollectionHandling = mode };
        var theorem = BuildCap3(ctx);

        var sat = theorem.Where(t => Z3Methods.Sum(t.Values[0], t.Values[1], t.Values[2]) == 9).Solve();
        Assert.NotNull(sat);

        var unsat = theorem.Where(t => Z3Methods.Sum(t.Values[0], t.Values[1], t.Values[2]) == 10).Solve();
        Assert.Null(unsat);
    }

    private static Theorem<Triple> BuildCap3(Z3Context ctx)
    {
        var theorem = ctx.NewTheorem<Triple>();
        for (int i = 0; i < 3; i++)
        {
            int i1 = i;
            theorem = theorem.Where(t => t.Values[i1] >= 1 && t.Values[i1] <= 3);
        }
        return theorem;
    }

    [Fact]
    public void Sum_WithConditionalIndicator_FormsPenaltyTerm()
    {
        // The canonical use case unlocked by B2+B3 together: sum of indicators
        //   penalty = sum_i (cond_i ? 1 : 0), here simplified to a fixed-count boolean array.
        // 3 booleans, exactly 2 must be true => sum of indicators == 2.
        using var ctx = new Z3Context();
        var theorem = ctx.NewTheorem<BoolTriple>()
            .Where(t => Z3Methods.Sum(t.B[0] ? 1 : 0, t.B[1] ? 1 : 0, t.B[2] ? 1 : 0) == 2);

        var result = theorem.Solve();

        Assert.NotNull(result);
        var trueCount = System.Linq.Enumerable.Range(0, 3).Count(i => result!.B[i]);
        Assert.Equal(2, trueCount);
    }

    public class BoolTriple
    {
        public bool[] B { get; set; } = new bool[3];
    }
}
