namespace Z3.Linq.Tests;

#pragma warning disable CS1591

/// <summary>
/// The unweighted pseudo-Boolean magic methods: <see cref="Z3Methods.ExactlyOne"/>,
/// <see cref="Z3Methods.AtMostOne"/> and <see cref="Z3Methods.AtLeastOne"/>.
/// </summary>
/// <remarks>
/// They map onto native Z3 pseudo-Boolean constraints (<c>MkPBGe</c> over the indicators and their
/// negations) rather than onto an <c>Ite</c>-and-add expansion, so Z3's own cardinality propagation
/// applies and the term does not grow quadratically in the group size.
/// </remarks>
[TestClass]
public class UnweightedPbTests
{
    /// <summary>Five flags, the group the cardinality constraints are stated over.</summary>
    public class BoolQuintuple
    {
        /// <summary>Gets or sets the five flags.</summary>
        public bool[] B { get; set; } = new bool[5];
    }

    [TestMethod]
    public void ExactlyOne_SelectsExactlyOneIndicator()
    {
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<BoolQuintuple>()
            .Where(t => Z3Methods.ExactlyOne(t.B[0], t.B[1], t.B[2], t.B[3], t.B[4]))
            .Solve();

        solution.ShouldNotBeNull();
        solution!.B.Count(set => set).ShouldBe(1);
    }

    [TestMethod]
    public void ExactlyOne_WithForcedTwoIsUnsat()
    {
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<BoolQuintuple>()
            .Where(t => Z3Methods.ExactlyOne(t.B[0], t.B[1], t.B[2], t.B[3], t.B[4]))

            // Forcing two on contradicts the cardinality constraint.
            .Where(t => t.B[0] && t.B[1])
            .Solve();

        solution.ShouldBeNull();
    }

    [TestMethod]
    public void AtMostOne_AllowsZeroOrOne()
    {
        // Three halves that only mean something together: zero allowed, one allowed, two
        // forbidden. A constraint that were not asserted at all would let the third half pass.
        using var ctx = new Z3Context();

        var theorem = ctx.NewTheorem<BoolQuintuple>()
            .Where(t => Z3Methods.AtMostOne(t.B[0], t.B[1], t.B[2], t.B[3], t.B[4]));

        var none = theorem.Where(t => !t.B[0] && !t.B[1] && !t.B[2] && !t.B[3] && !t.B[4]).Solve();
        none.ShouldNotBeNull();

        var exactlyOne = theorem.Where(t => t.B[2] && !t.B[0] && !t.B[1] && !t.B[3] && !t.B[4]).Solve();
        exactlyOne.ShouldNotBeNull();

        var two = theorem.Where(t => t.B[0] && t.B[1]).Solve();
        two.ShouldBeNull();
    }

    [TestMethod]
    public void AtLeastOne_ForbidsAllFalse()
    {
        using var ctx = new Z3Context();

        var theorem = ctx.NewTheorem<BoolQuintuple>()
            .Where(t => Z3Methods.AtLeastOne(t.B[0], t.B[1], t.B[2], t.B[3], t.B[4]));

        var oneSet = theorem.Where(t => t.B[2]).Solve();
        oneSet.ShouldNotBeNull();

        var allClear = theorem
            .Where(t => !t.B[0] && !t.B[1] && !t.B[2] && !t.B[3] && !t.B[4])
            .Solve();
        allClear.ShouldBeNull();
    }

    [TestMethod]
    public void ExactlyOne_ComposesWithArithmetic()
    {
        // The one-hot shape: exactly one option is picked, and a further constraint pins which.
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<BoolQuintuple>()
            .Where(t => Z3Methods.ExactlyOne(t.B[0], t.B[1], t.B[2], t.B[3], t.B[4]))
            .Where(t => t.B[2])
            .Solve();

        solution.ShouldNotBeNull();
        solution!.B[2].ShouldBeTrue();

        for (int i = 0; i < solution.B.Length; i++)
        {
            if (i == 2)
            {
                continue;
            }

            solution.B[i].ShouldBeFalse();
        }
    }

    [TestMethod]
    public void AtLeastOne_OverARuntimeGroup_IsTranslated()
    {
        // The point of the variadic shape: the group size is a value, not the number of arguments
        // written at the call site. Written out, this constraint would need one argument per flag.
        using var ctx = new Z3Context();
        const int GroupSize = 5;

        var solution = ctx.NewTheorem<BoolQuintuple>()
            .Where(t => Z3Methods.AtLeastOne(
                Enumerable.Range(0, GroupSize).Select(i => t.B[i]).ToArray()))
            .Solve();

        solution.ShouldNotBeNull();
        solution!.B.Count(set => set).ShouldBeGreaterThanOrEqualTo(1);
    }

    [TestMethod]
    public void EmptyGroup_IsDecided()
    {
        // Nothing can make one of zero indicators true, and at most one of zero holds vacuously.
        using var ctx = new Z3Context();

        ctx.NewTheorem<BoolQuintuple>()
            .Where(t => Z3Methods.AtMostOne())
            .Solve()
            .ShouldNotBeNull();

        ctx.NewTheorem<BoolQuintuple>()
            .Where(t => Z3Methods.ExactlyOne())
            .Solve()
            .ShouldBeNull();

        ctx.NewTheorem<BoolQuintuple>()
            .Where(t => Z3Methods.AtLeastOne())
            .Solve()
            .ShouldBeNull();
    }
}
