namespace Z3.Linq.Tests;

using Xunit;

/// <summary>
/// Regression tests for the <c>ExpressionType.Conditional</c> -> <c>MkIte</c> visitor case
/// (DSL backlog item B2, #4616).
///
/// Before B2, a ternary (<c>cond ? a : b</c>) inside a <c>.Where(...)</c> lambda threw
/// <see cref="System.NotSupportedException"/> because the visitor switch had no case for
/// <see cref="System.Linq.Expressions.ExpressionType.Conditional"/>. The indicator encoding
/// <c>condition ? 1 : 0</c> is the standard building block for MaxSAT-via-Optimize soft
/// penalties and for the disjunction cross-link of the hierarchical MealPlanner theorem.
/// </summary>
public class ConditionalIteTests
{
    /// <summary>
    /// Minimal env: one int variable. Exercises the simplest ternary that returns an int.
    /// </summary>
    public class Indicator
    {
        public int X { get; set; }
    }

    [Fact]
    public void Conditional_TrueBranch_SelectsIfTrue()
    {
        // The model must satisfy X == (X > 5 ? 10 : 0). The branch taken depends on X itself.
        // Satisfying assignments: X == 10 (true branch, 10 > 5 holds) or X == 0 (false branch, 0 > 5 fails).
        using var ctx = new Z3Context();
        var theorem = ctx.NewTheorem<Indicator>()
            .Where(t => t.X == (t.X > 5 ? 10 : 0));

        var result = theorem.Solve();

        Assert.NotNull(result);
        Assert.True(result!.X == 0 || result.X == 10, $"Expected X in {{0, 10}}, got {result.X}");
    }

    [Fact]
    public void Conditional_IndicatorForcesPositiveRange()
    {
        // Equivalent to: indicator I = (X >= 3 ? 1 : 0), and we force I == 1 -> X must be >= 3.
        // Expressed without an extra variable: assert (X >= 3 ? 1 : 0) == 1 directly.
        using var ctx = new Z3Context();
        var theorem = ctx.NewTheorem<Indicator>()
            .Where(t => (t.X >= 3 ? 1 : 0) == 1)
            .Where(t => t.X <= 100);

        var result = theorem.Solve();

        Assert.NotNull(result);
        Assert.True(result!.X >= 3, $"Expected X >= 3, got {result.X}");
    }

    /// <summary>
    /// Two-variable env to exercise a conditional whose test references a different variable
    /// than its value branches — the cross-link shape used by the MealPlanner disjunction.
    /// </summary>
    public class Choice
    {
        public bool Flag { get; set; }
        public int Value { get; set; }
    }

    [Fact]
    public void Conditional_PicksValueBasedOnIndependentFlag()
    {
        // Value == (Flag ? 42 : 7). For either Flag value, a satisfying Value exists.
        using var ctx = new Z3Context();
        var theorem = ctx.NewTheorem<Choice>()
            .Where(t => t.Value == (t.Flag ? 42 : 7));

        var result = theorem.Solve();

        Assert.NotNull(result);
        Assert.Equal(result!.Flag ? 42 : 7, result.Value);
    }

    [Fact]
    public void Conditional_CanForceFlagViaValueConstraint()
    {
        // Value == (Flag ? 42 : 7) AND Value == 42 -> Flag must be true.
        using var ctx = new Z3Context();
        var theorem = ctx.NewTheorem<Choice>()
            .Where(t => t.Value == (t.Flag ? 42 : 7))
            .Where(t => t.Value == 42);

        var result = theorem.Solve();

        Assert.NotNull(result);
        Assert.True(result!.Flag, "Flag must be true when Value is forced to the true-branch constant");
        Assert.Equal(42, result.Value);
    }
}
