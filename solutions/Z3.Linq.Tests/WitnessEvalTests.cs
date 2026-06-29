namespace Z3.Linq.Tests;

using Xunit;

/// <summary>
/// Regression tests for witness generation via <c>Theorem&lt;T&gt;.Solve(inspect)</c> (DSL backlog item B5,
/// #4616). The base <c>Solve()</c> computes a satisfying Z3 model and then discards it after populating the
/// result object; there was previously no way to read back the model value of a sub-expression that is not
/// directly a member of the result type (e.g. a derived quantity <c>t.X + t.Y</c>, or a relational fact).
/// B5 threads the live model out through a callback so the caller can evaluate arbitrary sub-expressions
/// under the same model that produced the solution.
/// </summary>
public class WitnessEvalTests
{
    public class Pair
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    /// <summary>
    /// A derived integer quantity (not a member of the result type) is evaluable under the model, and agrees
    /// with the returned solution's members.
    /// </summary>
    [Fact]
    public void Witness_EvaluatesDerivedQuantity_ConsistentWithSolution()
    {
        using var ctx = new Z3Context();

        int? witnessSum = null;
        var theorem = ctx.NewTheorem<Pair>()
            .Where(p => p.X == 4)
            .Where(p => p.Y == 5);

        var solution = theorem.Solve(w => witnessSum = w.Eval(p => p.X + p.Y));

        Assert.NotNull(solution);
        Assert.Equal(4, solution!.X);
        Assert.Equal(5, solution.Y);
        Assert.NotNull(witnessSum);
        Assert.Equal(9, witnessSum); // derived X+Y, consistent with the solution members
        Assert.Equal(solution.X + solution.Y, witnessSum);
    }

    /// <summary>
    /// A relational sub-expression (a boolean fact about the model) is evaluable as a witness.
    /// </summary>
    [Fact]
    public void Witness_EvaluatesBooleanFact()
    {
        using var ctx = new Z3Context();

        bool? xLessThanY = null;
        var theorem = ctx.NewTheorem<Pair>()
            .Where(p => p.X == 2)
            .Where(p => p.Y == 7);

        theorem.Solve(w => xLessThanY = w.Eval(p => p.X < p.Y));

        Assert.NotNull(xLessThanY);
        Assert.True(xLessThanY);
    }

    /// <summary>
    /// The inspect callback is NOT invoked when the theorem is UNSAT (no model to witness).
    /// </summary>
    [Fact]
    public void Witness_NotInvoked_OnUnsat()
    {
        using var ctx = new Z3Context();

        bool invoked = false;
        var theorem = ctx.NewTheorem<Pair>()
            .Where(p => p.X == 1)
            .Where(p => p.X == 2); // contradictory => UNSAT

        var solution = theorem.Solve(_ => invoked = true);

        Assert.Null(solution);
        Assert.False(invoked);
    }

    public class Cell
    {
        public int X { get; set; }
    }

    /// <summary>
    /// Witness over an optimized (soft-constraint / B1) theorem: the model that the optimizer produced is the
    /// one inspected, so the witness sees the MaxSAT-chosen value.
    /// </summary>
    [Fact]
    public void Witness_OverSoftConstrainedTheorem_SeesOptimizerModel()
    {
        using var ctx = new Z3Context();

        int? doubled = null;
        var theorem = ctx.NewTheorem<Cell>()
            .Where(c => c.X >= 0 && c.X <= 9)
            .SoftWhere(c => c.X == 3, weight: 2)
            .SoftWhere(c => c.X == 7, weight: 5);

        var solution = theorem.Solve(w => doubled = w.Eval(c => c.X * 2));

        Assert.NotNull(solution);
        Assert.Equal(7, solution!.X);     // higher-weight soft constraint wins (B1)
        Assert.Equal(14, doubled);        // witness of derived 2*X under the optimizer model
    }
}
