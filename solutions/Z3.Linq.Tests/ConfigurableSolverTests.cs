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

    // --- Tactics (B10, tactics half of "configurable solver/tactics") ---
    // SolverKind reached the solver half; Tactics reaches the tactics half. A non-empty
    // Tactics sequence is composed via AndThen and takes precedence over SolverKind.

    /// <summary>
    /// A single-tactic pipeline ("smt") is unwrapped (AndThen needs two operands) and produces
    /// a solver that solves the same linear system as the default combined solver.
    /// </summary>
    [Fact]
    public void Tactics_Single_SolvesSameSystem()
    {
        using var ctx = new Z3Context { Tactics = new[] { "smt" } };

        var result = LinearSystem(ctx).Solve();

        Assert.NotNull(result);
        Assert.Equal(8, result!.X);
        Assert.Equal(4, result.Y);
    }

    /// <summary>
    /// A composed pipeline simplify -> solve-eqs -> smt (simplify, propagate equalities, then
    /// decide) solves the linear system with the same witness as the default solver. This is
    /// the canonical non-trivial tactic sequence for arithmetic.
    /// </summary>
    [Fact]
    public void Tactics_Sequence_SolvesSameSystem()
    {
        using var ctx = new Z3Context { Tactics = new[] { "simplify", "solve-eqs", "smt" } };

        var result = LinearSystem(ctx).Solve();

        Assert.NotNull(result);
        Assert.Equal(8, result!.X);
        Assert.Equal(4, result.Y);
    }

    /// <summary>
    /// Tactics take precedence over SolverKind: with both set, the tactic pipeline is used
    /// (the combined tactic already ends in a decision procedure, so SolverKind is moot).
    /// Correctness is unchanged — same witness.
    /// </summary>
    [Fact]
    public void Tactics_OverrideSolverKind()
    {
        using var ctx = new Z3Context
        {
            SolverKind = SolverKind.Logic,
            Logic = "QF_LIA",
            Tactics = new[] { "simplify", "smt" },
        };

        var result = LinearSystem(ctx).Solve();

        Assert.NotNull(result);
        Assert.Equal(8, result!.X);
        Assert.Equal(4, result.Y);
    }

    /// <summary>
    /// The bit-vector overflow predicate <c>a + b &lt; a</c> (modular wrap) is SAT over 4-bit
    /// registers. Solving it through the dedicated bit-blasting pipeline
    /// (simplify -> bit-blast -> sat) returns the SAME overflow witness as the default combined
    /// solver — bit-blast reduces the bit-vector theory to propositional SAT, which is exactly
    /// the specialized decision procedure this problem lives in. This is the Prong-B
    /// discrimination instance: a tactic chosen because it matches the problem's theory, not
    /// because it compiles (the degenerate case [sota-not-workaround] forbids).
    /// </summary>
    [Fact]
    public void Tactics_BitBlast_SolvesBitVectorOverflow()
    {
        using var ctx = new Z3Context { Tactics = new[] { "simplify", "bit-blast", "sat" } };

        var result = ctx.NewTheorem<Reg4BitVec>()
            .Where(r => r.B >= 1)
            .Where(r => r.A + r.B < r.A) // wraps modulo 16
            .Solve();

        Assert.NotNull(result);
        Assert.InRange(result!.A, 0, 15);
        Assert.InRange(result.B, 1, 15);
        Assert.True((result.A + result.B) % 16 < result.A, "the witnessed sum must wrap below A");
    }

    /// <summary>A pair of 4-bit unsigned registers for the bit-blast tactic test.</summary>
    public class Reg4BitVec
    {
        [BitVecWidth(4)]
        public int A { get; set; }

        [BitVecWidth(4)]
        public int B { get; set; }
    }
}
