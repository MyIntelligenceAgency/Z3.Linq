namespace Z3.Linq.Tests;

using Xunit;

/// <summary>
/// A <see cref="Rational"/> theorem member as a first-class exact real symbol: it binds to a
/// single real constant, carries arithmetic and comparison constraints exactly, and reads back
/// as a reduced fraction. Before the fix, the member decomposed into unguarded
/// Numerator/Denominator integers with no expression of its own: constraints crashed with
/// NullReferenceException inside Microsoft.Z3 (MkGt on a null ArithExpr) and unconstrained
/// solves crashed in Model.Eval.
/// </summary>
public class RationalSymbolTests
{
    /// <summary>A positional record with a Rational member, materialised through its constructor.</summary>
    public sealed record RationalCell(Rational X);

    [Fact]
    public void RationalMember_ConstrainedWithDoubleBounds_SolvesAndMaterializes()
    {
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem(new { x = default(Rational) })
            .Where(t => t.x > 0.5)
            .Where(t => t.x < 2.0)
            .Solve();

        Assert.NotNull(solution);
        double x = (double)solution!.x;
        Assert.True(x > 0.5 && x < 2.0, $"expected 0.5 < x < 2, got {x}");
        Assert.True(solution.x.Denominator > 0);
    }

    [Fact]
    public void RationalMember_Unconstrained_Materializes()
    {
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem(new { x = default(Rational) })
            .Solve();

        Assert.NotNull(solution);
        Assert.True(solution!.x.Denominator > 0);
    }

    /// <summary>
    /// The capability that did not compile at all before the arithmetic operators: exact sums
    /// and differences between two rational symbols, resolved and read back exactly.
    /// </summary>
    [Fact]
    public void RationalMember_ExactArithmeticBetweenSymbols_SolvesExactly()
    {
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem(new { a = default(Rational), b = default(Rational) })
            .Where(t => t.a + t.b == Rational.Of(3, 4))
            .Where(t => t.a - t.b == Rational.Of(1, 4))
            .Solve();

        Assert.NotNull(solution);
        Assert.Equal(Rational.Of(1, 2), solution!.a);
        Assert.Equal(Rational.Of(1, 4), solution.b);
    }

    /// <summary>The model value is read back reduced: x + x == 1 gives 1/2, never 2/4.</summary>
    [Fact]
    public void RationalMember_ModelValue_IsReducedOnReadBack()
    {
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem(new { x = default(Rational) })
            .Where(t => t.x + t.x == Rational.Of(1, 1))
            .Solve();

        Assert.NotNull(solution);
        Assert.Equal(Rational.Of(1, 2), solution!.x);
        Assert.Equal(1, solution.x.Numerator);
        Assert.Equal(2, solution.x.Denominator);
    }

    [Fact]
    public void RationalMember_UnaryMinusAndMultiplication_SolveExactly()
    {
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem(new { x = default(Rational) })
            .Where(t => -t.x == Rational.Of(1, 3))
            .Where(t => t.x * t.x * Rational.Of(9, 1) == Rational.Of(1, 1))
            .Solve();

        Assert.NotNull(solution);
        Assert.Equal(Rational.Of(-1, 3), solution!.x);
    }

    [Fact]
    public void RationalMember_DivisionByNonZeroSymbol_SolvesExactly()
    {
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem(new { a = default(Rational), b = default(Rational) })
            .Where(t => t.b != Rational.Of(0, 1))
            .Where(t => t.a / t.b == Rational.Of(2, 1))
            .Solve();

        Assert.NotNull(solution);
        Assert.Equal(Rational.Of(2, 1), solution!.a / solution.b);
    }

    /// <summary>Positional records materialise through the constructor (ConstructFromModel).</summary>
    [Fact]
    public void RationalMember_PositionalRecord_SolvesAndMaterializes()
    {
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<RationalCell>()
            .Where(c => c.X > Rational.Of(1, 1))
            .Where(c => c.X < Rational.Of(2, 1))
            .Solve();

        Assert.NotNull(solution);
        double x = (double)solution!.X;
        Assert.True(x > 1 && x < 2, $"expected 1 < X < 2, got {x}");
    }

    /// <summary>The pre-defined Symbols environments carry rational symbols too.</summary>
    [Fact]
    public void RationalMember_SymbolsEnvironment_SolvesExactly()
    {
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<Symbols<Rational, Rational>>()
            .Where(s => s.X1 + s.X2 == Rational.Of(5, 6))
            .Where(s => s.X1 - s.X2 == Rational.Of(1, 6))
            .Solve();

        Assert.NotNull(solution);
        Assert.Equal(Rational.Of(1, 2), solution!.X1);
        Assert.Equal(Rational.Of(1, 3), solution.X2);
    }

    /// <summary>
    /// A model value outside the Int64 range must throw naming the symbol and the remedy, not a
    /// raw Z3 error (mirrors the DateTime and Int16 read-back guards).
    /// </summary>
    [Fact]
    public void RationalMember_ValueBeyondInt64_ThrowsNamingTheSymbol()
    {
        using var ctx = new Z3Context();

        var theorem = ctx.NewTheorem(new { x = default(Rational) })
            .Where(t => t.x > 1e30);

        var ex = Assert.Throws<OverflowException>(() => theorem.Solve());
        Assert.Contains("x", ex.Message);
        Assert.Contains("Int64", ex.Message);
    }
}
