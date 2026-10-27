namespace Z3.Linq.Tests;

#pragma warning disable CS1591

/// <summary>
/// The ternary conditional, <c>cond ? a : b</c>, translated into Z3's if-then-else.
/// </summary>
/// <remarks>
/// The indicator encoding <c>condition ? 1 : 0</c> is the building block of a soft penalty and of a
/// disjunction cross-link, so a ternary that the visitor rejects costs a whole class of theorems.
/// The cases below cover a branch chosen by the symbol itself, the indicator, and a test on one
/// symbol selecting a value for another - the cross-link shape.
/// </remarks>
[TestClass]
public class ConditionalIteTests
{
    [TestMethod]
    public void Conditional_TrueBranch_SelectsIfTrue()
    {
        // X must equal (X > 5 ? 10 : 0): either X = 10, where the test holds, or X = 0, where it
        // does not. Any other value would need one branch to yield it, and neither can.
        using var ctx = new Z3Context();

        var result = ctx.NewTheorem<Indicator>()
            .Where(t => t.X == (t.X > 5 ? 10 : 0))
            .Solve();

        result.ShouldNotBeNull();
        (result!.X == 0 || result.X == 10).ShouldBeTrue($"Expected X in {{0, 10}}, got {result.X}.");
    }

    [TestMethod]
    public void Conditional_IndicatorForcesPositiveRange()
    {
        // The indicator pinned to 1 forces its test to hold.
        using var ctx = new Z3Context();

        var result = ctx.NewTheorem<Indicator>()
            .Where(t => (t.X >= 3 ? 1 : 0) == 1)
            .Where(t => t.X <= 100)
            .Solve();

        result.ShouldNotBeNull();
        (result!.X >= 3).ShouldBeTrue($"Expected X >= 3, got {result.X}.");
    }

    [TestMethod]
    public void Conditional_PicksValueBasedOnIndependentFlag()
    {
        // The test reads one symbol and the branches yield a value for another.
        using var ctx = new Z3Context();

        var result = ctx.NewTheorem<Choice>()
            .Where(t => t.Value == (t.Flag ? 42 : 7))
            .Solve();

        result.ShouldNotBeNull();
        result!.Value.ShouldBe(result.Flag ? 42 : 7);
    }

    [TestMethod]
    public void Conditional_CanForceFlagViaValueConstraint()
    {
        // Forcing the value to the true branch's constant forces the test.
        using var ctx = new Z3Context();

        var result = ctx.NewTheorem<Choice>()
            .Where(t => t.Value == (t.Flag ? 42 : 7))
            .Where(t => t.Value == 42)
            .Solve();

        result.ShouldNotBeNull();
        result!.Flag.ShouldBeTrue("Flag must hold when Value is forced to the true-branch constant.");
        result.Value.ShouldBe(42);
    }

    /// <summary>One integer, for the simplest ternary shapes.</summary>
    public class Indicator
    {
        /// <summary>Gets or sets the value.</summary>
        public int X { get; set; }
    }

    /// <summary>A flag and a value, for the cross-link shape.</summary>
    public class Choice
    {
        /// <summary>Gets or sets the flag the ternary tests.</summary>
        public bool Flag { get; set; }

        /// <summary>Gets or sets the value the branches yield.</summary>
        public int Value { get; set; }
    }
}
