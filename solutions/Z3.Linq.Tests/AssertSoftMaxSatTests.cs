namespace Z3.Linq.Tests;

using System;
using Xunit;

/// <summary>
/// Regression tests for weighted-MaxSAT soft constraints via <c>Theorem&lt;T&gt;.SoftWhere</c> (DSL backlog
/// item B1, #4616). A soft constraint is not required to hold: when it conflicts with the hard constraints
/// (or with a higher-weight soft constraint), the optimizer leaves it violated and pays its weight, minimizing
/// the total weight of violated soft constraints. Before B1, the DSL had only hard <c>.Where</c> constraints
/// (a violated constraint simply made the theorem UNSAT — <c>null</c>); there was no way to express
/// "satisfy this if you can, but prefer the other".
/// </summary>
public class AssertSoftMaxSatTests
{
    public class Flags
    {
        public bool A { get; set; }
        public bool B { get; set; }
    }

    /// <summary>
    /// Two soft constraints in direct conflict via a hard XOR (exactly one of A, B is true). The higher-weight
    /// soft constraint must win: weight(A) > weight(B) => A is honored, B is sacrificed.
    /// </summary>
    [Fact]
    public void HigherWeightSoftConstraint_WinsConflict()
    {
        using var ctx = new Z3Context();

        // Hard: A xor B (exactly one true). Soft: prefer A (weight 10) and prefer B (weight 1).
        var theorem = ctx.NewTheorem<Flags>()
            .Where(f => f.A ^ f.B)
            .SoftWhere(f => f.A, weight: 10)
            .SoftWhere(f => f.B, weight: 1);

        var result = theorem.Solve();

        Assert.NotNull(result);
        Assert.True(result!.A);   // higher-weight soft constraint honored
        Assert.False(result.B);   // lower-weight soft constraint sacrificed
        Assert.True(result.A ^ result.B); // hard constraint still respected
    }

    /// <summary>
    /// Symmetric check: flipping the weights flips the outcome, proving the weight (not declaration order)
    /// drives the decision.
    /// </summary>
    [Fact]
    public void FlippingWeights_FlipsOutcome()
    {
        using var ctx = new Z3Context();

        var theorem = ctx.NewTheorem<Flags>()
            .Where(f => f.A ^ f.B)
            .SoftWhere(f => f.A, weight: 1)
            .SoftWhere(f => f.B, weight: 10);

        var result = theorem.Solve();

        Assert.NotNull(result);
        Assert.False(result!.A);
        Assert.True(result.B);
    }

    /// <summary>
    /// A soft constraint compatible with the hard constraints is satisfied (no reason to violate it).
    /// </summary>
    [Fact]
    public void CompatibleSoftConstraint_IsSatisfied()
    {
        using var ctx = new Z3Context();

        var theorem = ctx.NewTheorem<Flags>()
            .Where(f => f.A ^ f.B)
            .SoftWhere(f => f.A, weight: 5); // no conflicting soft constraint

        var result = theorem.Solve();

        Assert.NotNull(result);
        Assert.True(result!.A);
        Assert.False(result.B);
    }

    /// <summary>
    /// Soft constraints never make a satisfiable hard problem UNSAT: even an impossible soft constraint
    /// (paired against a hard fact) just gets sacrificed, and a model is still returned.
    /// </summary>
    [Fact]
    public void ImpossibleSoftConstraint_DoesNotMakeProblemUnsat()
    {
        using var ctx = new Z3Context();

        // Hard: A is true. Soft: A is false (impossible) — sacrificed, model still returned.
        var theorem = ctx.NewTheorem<Flags>()
            .Where(f => f.A)
            .SoftWhere(f => !f.A, weight: 100);

        var result = theorem.Solve();

        Assert.NotNull(result);
        Assert.True(result!.A); // hard wins; soft sacrificed
    }

    /// <summary>
    /// A non-positive weight is rejected eagerly (Z3 <c>AssertSoft</c> takes an unsigned weight; a 0 or
    /// negative weight is a programming error rather than "free to violate").
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void NonPositiveWeight_Throws(int weight)
    {
        using var ctx = new Z3Context();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => ctx.NewTheorem<Flags>().SoftWhere(f => f.A, weight));
    }

    /// <summary>
    /// Numeric MaxSAT: maximize the number of satisfied soft equalities on an integer cell whose hard
    /// constraint pins it to a single value. Two soft constraints want different values; the higher-weight
    /// one wins, and the lower-weight one is paid.
    /// </summary>
    public class Cell
    {
        public int X { get; set; }
    }

    [Fact]
    public void NumericSoftConstraints_HigherWeightTargetWins()
    {
        using var ctx = new Z3Context();

        var theorem = ctx.NewTheorem<Cell>()
            .Where(c => c.X >= 0 && c.X <= 9)
            .SoftWhere(c => c.X == 3, weight: 2)
            .SoftWhere(c => c.X == 7, weight: 5);

        var result = theorem.Solve();

        Assert.NotNull(result);
        Assert.Equal(7, result!.X); // weight 5 > weight 2
    }
}
