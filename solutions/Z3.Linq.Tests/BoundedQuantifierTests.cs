namespace Z3.Linq.Tests;

#pragma warning disable CS1591

/// <summary>
/// The bounded quantifiers: <see cref="Z3Methods.ForAll{T}"/> and <see cref="Z3Methods.Exists{T}"/>.
/// </summary>
/// <remarks>
/// They are finite-domain quantifiers. The domain is a host value and the predicate is unrolled over
/// it, so a single <c>Where</c> expresses what would otherwise need a loop emitting one constraint per
/// index. They are not SMT quantifiers over an unbounded sort, and this class says so where the
/// distinction is observable: the empty domain.
/// </remarks>
[TestClass]
public class BoundedQuantifierTests
{
    /// <summary>Four integers, the domain the quantifiers range over.</summary>
    public class ArrayEnv
    {
        /// <summary>Gets or sets the four values.</summary>
        public int[] Values { get; set; } = new int[4];
    }

    [TestMethod]
    public void ForAll_BoundsEveryElement()
    {
        // One constraint, four bounds: the unrolled conjunction is what makes all four hold at once.
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<ArrayEnv>()
            .Where(e => Z3Methods.ForAll(Enumerable.Range(0, 4), i => e.Values[i] >= 1 && e.Values[i] <= 9))
            .Solve();

        solution.ShouldNotBeNull();
        solution!.Values.Length.ShouldBe(4);
        solution.Values.ShouldAllBe(value => value >= 1 && value <= 9);
    }

    [TestMethod]
    public void Exists_RequiresAtLeastOneWitness()
    {
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<ArrayEnv>()
            .Where(e => Z3Methods.ForAll(Enumerable.Range(0, 4), i => e.Values[i] >= 0 && e.Values[i] <= 5))
            .Where(e => Z3Methods.Exists(Enumerable.Range(0, 4), i => e.Values[i] == 5))
            .Solve();

        solution.ShouldNotBeNull();
        solution!.Values.ShouldAllBe(value => value >= 0 && value <= 5);
        solution.Values.ShouldContain(5);
    }

    [TestMethod]
    public void ForAll_CombinesWithExists_ForExactlyConstrainedShape()
    {
        // The bounds hold everywhere while two named values each occur somewhere - the shape that
        // needs both quantifiers, since neither alone can state it.
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<ArrayEnv>()
            .Where(e => Z3Methods.ForAll(Enumerable.Range(0, 4), i => e.Values[i] >= 0 && e.Values[i] <= 10))
            .Where(e => Z3Methods.Exists(Enumerable.Range(0, 4), i => e.Values[i] == 0))
            .Where(e => Z3Methods.Exists(Enumerable.Range(0, 4), i => e.Values[i] == 10))
            .Solve();

        solution.ShouldNotBeNull();
        solution!.Values.ShouldAllBe(value => value >= 0 && value <= 10);
        solution.Values.ShouldContain(0);
        solution.Values.ShouldContain(10);
    }

    [TestMethod]
    public void ForAll_EmptyDomain_IsVacuouslyTrue()
    {
        // The predicate is a contradiction, so over a non-empty domain this would be unsatisfiable.
        // Over the empty domain it holds, and the rest of the theorem decides the solution.
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<ArrayEnv>()
            .Where(e => Z3Methods.ForAll(Array.Empty<int>(), i => e.Values[i] > e.Values[i]))
            .Where(e => e.Values[0] == 7)
            .Solve();

        solution.ShouldNotBeNull();
        solution!.Values[0].ShouldBe(7);
    }

    [TestMethod]
    public void Exists_EmptyDomain_IsVacuouslyFalse()
    {
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<ArrayEnv>()
            .Where(e => Z3Methods.Exists(Array.Empty<int>(), i => e.Values[i] == 0))
            .Solve();

        solution.ShouldBeNull();
    }

    [TestMethod]
    public void ForAll_OverARuntimeDomain_IsTranslated()
    {
        // The point of the shape: the domain size is a value, not the number of lambdas written out.
        using var ctx = new Z3Context();
        int size = 4;

        var solution = ctx.NewTheorem<ArrayEnv>()
            .Where(e => Z3Methods.ForAll(Enumerable.Range(0, size), i => e.Values[i] != 0))
            .Solve();

        solution.ShouldNotBeNull();
        solution!.Values.ShouldAllBe(value => value != 0);
    }
}
