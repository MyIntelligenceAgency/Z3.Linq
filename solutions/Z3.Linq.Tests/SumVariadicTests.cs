namespace Z3.Linq.Tests;

#pragma warning disable CS1591

/// <summary>
/// The variadic <see cref="Z3Methods.Sum{T}"/> magic method: a sum whose number of terms is a
/// property of the collection, not of the written expression.
/// </summary>
/// <remarks>
/// The visitor handles the binary <c>+</c> as a pairwise addition, so summing an arbitrary number
/// of theorem-collection elements would otherwise need per-element unrolling. <c>Sum</c> maps to a
/// single variadic addition over the materialised terms.
/// </remarks>
[TestClass]
public class SumVariadicTests
{
    /// <summary>Three integers, the smallest collection a sum can be told apart from a pair.</summary>
    public class Triple
    {
        /// <summary>Gets or sets the three values.</summary>
        public int[] Values { get; set; } = new int[3];
    }

    /// <summary>Three flags, for the indicator-encoding case.</summary>
    public class BoolTriple
    {
        /// <summary>Gets or sets the three flags.</summary>
        public bool[] Flags { get; set; } = new bool[3];
    }

    private static Theorem<Triple> BuildInRange(Z3Context ctx, int low, int high)
    {
        var theorem = ctx.NewTheorem<Triple>();

        for (int i = 0; i < 3; i++)
        {
            int position = i;
            theorem = theorem.Where(t => t.Values[position] >= low && t.Values[position] <= high);
        }

        return theorem;
    }

    [TestMethod]
    [DataRow(CollectionHandling.Array)]
    [DataRow(CollectionHandling.Constants)]
    public void Sum_OfCollection_EqualsTarget(CollectionHandling mode)
    {
        // Each value in [1, 9] and the three sum to 10: many solutions, so the assertion is the
        // invariant the constraint states, not a particular assignment.
        using var ctx = new Z3Context { DefaultCollectionHandling = mode };

        var solution = BuildInRange(ctx, 1, 9)
            .Where(t => Z3Methods.Sum(t.Values[0], t.Values[1], t.Values[2]) == 10)
            .Solve();

        solution.ShouldNotBeNull();
        (solution!.Values[0] + solution.Values[1] + solution.Values[2]).ShouldBe(10);
    }

    [TestMethod]
    [DataRow(CollectionHandling.Array)]
    [DataRow(CollectionHandling.Constants)]
    public void Sum_RespectsUpperBoundAndUnsatAboveIt(CollectionHandling mode)
    {
        // Each value in [1, 3], so the largest sum is 9: 9 is satisfiable and 10 is not. The two
        // halves matter together - a sum that were not actually asserted would let both pass.
        using var ctx = new Z3Context { DefaultCollectionHandling = mode };

        var theorem = BuildInRange(ctx, 1, 3);

        var sat = theorem.Where(t => Z3Methods.Sum(t.Values[0], t.Values[1], t.Values[2]) == 9).Solve();
        sat.ShouldNotBeNull();

        var unsat = theorem.Where(t => Z3Methods.Sum(t.Values[0], t.Values[1], t.Values[2]) == 10).Solve();
        unsat.ShouldBeNull();
    }

    [TestMethod]
    public void Sum_OfConditionalIndicators_CountsTheTrueOnes()
    {
        // The indicator encoding, condition and sum together: sum of (flag ? 1 : 0) over three
        // flags equals 2 exactly when two of them are set.
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<BoolTriple>()
            .Where(t => Z3Methods.Sum(t.Flags[0] ? 1 : 0, t.Flags[1] ? 1 : 0, t.Flags[2] ? 1 : 0) == 2)
            .Solve();

        solution.ShouldNotBeNull();
        solution!.Flags.Count(f => f).ShouldBe(2);
    }
}
