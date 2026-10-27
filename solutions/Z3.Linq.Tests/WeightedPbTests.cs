namespace Z3.Linq.Tests;

#pragma warning disable CS1591

/// <summary>
/// The weighted pseudo-Boolean magic methods: <see cref="Z3Methods.WeightedAtLeast"/>,
/// <see cref="Z3Methods.WeightedAtMost"/> and <see cref="Z3Methods.WeightedExactly"/>.
/// </summary>
/// <remarks>
/// The unweighted trio covers equal contributions; these cover a group whose options carry unequal
/// contributions, stated as a lower bound, an upper bound, or an exact value over the weighted sum.
/// They map onto native <c>MkPBGe</c> / <c>MkPBLe</c> / <c>MkPBEq</c>, so Z3's pseudo-Boolean
/// propagation applies instead of an <c>Ite</c>-and-add expansion.
/// </remarks>
[TestClass]
public class WeightedPbTests
{
    /// <summary>Five flags, the group the bands are stated over.</summary>
    public class BoolQuintuple
    {
        /// <summary>Gets or sets the five flags.</summary>
        public bool[] B { get; set; } = new bool[5];
    }

    // Weights {3, 1, 4, 1, 5}: the sums of the forced patterns below are 1, 3, 5, 7 and 14, so a
    // bound can be placed on either side of a reachable value rather than only in open space.
    private static readonly int[] W = [3, 1, 4, 1, 5];

    [TestMethod]
    public void WeightedAtLeast_AcceptsSatisfiableBand()
    {
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<BoolQuintuple>()
            .Where(t => Z3Methods.WeightedAtLeast(7, W, t.B[0], t.B[1], t.B[2], t.B[3], t.B[4]))

            // B0 + B2 weighs exactly 7, so the bound is met at equality.
            .Where(t => t.B[0] && t.B[2])
            .Solve();

        solution.ShouldNotBeNull();
        solution!.B[0].ShouldBeTrue();
        solution.B[2].ShouldBeTrue();
    }

    [TestMethod]
    public void WeightedAtLeast_RejectsInsufficientSum()
    {
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<BoolQuintuple>()
            .Where(t => Z3Methods.WeightedAtLeast(7, W, t.B[0], t.B[1], t.B[2], t.B[3], t.B[4]))

            // Only B1 is on, and it weighs 1, well under the bound.
            .Where(t => t.B[1] && !t.B[0] && !t.B[2] && !t.B[3] && !t.B[4])
            .Solve();

        solution.ShouldBeNull();
    }

    [TestMethod]
    public void WeightedAtMost_AcceptsExactBound()
    {
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<BoolQuintuple>()
            .Where(t => Z3Methods.WeightedAtMost(7, W, t.B[0], t.B[1], t.B[2], t.B[3], t.B[4]))
            .Where(t => t.B[0] && t.B[2])
            .Solve();

        solution.ShouldNotBeNull();
    }

    [TestMethod]
    public void WeightedAtMost_RejectsOverflowingSum()
    {
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<BoolQuintuple>()
            .Where(t => Z3Methods.WeightedAtMost(6, W, t.B[0], t.B[1], t.B[2], t.B[3], t.B[4]))

            // B0 + B2 weighs 7, one over the bound: the pair only fails together, which is what
            // tells the constraint apart from one that would reject B2 alone.
            .Where(t => t.B[0] && t.B[2])
            .Solve();

        solution.ShouldBeNull();
    }

    [TestMethod]
    public void WeightedExactly_PinsTheSum()
    {
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<BoolQuintuple>()
            .Where(t => Z3Methods.WeightedExactly(5, W, t.B[0], t.B[1], t.B[2], t.B[3], t.B[4]))
            .Solve();

        solution.ShouldNotBeNull();

        int sum = 0;
        for (int i = 0; i < solution.B.Length; i++)
        {
            if (solution.B[i])
            {
                sum += W[i];
            }
        }

        sum.ShouldBe(5);

        // B4 alone weighs 5, so pinning B4 leaves the equality satisfied with nothing else on.
        var pinned = ctx.NewTheorem<BoolQuintuple>()
            .Where(t => Z3Methods.WeightedExactly(5, W, t.B[0], t.B[1], t.B[2], t.B[3], t.B[4]))
            .Where(t => t.B[4])
            .Solve();

        pinned.ShouldNotBeNull();
        pinned!.B.Count(set => set).ShouldBe(1);
        pinned.B[4].ShouldBeTrue();
    }

    [TestMethod]
    public void WeightedExactly_UnreachableBoundIsUnsat()
    {
        // Weights {2, 3, 4, 6, 5} have no subset summing to 1, so equality with 1 has no solution -
        // a fact only the pseudo-Boolean solver finds, since no single indicator is at fault.
        using var ctx = new Z3Context();
        int[] weights = [2, 3, 4, 6, 5];

        var solution = ctx.NewTheorem<BoolQuintuple>()
            .Where(t => Z3Methods.WeightedExactly(1, weights, t.B[0], t.B[1], t.B[2], t.B[3], t.B[4]))
            .Solve();

        solution.ShouldBeNull();
    }

    [TestMethod]
    public void WeightedBand_ComposesWithExactlyOneSelection()
    {
        // The one-hot-plus-band shape: exactly one option is picked, and the weighted sum has to
        // land on 4 - which only B2 satisfies, so the solution is decided rather than merely found.
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<BoolQuintuple>()
            .Where(t => Z3Methods.ExactlyOne(t.B[0], t.B[1], t.B[2], t.B[3], t.B[4]))
            .Where(t => Z3Methods.WeightedAtLeast(4, W, t.B[0], t.B[1], t.B[2], t.B[3], t.B[4]))
            .Where(t => Z3Methods.WeightedAtMost(4, W, t.B[0], t.B[1], t.B[2], t.B[3], t.B[4]))
            .Solve();

        solution.ShouldNotBeNull();
        solution!.B.Count(set => set).ShouldBe(1);
        solution.B[2].ShouldBeTrue();
    }

    [TestMethod]
    public void WeightMismatch_ThrowsAtTranslation()
    {
        // Two weights for three indicators describes nothing, so it is refused rather than silently
        // paired up to whichever length is shorter.
        using var ctx = new Z3Context();
        int[] tooFew = [3, 1];

        var theorem = ctx.NewTheorem<BoolQuintuple>()
            .Where(t => Z3Methods.WeightedAtLeast(7, tooFew, t.B[0], t.B[1], t.B[2]));

        Should.Throw<InvalidOperationException>(() => theorem.Solve());
    }

    [TestMethod]
    public void WeightedBand_OverARuntimeGroup_IsTranslated()
    {
        // A band whose group size and bound are runtime values: the weights are computed, the
        // indicators are selected over the group, and neither is written out at the call site.
        using var ctx = new Z3Context();
        const int GroupSize = 5;
        int[] weights = Enumerable.Repeat(1, GroupSize).ToArray();

        var solution = ctx.NewTheorem<BoolQuintuple>()
            .Where(t => Z3Methods.WeightedExactly(
                GroupSize,
                weights,
                Enumerable.Range(0, GroupSize).Select(i => t.B[i]).ToArray()))
            .Solve();

        solution.ShouldNotBeNull();
        solution!.B.All(set => set).ShouldBeTrue();
    }
}
