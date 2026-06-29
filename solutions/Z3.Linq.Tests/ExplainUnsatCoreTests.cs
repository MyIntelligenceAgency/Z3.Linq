namespace Z3.Linq.Tests;

using System.Linq;
using Xunit;

/// <summary>
/// Regression tests for the diagnostic <c>Theorem&lt;T&gt;.Explain()</c> surface (DSL backlog item B6, #4616).
/// <c>Solve()</c> collapses every non-satisfiable outcome to <c>default(T)</c> — so "no solution exists" and
/// "the solver could not decide" are indistinguishable, and there is no way to learn <em>why</em> a set of
/// constraints is contradictory. <c>Explain()</c> distinguishes Satisfiable / Unsatisfiable / Unknown and, on
/// UNSAT, returns the minimal UNSAT core (the conflicting subset of hard <c>.Where</c> constraints). It is a
/// parallel, non-breaking surface: <c>Solve()</c> is unchanged.
/// </summary>
public class ExplainUnsatCoreTests
{
    public class Cell
    {
        public int X { get; set; }
    }

    [Fact]
    public void Explain_ReportsSatisfiable_WhenConsistent()
    {
        using var ctx = new Z3Context();

        var explanation = ctx.NewTheorem<Cell>()
            .Where(c => c.X >= 0)
            .Where(c => c.X <= 10)
            .Explain();

        Assert.Equal(SolveStatus.Satisfiable, explanation.Status);
        Assert.True(explanation.IsSatisfiable);
        Assert.Empty(explanation.UnsatCore);
    }

    [Fact]
    public void Explain_ReportsUnsatisfiable_WithConflictingConstraints()
    {
        using var ctx = new Z3Context();

        // Three constraints; #0 and #2 conflict (X==1 vs X==2). #1 is satisfiable on its own and should NOT
        // be part of the minimal core.
        var explanation = ctx.NewTheorem<Cell>()
            .Where(c => c.X == 1)   // #0
            .Where(c => c.X >= 0)   // #1 (innocent)
            .Where(c => c.X == 2)   // #2
            .Explain();

        Assert.Equal(SolveStatus.Unsatisfiable, explanation.Status);
        Assert.False(explanation.IsSatisfiable);

        var coreIndexes = explanation.UnsatCore.Select(c => c.Index).ToArray();
        Assert.Contains(0, coreIndexes);
        Assert.Contains(2, coreIndexes);
        Assert.DoesNotContain(1, coreIndexes); // the innocent constraint is excluded from the minimal core
    }

    [Fact]
    public void Explain_CoreCarriesSourceExpression()
    {
        using var ctx = new Z3Context();

        var explanation = ctx.NewTheorem<Cell>()
            .Where(c => c.X == 5)
            .Where(c => c.X == 6)
            .Explain();

        Assert.Equal(SolveStatus.Unsatisfiable, explanation.Status);
        Assert.Equal(2, explanation.UnsatCore.Count);
        // The reported core references the source expressions (string form), not just indexes.
        Assert.All(explanation.UnsatCore, c => Assert.False(string.IsNullOrWhiteSpace(c.Expression)));
        Assert.Contains(explanation.UnsatCore, c => c.Expression.Contains("== 5") || c.Expression.Contains("==5"));
    }

    [Fact]
    public void Explain_DoesNotAlterSolve()
    {
        using var ctx = new Z3Context();

        // A satisfiable theorem: Explain() then Solve() must both behave normally (no shared mutable state).
        var theorem = ctx.NewTheorem<Cell>()
            .Where(c => c.X == 42);

        var explanation = theorem.Explain();
        var solution = theorem.Solve();

        Assert.Equal(SolveStatus.Satisfiable, explanation.Status);
        Assert.NotNull(solution);
        Assert.Equal(42, solution!.X);
    }

    public class TwoCells
    {
        public int A { get; set; }
        public int B { get; set; }
    }

    [Fact]
    public void Explain_TransitiveConflict_AllThreeInCore()
    {
        using var ctx = new Z3Context();

        // A < B, B < A+0 (i.e. B < A), and A != B are mutually contradictory via the first two alone.
        var explanation = ctx.NewTheorem<TwoCells>()
            .Where(t => t.A < t.B)  // #0
            .Where(t => t.B < t.A)  // #1
            .Explain();

        Assert.Equal(SolveStatus.Unsatisfiable, explanation.Status);
        var coreIndexes = explanation.UnsatCore.Select(c => c.Index).ToArray();
        Assert.Contains(0, coreIndexes);
        Assert.Contains(1, coreIndexes);
    }
}
