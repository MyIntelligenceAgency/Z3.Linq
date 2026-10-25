namespace Z3.Linq.Tests;

#pragma warning disable CS1591

/// <summary>
/// The diagnostic surface <see cref="Theorem{T}.Explain"/>.
/// </summary>
/// <remarks>
/// <see cref="Theorem{T}.Solve(CancellationToken)"/> collapses every non-satisfiable outcome to <c>default(T)</c>, so
/// "no solution exists" and "the solver could not decide" arrive as the same value, and nothing
/// says <em>which</em> constraints contradict each other. This class holds both halves: the status
/// is reported as Z3 reports it, and on a conflict the core names the constraints whose removal
/// would restore satisfiability.
/// </remarks>
[TestClass]
public class ExplainUnsatCoreTests
{
    /// <summary>One integer.</summary>
    public class Cell
    {
        /// <summary>Gets or sets the value.</summary>
        public int X { get; set; }
    }

    /// <summary>Two integers, for a conflict that spans symbols.</summary>
    public class TwoCells
    {
        /// <summary>Gets or sets the first value.</summary>
        public int A { get; set; }

        /// <summary>Gets or sets the second value.</summary>
        public int B { get; set; }
    }

    [TestMethod]
    public void Explain_ReportsSatisfiable_WhenConsistent()
    {
        using var ctx = new Z3Context();

        var explanation = ctx.NewTheorem<Cell>()
            .Where(c => c.X >= 0)
            .Where(c => c.X <= 10)
            .Explain();

        explanation.Status.ShouldBe(SolveStatus.Satisfiable);
        explanation.IsSatisfiable.ShouldBeTrue();
        explanation.UnsatCore.ShouldBeEmpty();
        explanation.ToString().ShouldBe("Satisfiable");
    }

    [TestMethod]
    public void Explain_ReportsUnsatisfiable_WithConflictingConstraints()
    {
        // #0 and #2 conflict; #1 is innocent and satisfiable alongside either of them alone, so a
        // core that named it would be blaming a constraint the refutation does not need.
        using var ctx = new Z3Context();

        var explanation = ctx.NewTheorem<Cell>()
            .Where(c => c.X == 1)
            .Where(c => c.X >= 0)
            .Where(c => c.X == 2)
            .Explain();

        explanation.Status.ShouldBe(SolveStatus.Unsatisfiable);
        explanation.IsSatisfiable.ShouldBeFalse();

        int[] coreIndexes = explanation.UnsatCore.Select(core => core.Index).ToArray();
        coreIndexes.ShouldContain(0);
        coreIndexes.ShouldContain(2);
        coreIndexes.ShouldNotContain(1);
    }

    [TestMethod]
    public void Explain_CoreCarriesSourceExpression()
    {
        using var ctx = new Z3Context();

        var explanation = ctx.NewTheorem<Cell>()
            .Where(c => c.X == 5)
            .Where(c => c.X == 6)
            .Explain();

        explanation.Status.ShouldBe(SolveStatus.Unsatisfiable);
        explanation.UnsatCore.Count.ShouldBe(2);
        explanation.UnsatCore.ShouldAllBe(core => !string.IsNullOrWhiteSpace(core.Expression));
        explanation.UnsatCore.ShouldContain(core => core.Expression.Contains("== 5", StringComparison.Ordinal));
        explanation.ToString().ShouldContain("#");
    }

    [TestMethod]
    public void Explain_DoesNotAlterSolve()
    {
        using var ctx = new Z3Context();

        var theorem = ctx.NewTheorem<Cell>()
            .Where(c => c.X == 42);

        var explanation = theorem.Explain();
        var solution = theorem.Solve();

        explanation.Status.ShouldBe(SolveStatus.Satisfiable);
        solution.ShouldNotBeNull();
        solution!.X.ShouldBe(42);
    }

    [TestMethod]
    public void Explain_TransitiveConflict_AllThreeInCore()
    {
        // Two inequalities that cannot both hold: the refutation needs both, so both are named.
        using var ctx = new Z3Context();

        var explanation = ctx.NewTheorem<TwoCells>()
            .Where(t => t.A < t.B)
            .Where(t => t.B < t.A)
            .Explain();

        explanation.Status.ShouldBe(SolveStatus.Unsatisfiable);

        int[] coreIndexes = explanation.UnsatCore.Select(core => core.Index).ToArray();
        coreIndexes.ShouldContain(0);
        coreIndexes.ShouldContain(1);
    }

    [TestMethod]
    public void Explain_AgreesWithSolve_OnATypeBoundConflict()
    {
        // int.MaxValue + 1 cannot be held by an int, so the solve path answers "no solution" through
        // the range it asserts on the symbol. The diagnostic agrees with it, and still names the
        // constraint that conflicts with the range rather than returning an empty core.
        using var ctx = new Z3Context();

        var theorem = ctx.NewTheorem<Cell>()
            .Where(c => c.X > int.MaxValue);

        theorem.TrySolve(out Cell? solution).ShouldBeFalse();
        solution.ShouldBeNull();

        var explanation = theorem.Explain().Status;
        explanation.ShouldBe(SolveStatus.Unsatisfiable);

        ctx.NewTheorem<Cell>()
            .Where(c => c.X > int.MaxValue)
            .Explain()
            .UnsatCore
            .ShouldContain(core => core.Index == 0);
    }
}
