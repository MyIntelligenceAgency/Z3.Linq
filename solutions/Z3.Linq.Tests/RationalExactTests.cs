namespace Z3.Linq.Tests;

#pragma warning disable CS1591

/// <summary>
/// Exact rational constants via <see cref="Rational"/>.
/// </summary>
/// <remarks>
/// A real literal reaches Z3 through <c>MkReal(value.ToString())</c>, which is exact for terminating
/// decimals and lossy for everything else. The contrast in this class is drawn on one-third, where
/// both forms are written side by side: the exact one multiplies back to 1, the double-derived one
/// does not.
/// </remarks>
[TestClass]
public class RationalExactTests
{
    /// <summary>One real-valued member.</summary>
    public class RealCell
    {
        /// <summary>Gets or sets the real value.</summary>
        public double X { get; set; }
    }

    /// <summary>An integer and a real member, for the promotion case.</summary>
    public class MixedCell
    {
        /// <summary>Gets or sets the integer value.</summary>
        public int N { get; set; }

        /// <summary>Gets or sets the real value.</summary>
        public double R { get; set; }
    }

    [TestMethod]
    public void ExactRational_SurvivesArithmetic_WhereDoubleApproximationDoesNot()
    {
        // The two halves have identical structure and differ only in how one-third is written, which
        // is what makes the pair a measurement rather than two assumptions.
        using var ctx = new Z3Context();

        var exact = ctx.NewTheorem<RealCell>()
            .Where(c => c.X == Rational.Of(1, 3))
            .Where(c => (c.X * 3.0) == 1.0)
            .Solve();

        exact.ShouldNotBeNull();

        var approximate = ctx.NewTheorem<RealCell>()
            .Where(c => c.X == 1.0 / 3.0)
            .Where(c => (c.X * 3.0) == 1.0)
            .Solve();

        approximate.ShouldBeNull();
    }

    [TestMethod]
    public void ExactRational_TwoSevenths_MultipliesBack()
    {
        // A second non-terminating fraction, so the first test's result is not read as a property of
        // one third in particular.
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<RealCell>()
            .Where(c => c.X == Rational.Of(2, 7))
            .Where(c => (c.X * 7.0) == 2.0)
            .Solve();

        solution.ShouldNotBeNull();
    }

    [TestMethod]
    public void ExactRational_AsRelationalBound_BothOrders()
    {
        using var ctx = new Z3Context();

        var feasible = ctx.NewTheorem<RealCell>()
            .Where(c => c.X >= Rational.Of(1, 2))
            .Where(c => Rational.Of(3, 4) >= c.X)
            .Solve();

        feasible.ShouldNotBeNull();
        feasible!.X.ShouldBeInRange(0.5, 0.75);

        var infeasible = ctx.NewTheorem<RealCell>()
            .Where(c => c.X >= Rational.Of(1, 2))
            .Where(c => c.X < Rational.Of(1, 3))
            .Solve();

        infeasible.ShouldBeNull();
    }

    [TestMethod]
    public void Rational_NormalisesSignAndReduces()
    {
        var negative = new Rational(1, -2);
        negative.Numerator.ShouldBe(-1);
        negative.Denominator.ShouldBe(2);

        var reducible = new Rational(2, 4);
        reducible.Numerator.ShouldBe(1);
        reducible.Denominator.ShouldBe(2);

        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<RealCell>()
            .Where(c => c.X == Rational.Of(3, -6))

            // Three sixths negative is minus one half; the arithmetic constraint confirms the value
            // rather than trusting the display.
            .Where(c => (c.X * 2.0) == -1.0)
            .Solve();

        solution.ShouldNotBeNull();
    }

    [TestMethod]
    public void Rational_ZeroDenominator_Throws()
    {
        Should.Throw<DivideByZeroException>(() => new Rational(1, 0));
    }

    [TestMethod]
    public void IntToReal_Promotion_IsSolvable()
    {
        // The compiler inserts Convert(int -> double) in c.R == c.N, which has to lift the integer
        // member to a real rather than refuse the cast.
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<MixedCell>()
            .Where(c => c.N == 3)
            .Where(c => c.R == c.N)
            .Where(c => c.R == Rational.Of(3, 1))
            .Solve();

        solution.ShouldNotBeNull();
        solution!.N.ShouldBe(3);
        solution.R.ShouldBe(3.0, 1e-6);
    }
}
