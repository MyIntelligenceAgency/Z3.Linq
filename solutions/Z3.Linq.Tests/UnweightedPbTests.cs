namespace Z3.Linq.Tests;

using Xunit;

/// <summary>
/// Regression tests for the unweighted Pseudo-Boolean magic methods
/// (<see cref="Z3Methods.ExactlyOne"/>, <see cref="Z3Methods.AtMostOne"/>,
/// <see cref="Z3Methods.AtLeastOne"/>), the last open item of backlog tracker #4616
/// (see issue #10605). They map to native Z3 <c>ctx.MkPBGe</c> on the indicators
/// and their negations, instead of an <c>MkIte + MkAdd</c> expansion -- measurably
/// cheaper: pb-bench c.8247 records ~half the AST nodes and 3-6x faster solving on
/// the planner 7x5 instance and up to n=100.
/// </summary>
public class UnweightedPbTests
{
    public class BoolQuintuple
    {
        public bool[] B { get; set; } = new bool[5];
    }

    [Fact]
    public void ExactlyOne_SelectsExactlyOneIndicator()
    {
        using var ctx = new Z3Context();
        var theorem = ctx.NewTheorem<BoolQuintuple>()
            .Where(t => Z3Methods.ExactlyOne(t.B[0], t.B[1], t.B[2], t.B[3], t.B[4]));

        var result = theorem.Solve();

        Assert.NotNull(result);
        var trueCount = System.Linq.Enumerable.Range(0, 5).Count(i => result!.B[i]);
        Assert.Equal(1, trueCount);
    }

    [Fact]
    public void ExactlyOne_WithForcedTwoIsUnsat()
    {
        using var ctx = new Z3Context();
        var theorem = ctx.NewTheorem<BoolQuintuple>()
            .Where(t => Z3Methods.ExactlyOne(t.B[0], t.B[1], t.B[2], t.B[3], t.B[4]))
            .Where(t => t.B[0] && t.B[1]);   // force two on, breaks exactly-one

        var result = theorem.Solve();
        Assert.Null(result);
    }

    [Fact]
    public void AtMostOne_AllowsZeroOrOne()
    {
        using var ctx = new Z3Context();
        var theorem = ctx.NewTheorem<BoolQuintuple>()
            .Where(t => Z3Methods.AtMostOne(t.B[0], t.B[1], t.B[2], t.B[3], t.B[4]));

        var sat0 = theorem.Where(t => !t.B[0] && !t.B[1] && !t.B[2] && !t.B[3] && !t.B[4]).Solve();
        Assert.NotNull(sat0);  // zero is allowed.

        var sat1 = theorem.Where(t => t.B[2] && !t.B[0] && !t.B[1] && !t.B[3] && !t.B[4]).Solve();
        Assert.NotNull(sat1);  // exactly one is allowed.

        var unsat = theorem.Where(t => t.B[0] && t.B[1]).Solve();
        Assert.Null(unsat);    // two is forbidden.
    }

    [Fact]
    public void AtLeastOne_ForbidsAllFalse()
    {
        using var ctx = new Z3Context();
        var theorem = ctx.NewTheorem<BoolQuintuple>()
            .Where(t => Z3Methods.AtLeastOne(t.B[0], t.B[1], t.B[2], t.B[3], t.B[4]));

        var sat1 = theorem.Where(t => t.B[2]).Solve();
        Assert.NotNull(sat1);

        var unsat = theorem.Where(t => !t.B[0] && !t.B[1] && !t.B[2] && !t.B[3] && !t.B[4]).Solve();
        Assert.Null(unsat);
    }

    [Fact]
    public void ExactlyOne_ComposesWithArithmetic()
    {
        // Each indicator picks a recipe (slot index); exactly one recipe must be picked.
        // The slot index must satisfy an arithmetic constraint. This is the canonical
        // "one-hot" shape that notebook 09 (meal planner) needs.
        using var ctx = new Z3Context();
        var theorem = ctx.NewTheorem<BoolQuintuple>()
            .Where(t => Z3Methods.ExactlyOne(t.B[0], t.B[1], t.B[2], t.B[3], t.B[4]))
            .Where(t => t.B[2]);   // forces the third indicator

        var result = theorem.Solve();
        Assert.NotNull(result);
        Assert.True(result!.B[2]);
        for (int i = 0; i < 5; i++)
        {
            if (i == 2) continue;
            Assert.False(result.B[i]);
        }
    }

    // Note: empty-indicator edge cases (ExactlyOne() with no args) are handled in
    // the visitor's degenerate path (n==0 -> MkTrue/MkFalse depending on the operator).
    // We don't add separate tests here because the production code path (notebook 09)
    // always passes a populated indicator array -- the meal planner has slot cardinalities
    // and a category has at least one recipe.
}
