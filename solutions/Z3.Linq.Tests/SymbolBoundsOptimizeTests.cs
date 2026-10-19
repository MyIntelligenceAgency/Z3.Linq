namespace Z3.Linq.Tests;

using System;
using Xunit;

/// <summary>
/// One discriminating test per row of <c>Theorem.GetBounds</c>: an optimisation with no other
/// bound on a symbol returns the extreme of its type, and an <c>int</c> symbol constrained beyond
/// its range is unsatisfiable. Each test fails if its own type's row is dropped from the bounds -
/// which is the gap in <see cref="SymbolBoundsTests"/>, where only the <c>short</c> row is
/// exercised by a test that can fail (the <c>int</c>, <c>long</c> and <c>DateTime</c> tests there
/// assert the extremes are still reachable, which holds with the bounds and without them).
/// </summary>
/// <remarks>
/// Port of the optimisation evidence in endjin/Z3.Linq#98 (their #87). The rationale for bounding
/// scalars only, and for reading a collection element with a checked conversion instead, is on
/// <c>Theorem.AssertBounds</c>.
/// </remarks>
public class SymbolBoundsOptimizeTests
{
    [Fact]
    public void Optimize_ShortSymbolMaximised_ReturnsShortMaxValue()
    {
        using var ctx = new Z3Context();

        var result = ctx.NewTheorem<Symbols<short, int>>()
            .Where(t => t.X2 == 1)
            .Optimize(Optimization.Maximize, t => t.X1);

        Assert.NotNull(result);
        Assert.Equal(short.MaxValue, result!.X1);
    }

    [Fact]
    public void Optimize_ShortSymbolMinimised_ReturnsShortMinValue()
    {
        using var ctx = new Z3Context();

        var result = ctx.NewTheorem<Symbols<short, int>>()
            .Where(t => t.X2 == 1)
            .Optimize(Optimization.Minimize, t => t.X1);

        Assert.NotNull(result);
        Assert.Equal(short.MinValue, result!.X1);
    }

    [Fact]
    public void Optimize_IntSymbolMaximised_ReturnsIntMaxValue()
    {
        // Without the TypeCode.Int32 row, Z3 maximises an unbounded integer and the answer is
        // whatever it supplies for the objective - zero, measured - not the extreme of the type.
        using var ctx = new Z3Context();

        var result = ctx.NewTheorem<Symbols<int, int>>()
            .Where(t => t.X2 == 1)
            .Optimize(Optimization.Maximize, t => t.X1);

        Assert.NotNull(result);
        Assert.Equal(int.MaxValue, result!.X1);
    }

    [Fact]
    public void Optimize_LongSymbolMinimised_ReturnsLongMinValue()
    {
        using var ctx = new Z3Context();

        var result = ctx.NewTheorem<Symbols<long, int>>()
            .Where(t => t.X2 == 1)
            .Optimize(Optimization.Minimize, t => t.X1);

        Assert.NotNull(result);
        Assert.Equal(long.MinValue, result!.X1);
    }

    [Fact]
    public void Optimize_DateTimeSymbolMaximised_ReturnsDateTimeMaxValue()
    {
        using var ctx = new Z3Context();

        var result = ctx.NewTheorem<Symbols<DateTime, int>>()
            .Where(t => t.X2 == 1)
            .Optimize(Optimization.Maximize, t => t.X1);

        Assert.NotNull(result);
        Assert.Equal(DateTime.MaxValue.Ticks, result!.X1.Ticks);
        Assert.Equal(DateTimeKind.Utc, result.X1.Kind);
    }

    [Fact]
    public void Solve_DateTimeSymbolBelowDateTimeMinValue_IsUnsatisfiable()
    {
        // The refutation side of the TypeCode.DateTime row, in the direction that can be measured:
        // minimising an unbounded symbol returns zero, and zero ticks IS DateTime.MinValue, so a
        // minimisation cannot tell the bound from its absence. Below MinValue it can - the
        // constraint is satisfiable in integers (negative ticks), and only the bound refutes it.
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<Symbols<DateTime, int>>()
            .Where(t => t.X1 < DateTime.MinValue)
            .Solve();

        Assert.Null(solution);
    }

    [Fact]
    public void Solve_IntSymbolConstrainedBeyondIntRange_IsUnsatisfiable()
    {
        // The refutation side of the TypeCode.Int32 row: a comparison against a literal outside
        // the range is expressible in C# (int promotes), so only the bound refutes it. Before the
        // bounds this solved and the read threw Z3Exception "Numeral is not an int".
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<Symbols<int, int>>()
            .Where(t => t.X1 > int.MaxValue)
            .Solve();

        Assert.Null(solution);
    }

    [Fact]
    public void Optimize_ShortSymbolConstrainedOutsideItsRange_ReturnsDefault()
    {
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<Symbols<short, int>>()
            .Where(t => t.X1 > short.MaxValue)
            .Optimize(Optimization.Minimize, t => t.X2);

        Assert.Null(solution);
    }

    [Fact]
    public void Optimize_ShortSymbolInANestedObjectMaximised_ReturnsShortMaxValue()
    {
        // The bounds are asserted by walking the environment, and a nested object is an
        // environment of its own under the outer one, so the walk has to descend.
        using var ctx = new Z3Context();

        var result = ctx.NewTheorem<OuterEnvironment>()
            .Where(t => t.Top == 1)
            .Optimize(Optimization.Maximize, t => t.Inner.Value);

        Assert.NotNull(result);
        Assert.Equal(short.MaxValue, result!.Inner.Value);
    }

    private sealed class InnerEnvironment
    {
        public short Value { get; set; }
    }

    private sealed class OuterEnvironment
    {
        public InnerEnvironment Inner { get; set; } = new();

        public int Top { get; set; }
    }
}