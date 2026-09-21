namespace Z3.Linq.Tests;

using System;
using Xunit;

/// <summary>
/// Bounds on scalar symbols (port of endjin/Z3.Linq#98, their #87 for the defect).
/// A <c>short</c>/<c>int</c>/<c>long</c>/<c>DateTime</c> symbol reaches Z3 as an integer of
/// unbounded range, so a constraint that no value of the type satisfies still had a model --
/// and the failure only surfaced on read-back as an <see cref="OverflowException"/>. The bounds
/// are asserted at theorem construction, so the search itself now answers "no model".
/// </summary>
public class SymbolBoundsTests
{
    public class ShortBag
    {
        public short S { get; set; }
    }

    public class IntBag
    {
        public int I { get; set; }
    }

    public class LongBag
    {
        public long L { get; set; }
    }

    public class DateBag
    {
        public DateTime D { get; set; }
    }

#pragma warning disable CS0652 // La comparaison à la constante intégrale est inutile, car la constante
                        // est en dehors de la plage du type 'short' -- this warning IS the defect
                        // statement: C# knows the comparison is always false, Z3 did not.
    [Fact]
    public void Solve_ShortLiteralOutOfTypeRange_IsUnsatisfiable()
    {
        // The exact case the unbound range produced: `short == 40000` is false in C#, but Z3 saw
        // an unconstrained integer symbol and answered "satisfiable" with a value no short can
        // hold. Before the bounds this threw on read; now it is simply unsatisfiable.
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<ShortBag>()
            .Where(b => b.S == 40000)
            .Solve();

        Assert.Null(solution);
    }
#pragma warning restore CS0652

    [Fact]
    public void Solve_ShortAboveTypeMaximum_IsUnsatisfiable()
    {
        // Same defect reached without an out-of-type literal: the comparison promotes to int, so
        // the constraint is expressible, and only the bound on S makes it refutable.
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<ShortBag>()
            .Where(b => b.S > short.MaxValue)
            .Solve();

        Assert.Null(solution);
    }

    // No satisfiable short theorem is asserted here on purpose: a short member cannot be read back
    // at all today, for a reason orthogonal to these bounds (ConvertScalarExpr folds Int16 into the
    // Int32 arm, so SetValue receives an Int32 and the reflection call refuses). Measured with the
    // bounds disabled -- identical exception -- and tracked in #17302. The two tests above stay
    // meaningful: they are unsatisfiable, so they never reach the read-back.
    //
    // The edge-guard for the bound's lower end ("the extreme is inside the bound, not outside")
    // is carried by the int and long cases below.

    [Fact]
    public void Solve_IntAtTypeMaximum_StillSolves()
    {
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<IntBag>()
            .Where(b => b.I == int.MaxValue)
            .Solve();

        Assert.NotNull(solution);
        Assert.Equal(int.MaxValue, solution!.I);
    }

    [Fact]
    public void Solve_LongAtTypeMaximum_StillSolves()
    {
        // A long bound cannot be exceeded by any C#-expressible constraint, so the observable
        // effect of bounding it is exclusively the edge case: the extreme must survive.
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<LongBag>()
            .Where(b => b.L == long.MaxValue)
            .Solve();

        Assert.NotNull(solution);
        Assert.Equal(long.MaxValue, solution!.L);
    }

    [Fact]
    public void Solve_DateAtTypeMaximum_StillSolves()
    {
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<DateBag>()
            .Where(b => b.D == DateTime.MaxValue)
            .Solve();

        Assert.NotNull(solution);
        Assert.Equal(DateTime.MaxValue.Ticks, solution!.D.Ticks);
    }

    [Fact]
    public void Solve_UnboundedSymbol_StillReturnsTheModelTheConstraintImplies()
    {
        // The bounds narrow the domain, they do not constrain the answer: a satisfiable theorem
        // with a single equality must still yield exactly that value.
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<IntBag>()
            .Where(b => b.I == 42)
            .Solve();

        Assert.NotNull(solution);
        Assert.Equal(42, solution!.I);
    }
}
