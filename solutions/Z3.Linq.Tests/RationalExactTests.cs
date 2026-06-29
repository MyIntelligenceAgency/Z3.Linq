namespace Z3.Linq.Tests;

using Xunit;

/// <summary>
/// Regression tests for exact rational constants via <see cref="Rational"/> (DSL backlog item B7, #4616).
/// Real literals were emitted from a CLR <see cref="double"/> through <c>MkReal(value.ToString())</c>, which is
/// exact for terminating decimals but lossy for non-terminating rationals: <c>1.0 / 3.0</c> stringifies to a
/// 16-digit truncation, so <c>3 * x == 1</c> turns unsatisfiable. <see cref="Rational"/> carries the exact
/// <c>num/den</c> fraction into Z3, restoring satisfiability.
/// </summary>
public class RationalExactTests
{
    public class RealCell
    {
        public double X { get; set; }
    }

    public class MixedCell
    {
        public int N { get; set; }

        public double R { get; set; }
    }

    /// <summary>
    /// The centrepiece contrast. <c>x == 1/3</c> conjoined with <c>3 * x == 1</c> is satisfiable when 1/3 is an
    /// exact rational (3 * (1/3) == 1 holds exactly) but unsatisfiable when 1/3 comes from a CLR double (the
    /// stringified approximation does not multiply back to 1). The two halves of this test share identical
    /// structure and differ only in how the one-third constant is expressed.
    /// </summary>
    [Fact]
    public void ExactRational_SurvivesArithmetic_WhereDoubleApproximationDoesNot()
    {
        using var ctx = new Z3Context();

        // Exact: x == Rational(1,3) and 3*x == 1 -> satisfiable.
        var exact = ctx.NewTheorem<RealCell>()
            .Where(c => c.X == Rational.Of(1, 3))
            .Where(c => c.X * 3.0 == 1.0)
            .Solve();

        Assert.NotNull(exact);

        // Lossy: the same predicate with a CLR double 1.0/3.0 -> unsatisfiable.
        var approximate = ctx.NewTheorem<RealCell>()
            .Where(c => c.X == 1.0 / 3.0)
            .Where(c => c.X * 3.0 == 1.0)
            .Solve();

        Assert.Null(approximate);
    }

    /// <summary>
    /// A second non-terminating rational: <c>x == 2/7</c> with <c>7 * x == 2</c> is satisfiable exactly.
    /// </summary>
    [Fact]
    public void ExactRational_TwoSevenths_MultipliesBack()
    {
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<RealCell>()
            .Where(c => c.X == Rational.Of(2, 7))
            .Where(c => c.X * 7.0 == 2.0)
            .Solve();

        Assert.NotNull(solution);
    }

    /// <summary>
    /// The rational can sit on either side of a relational constraint over a real member; an inconsistent pair
    /// of exact bounds is unsatisfiable (<c>x &gt;= 1/2</c> and <c>x &lt; 1/3</c>).
    /// </summary>
    [Fact]
    public void ExactRational_AsRelationalBound_BothOrders()
    {
        using var ctx = new Z3Context();

        var feasible = ctx.NewTheorem<RealCell>()
            .Where(c => c.X >= Rational.Of(1, 2))
            .Where(c => Rational.Of(3, 4) >= c.X)
            .Solve();

        Assert.NotNull(feasible);
        Assert.InRange(feasible!.X, 0.5, 0.75);

        var infeasible = ctx.NewTheorem<RealCell>()
            .Where(c => c.X >= Rational.Of(1, 2))
            .Where(c => c.X < Rational.Of(1, 3))
            .Solve();

        Assert.Null(infeasible);
    }

    /// <summary>
    /// A negative / sign-normalised rational: <c>Rational(1, -2)</c> reduces to <c>-1/2</c>, and the equality
    /// constraint pins the real member to it.
    /// </summary>
    [Fact]
    public void Rational_NormalisesSignAndReduces()
    {
        Assert.Equal(-1, new Rational(1, -2).Numerator);
        Assert.Equal(2, new Rational(1, -2).Denominator);
        Assert.Equal(1, new Rational(2, 4).Numerator);
        Assert.Equal(2, new Rational(2, 4).Denominator);

        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<RealCell>()
            .Where(c => c.X == Rational.Of(3, -6)) // == -1/2
            .Where(c => c.X * 2.0 == -1.0)
            .Solve();

        Assert.NotNull(solution);
    }

    /// <summary>
    /// A zero denominator is rejected eagerly.
    /// </summary>
    [Fact]
    public void Rational_ZeroDenominator_Throws()
    {
        Assert.Throws<System.DivideByZeroException>(() => new Rational(1, 0));
    }

    /// <summary>
    /// Cast-matrix completion (B7): an int-to-real promotion (<c>c.R == c.N</c>, where C# inserts a
    /// <c>Convert(int -&gt; double)</c>) lifts the integer member to a real via MkInt2Real, so the mixed
    /// equality is solvable.
    /// </summary>
    [Fact]
    public void IntToReal_Promotion_IsSolvable()
    {
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<MixedCell>()
            .Where(c => c.N == 3)
            .Where(c => c.R == c.N)       // Convert(int -> double) -> MkInt2Real
            .Where(c => c.R == Rational.Of(3, 1))
            .Solve();

        Assert.NotNull(solution);
        Assert.Equal(3, solution!.N);
        Assert.Equal(3.0, solution.R, 6);
    }
}
