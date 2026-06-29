namespace Z3.Linq.Tests;

using System;

using Xunit;

/// <summary>
/// Tests for configurable solver / logic selection on <see cref="Z3Context"/>
/// (DSL backlog item B10, #4616).
///
/// Before B10, <see cref="Theorem"/> hard-coded <c>ctx.MkSolver()</c>, so neither the
/// lightweight <c>MkSimpleSolver()</c> (which the original endjin code referenced in a
/// commented-out line) nor a logic-specialized <c>MkSolver("QF_LIA")</c> could be chosen,
/// and Z3 module parameters (timeout, random_seed, ...) could not be set. B10 adds
/// <see cref="Z3Context.SolverKind"/>, <see cref="Z3Context.Logic"/> and
/// <see cref="Z3Context.SetParameter(string, string)"/>, plumbed through
/// <c>Z3Context.CreateSolver</c>.
/// </summary>
public class ConfigurableSolverTests
{
    public class LinearEnv
    {
        public int X { get; set; }

        public int Y { get; set; }
    }

    private static Theorem<LinearEnv> LinearSystem(Z3Context ctx)
    {
        // Unique integer solution: X = 8, Y = 4.
        return ctx.NewTheorem<LinearEnv>()
            .Where(e => e.X + e.Y == 12)
            .Where(e => e.X == 2 * e.Y);
    }

    [Fact]
    public void DefaultSolverKind_PreservesBehavior()
    {
        using var ctx = new Z3Context();
        Assert.Equal(SolverKind.Default, ctx.SolverKind);

        var result = LinearSystem(ctx).Solve();

        Assert.NotNull(result);
        Assert.Equal(8, result!.X);
        Assert.Equal(4, result.Y);
    }

    [Fact]
    public void SimpleSolverKind_SolvesSameSystem()
    {
        using var ctx = new Z3Context { SolverKind = SolverKind.Simple };

        var result = LinearSystem(ctx).Solve();

        Assert.NotNull(result);
        Assert.Equal(8, result!.X);
        Assert.Equal(4, result.Y);
    }

    [Fact]
    public void LogicSolverKind_QF_LIA_SolvesIntegerArithmetic()
    {
        using var ctx = new Z3Context { SolverKind = SolverKind.Logic, Logic = "QF_LIA" };

        var result = LinearSystem(ctx).Solve();

        Assert.NotNull(result);
        Assert.Equal(8, result!.X);
        Assert.Equal(4, result.Y);
    }

    [Fact]
    public void LogicSolverKind_WithoutLogic_Throws()
    {
        using var ctx = new Z3Context { SolverKind = SolverKind.Logic };

        var ex = Assert.Throws<InvalidOperationException>(() => LinearSystem(ctx).Solve());
        Assert.Contains("Logic", ex.Message);
    }

    [Fact]
    public void SetParameter_RandomSeed_IsPlumbedAndSolves()
    {
        // A non-default random_seed must be accepted by the underlying Context and still solve.
        using var ctx = new Z3Context();
        var returned = ctx.SetParameter("random_seed", "42");

        Assert.Same(ctx, returned); // fluent chaining

        var result = LinearSystem(ctx).Solve();

        Assert.NotNull(result);
        Assert.Equal(8, result!.X);
        Assert.Equal(4, result.Y);
    }

    [Fact]
    public void SetParameter_EmptyName_Throws()
    {
        using var ctx = new Z3Context();
        Assert.Throws<ArgumentException>(() => ctx.SetParameter("", "1"));
    }
}
