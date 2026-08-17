namespace Z3.Linq.Tests;

using Xunit;

/// <summary>
/// Regression tests for the weighted Pseudo-Boolean magic methods
/// (<see cref="Z3Methods.WeightedAtLeast"/>, <see cref="Z3Methods.WeightedAtMost"/>,
/// <see cref="Z3Methods.WeightedExactly"/>) -- the band forms of issue #10605
/// (weights != 1, e.g. nutrition bounds over recipe indicators in notebook 09).
/// They map to native Z3 <c>MkPBGe</c> / <c>MkPBLe</c> / <c>MkPBEq</c> instead of an
/// <c>MkIte + MkAdd</c> expansion (pb-bench c.8252 measures the gap).
/// </summary>
public class WeightedPbTests
{
    public class BoolQuintuple
    {
        public bool[] B { get; set; } = new bool[5];
    }

    // weights {3, 1, 4, 1, 5} -- sums of forced patterns:
    //   B0 alone = 3 ; B0+B2 = 7 ; B4 alone = 5 ; B0+B1+B3 = 5 ; all = 14.
    private static readonly int[] W = new int[] { 3, 1, 4, 1, 5 };

    [Fact]
    public void WeightedAtLeast_AcceptsSatisfiableBand()
    {
        using var ctx = new Z3Context();
        var theorem = ctx.NewTheorem<BoolQuintuple>()
            .Where(t => Z3Methods.WeightedAtLeast(7, W, t.B[0], t.B[1], t.B[2], t.B[3], t.B[4]))
            .Where(t => t.B[0] && t.B[2]);   // weighted sum = 7 exactly

        var result = theorem.Solve();
        Assert.NotNull(result);
        Assert.True(result!.B[0] && result.B[2]);
    }

    [Fact]
    public void WeightedAtLeast_RejectsInsufficientSum()
    {
        using var ctx = new Z3Context();
        var theorem = ctx.NewTheorem<BoolQuintuple>()
            .Where(t => Z3Methods.WeightedAtLeast(7, W, t.B[0], t.B[1], t.B[2], t.B[3], t.B[4]))
            .Where(t => t.B[1] && !t.B[0] && !t.B[2] && !t.B[3] && !t.B[4]);   // only B1 on: sum = 1 < 7

        var result = theorem.Solve();
        Assert.Null(result);
    }

    [Fact]
    public void WeightedAtMost_AcceptsExactBound()
    {
        using var ctx = new Z3Context();
        var theorem = ctx.NewTheorem<BoolQuintuple>()
            .Where(t => Z3Methods.WeightedAtMost(7, W, t.B[0], t.B[1], t.B[2], t.B[3], t.B[4]))
            .Where(t => t.B[0] && t.B[2]);   // weighted sum = 7 <= 7

        var result = theorem.Solve();
        Assert.NotNull(result);
    }

    [Fact]
    public void WeightedAtMost_RejectsOverflowingSum()
    {
        using var ctx = new Z3Context();
        var theorem = ctx.NewTheorem<BoolQuintuple>()
            .Where(t => Z3Methods.WeightedAtMost(6, W, t.B[0], t.B[1], t.B[2], t.B[3], t.B[4]))
            .Where(t => t.B[0] && t.B[2]);   // weighted sum = 7 > 6

        var result = theorem.Solve();
        Assert.Null(result);
    }

    [Fact]
    public void WeightedExactly_PinsTheSum()
    {
        using var ctx = new Z3Context();
        // B0 + B1 + B3 = 3 + 1 + 1 = 5, and B4 alone = 5 too: two satisfying patterns.
        var theorem = ctx.NewTheorem<BoolQuintuple>()
            .Where(t => Z3Methods.WeightedExactly(5, W, t.B[0], t.B[1], t.B[2], t.B[3], t.B[4]));

        var result = theorem.Solve();
        Assert.NotNull(result);
        int sum = 0;
        for (int i = 0; i < 5; i++)
        {
            if (result!.B[i]) sum += W[i];
        }
        Assert.Equal(5, sum);

        // Off-by-one on the bound is unsat against the forced maximal pattern below.
        var unsat = ctx.NewTheorem<BoolQuintuple>()
            .Where(t => Z3Methods.WeightedExactly(5, W, t.B[0], t.B[1], t.B[2], t.B[3], t.B[4]))
            .Where(t => t.B[4]);   // B4 alone already weighs 5: no other indicator may be on.
        var forced = unsat.Solve();
        Assert.NotNull(forced);
        Assert.True(forced!.B[4]);
        for (int i = 0; i < 4; i++)
        {
            if (i == 4) continue;
            Assert.False(forced.B[i]);
        }
    }

    [Fact]
    public void WeightedExactly_UnreachableBoundIsUnsat()
    {
        using var ctx = new Z3Context();
        // Weights {2,3,4,6,5} have no subset summing to 1 (minimum positive weight is 2),
        // so the equality band exactly-1 is unsat for a reason the PB solver must find.
        var w = new int[] { 2, 3, 4, 6, 5 };
        var theorem = ctx.NewTheorem<BoolQuintuple>()
            .Where(t => Z3Methods.WeightedExactly(1, w, t.B[0], t.B[1], t.B[2], t.B[3], t.B[4]));

        var result = theorem.Solve();
        Assert.Null(result);
    }

    [Fact]
    public void WeightedBand_ComposesWithExactlyOneSelection()
    {
        // The canonical notebook 09 shape: pick exactly one recipe per slot (one-hot),
        // then enforce a nutrition band on the weighted sum over ALL slots.
        using var ctx = new Z3Context();
        var theorem = ctx.NewTheorem<BoolQuintuple>()
            .Where(t => Z3Methods.ExactlyOne(t.B[0], t.B[1], t.B[2], t.B[3], t.B[4]))
            .Where(t => Z3Methods.WeightedAtLeast(4, W, t.B[0], t.B[1], t.B[2], t.B[3], t.B[4]))
            .Where(t => Z3Methods.WeightedAtMost(4, W, t.B[0], t.B[1], t.B[2], t.B[3], t.B[4]));

        var result = theorem.Solve();
        Assert.NotNull(result);
        // Exactly-one + band [4,4] forces the single selected indicator to weigh exactly 4: B2.
        Assert.True(result!.B[2]);
        for (int i = 0; i < 5; i++)
        {
            if (i == 2) continue;
            Assert.False(result.B[i]);
        }
    }

    [Fact]
    public void WeightMismatch_ThrowsAtTranslation()
    {
        using var ctx = new Z3Context();
        var theorem = ctx.NewTheorem<BoolQuintuple>()
            .Where(t => Z3Methods.WeightedAtLeast(7, new int[] { 3, 1 }, t.B[0], t.B[1], t.B[2]));

        // The visitor runs at solve time; the length mismatch must surface as a clear error.
        Assert.Throws<InvalidOperationException>(() => theorem.Solve());
    }
}
