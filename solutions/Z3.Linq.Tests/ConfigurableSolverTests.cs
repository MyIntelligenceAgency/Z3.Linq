namespace Z3.Linq.Tests;

#pragma warning disable CS1591

/// <summary>
/// Choosing the solver a theorem is checked with: <see cref="Z3Context.SolverKind"/>,
/// <see cref="Z3Context.Logic"/>, <see cref="Z3Context.Tactics"/> and
/// <see cref="Z3Context.SetParameter(string, uint)"/>.
/// </summary>
/// <remarks>
/// The solve path used to hard-code the combined solver, so neither the lightweight incremental one
/// nor a logic-specialised one could be chosen, and Z3 solver parameters could not be set at all.
/// Every case below asserts the same witness, so what varies is the path taken to it - a test that
/// settled for "it returned something" would pass on a solver that had silently fallen back.
/// </remarks>
[TestClass]
public class ConfigurableSolverTests
{
    [TestMethod]
    public void DefaultSolverKind_PreservesBehaviour()
    {
        using var ctx = new Z3Context();
        ctx.SolverKind.ShouldBe(SolverKind.Default);

        var result = LinearSystem(ctx).Solve();

        result.ShouldNotBeNull();
        result!.X.ShouldBe(8);
        result.Y.ShouldBe(4);
    }

    [TestMethod]
    public void SimpleSolverKind_SolvesSameSystem()
    {
        using var ctx = new Z3Context { SolverKind = SolverKind.Simple };

        var result = LinearSystem(ctx).Solve();

        result.ShouldNotBeNull();
        result!.X.ShouldBe(8);
        result.Y.ShouldBe(4);
    }

    [TestMethod]
    public void LogicSolverKind_QF_LIA_SolvesIntegerArithmetic()
    {
        using var ctx = new Z3Context { SolverKind = SolverKind.Logic, Logic = "QF_LIA" };

        var result = LinearSystem(ctx).Solve();

        result.ShouldNotBeNull();
        result!.X.ShouldBe(8);
        result.Y.ShouldBe(4);
    }

    [TestMethod]
    public void LogicSolverKind_WithoutLogic_Throws()
    {
        // Z3's Context.MkSolver(string) treats an empty logic as "pick one yourself", so asking for
        // the logic-specialised solver without naming a logic would silently build the general one.
        using var ctx = new Z3Context { SolverKind = SolverKind.Logic };

        var exception = Should.Throw<InvalidOperationException>(() => LinearSystem(ctx).Solve());

        exception.Message.ShouldContain("Logic");
    }

    [TestMethod]
    public void SetParameter_RandomSeed_IsPlumbedAndSolves()
    {
        // The parameter has to survive the trip to the solver: Z3 types its parameters, so a
        // recognised name with the wrong type fails when the solver is configured, and reaching a
        // model is the evidence this one landed as the unsigned integer random_seed expects.
        using var ctx = new Z3Context();
        Z3Context returned = ctx.SetParameter("random_seed", 42u);

        returned.ShouldBeSameAs(ctx);

        var result = LinearSystem(ctx).Solve();

        result.ShouldNotBeNull();
        result!.X.ShouldBe(8);
        result.Y.ShouldBe(4);
    }

    [TestMethod]
    public void SetParameter_EmptyName_Throws()
    {
        using var ctx = new Z3Context();

        Should.Throw<ArgumentException>(() => ctx.SetParameter(string.Empty, 1u));
    }

    [TestMethod]
    public void Tactics_Single_SolvesSameSystem()
    {
        // A one-element sequence is returned as it is: Context.AndThen needs two operands.
        using var ctx = new Z3Context { Tactics = ["smt"] };

        var result = LinearSystem(ctx).Solve();

        result.ShouldNotBeNull();
        result!.X.ShouldBe(8);
        result.Y.ShouldBe(4);
    }

    [TestMethod]
    public void Tactics_Sequence_SolvesSameSystem()
    {
        // Simplify, propagate equalities, then decide: the canonical non-trivial arithmetic chain.
        using var ctx = new Z3Context { Tactics = ["simplify", "solve-eqs", "smt"] };

        var result = LinearSystem(ctx).Solve();

        result.ShouldNotBeNull();
        result!.X.ShouldBe(8);
        result.Y.ShouldBe(4);
    }

    [TestMethod]
    public void Tactics_OverrideSolverKind()
    {
        // Both are set: the tactic pipeline wins, and the result is unchanged. A sequence already
        // ends in a decision procedure, so layering a solver kind on top would be a no-op.
        using var ctx = new Z3Context
        {
            SolverKind = SolverKind.Logic,
            Logic = "QF_LIA",
            Tactics = ["simplify", "smt"],
        };

        var result = LinearSystem(ctx).Solve();

        result.ShouldNotBeNull();
        result!.X.ShouldBe(8);
        result.Y.ShouldBe(4);
    }

    [TestMethod]
    public void Tactics_BitBlast_SolvesBitVectorOverflow()
    {
        // The overflow predicate a + b < a is satisfiable only because the addition wraps modulo
        // 2^4 - over the unbounded integers it is simply false. Bit-blasting reduces the bit-vector
        // theory to propositional SAT, which is the decision procedure this problem belongs to: the
        // tactic is chosen because it matches the theory, not because any solver would do.
        using var ctx = new Z3Context { Tactics = ["simplify", "bit-blast", "sat"] };

        var result = ctx.NewTheorem<Reg4BitVec>()
            .Where(r => r.B >= 1)
            .Where(r => r.A + r.B < r.A)
            .Solve();

        result.ShouldNotBeNull();
        result!.A.ShouldBeInRange(0, 15);
        result.B.ShouldBeInRange(1, 15);
        ((result.A + result.B) % 16).ShouldBeLessThan(result.A);
    }

    /// <summary>Two integers, with a unique integer solution of <c>X = 8</c>, <c>Y = 4</c>.</summary>
    public class LinearEnv
    {
        /// <summary>Gets or sets the first variable.</summary>
        public int X { get; set; }

        /// <summary>Gets or sets the second variable.</summary>
        public int Y { get; set; }
    }

    /// <summary>A pair of 4-bit unsigned registers, for the bit-blast tactic case.</summary>
    public class Reg4BitVec
    {
        /// <summary>Gets or sets the first register.</summary>
        [BitVecWidth(4)]
        public int A { get; set; }

        /// <summary>Gets or sets the second register.</summary>
        [BitVecWidth(4)]
        public int B { get; set; }
    }

    /// <summary>
    /// The linear system every solver kind is asked to solve.
    /// </summary>
    /// <param name="ctx">The context whose configuration is under test.</param>
    /// <returns>The theorem, before it is solved.</returns>
    private static Theorem<LinearEnv> LinearSystem(Z3Context ctx)
    {
        return ctx.NewTheorem<LinearEnv>()
            .Where(e => e.X + e.Y == 12)
            .Where(e => e.X == 2 * e.Y);
    }
}
