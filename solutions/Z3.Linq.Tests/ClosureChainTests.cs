namespace Z3.Linq.Tests;

#pragma warning disable CS1591

/// <summary>
/// A value captured by a constraint and reached through more than one member access must be
/// reduced to a constant.
/// </summary>
/// <remarks>
/// Two shapes reach <c>VisitMember</c> with a chain longer than one level, and both are ordinary
/// C# rather than corners:
/// <list type="bullet">
/// <item>a lambda that captures a method parameter <em>and</em> a loop variable - Roslyn gives the
/// loop body its own display class nested inside the method's, so the outer value is reached
/// through two fields;</item>
/// <item>a property read off a captured instance, <c>limit.Value</c>, which is a field followed by
/// a property.</item>
/// </list>
/// Reading every level off the root constant rather than off the previous level makes the second
/// level fail with "field ... is not a field on the target object".
/// </remarks>
[TestClass]
public class ClosureChainTests
{
    /// <summary>Three integers constrained one by one.</summary>
    public class Triple
    {
        /// <summary>Gets or sets the three values.</summary>
        public int[] Values { get; set; } = new int[3];
    }

    /// <summary>A captured instance whose property the constraint reads.</summary>
    public class Limit
    {
        /// <summary>Gets or sets the bound.</summary>
        public int Value { get; set; } = 4;
    }

    private static Theorem<Triple> BuildFloorPerElement(Z3Context ctx, int floor)
    {
        var theorem = ctx.NewTheorem<Triple>();

        for (int i = 0; i < 3; i++)
        {
            // `position` is captured per iteration, `floor` once for the method: two nesting levels
            // of display class in the expression tree.
            int position = i;
            theorem = theorem.Where(t => t.Values[position] >= floor);
        }

        return theorem;
    }

    [TestMethod]
    [DataRow(CollectionHandling.Array)]
    [DataRow(CollectionHandling.Constants)]
    public void CapturedMethodParameter_InsideLoop_IsReduced(CollectionHandling mode)
    {
        using var ctx = new Z3Context { DefaultCollectionHandling = mode };

        var solution = BuildFloorPerElement(ctx, 5)
            .Where(t => t.Values[0] <= 9 && t.Values[1] <= 9 && t.Values[2] <= 9)
            .Solve();

        solution.ShouldNotBeNull();
        solution!.Values.ShouldAllBe(v => v >= 5 && v <= 9);
    }

    [TestMethod]
    public void CapturedInstance_PropertyChain_IsReduced()
    {
        using var ctx = new Z3Context();
        var limit = new Limit { Value = 4 };

        var solution = ctx.NewTheorem<Triple>()
            .Where(t => t.Values[0] == 7)
            .Where(t => t.Values[0] <= limit.Value)
            .Solve();

        // 7 is above the captured bound, so the theorem is unsatisfiable: the point is that the
        // bound is read at all, and read as 4 rather than as something else.
        solution.ShouldBeNull();
    }

    [TestMethod]
    public void CapturedInstance_PropertyChain_ConstrainsTheSolution()
    {
        using var ctx = new Z3Context();
        var limit = new Limit { Value = 4 };

        var solution = ctx.NewTheorem<Triple>()
            .Where(t => t.Values[0] >= 1)
            .Where(t => t.Values[0] == limit.Value)
            .Solve();

        solution.ShouldNotBeNull();
        solution!.Values[0].ShouldBe(4);
    }
}
