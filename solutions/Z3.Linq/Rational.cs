namespace Z3.Linq;

using System;

/// <summary>
/// An exact rational constant, for real-arithmetic constraints that a CLR <see cref="double"/>
/// cannot state.
/// </summary>
/// <remarks>
/// <para>
/// Real literals reach Z3 through <c>MkReal(value.ToString())</c>. That is exact for terminating
/// decimals - <c>0.5</c>, <c>0.25</c> - and lossy for everything else: <c>1.0 / 3.0</c> stringifies
/// to a sixteen-digit truncation, which Z3 reads as 3333333333333333/10^16, so <c>3 * x == 1</c>
/// becomes unsatisfiable even though the constraint is plainly solvable.
/// </para>
/// <para>
/// A value of this type is carried into the expression tree as the fraction <c>num/den</c> and
/// emitted to Z3 as an exact rational, so <c>x == Rational.Of(1, 3)</c> conjoined with
/// <c>3 * x == 1</c> holds.
/// </para>
/// <para>
/// The comparison operators against <see cref="double"/> exist in both orders so a rational can sit
/// on either side of a constraint over a real-valued member, as in <c>c.X &gt;= Rational.Of(1, 2)</c>.
/// Their bodies use the floating-point value, which only matters if the predicate is ever evaluated
/// as plain CLR; inside a theorem the visitor emits the exact fraction instead.
/// </para>
/// </remarks>
public readonly struct Rational : IEquatable<Rational>
{
    /// <summary>Gets the numerator, which carries the sign.</summary>
    /// <remarks>The fraction is stored fully reduced, so this is coprime with the denominator.</remarks>
    public long Numerator { get; }

    /// <summary>Gets the denominator, which is always strictly positive.</summary>
    public long Denominator { get; }

    /// <summary>
    /// Initialises a new instance of the <see cref="Rational"/> struct, reduced.
    /// </summary>
    /// <param name="numerator">The numerator, which may be negative.</param>
    /// <param name="denominator">The denominator, which must not be zero.</param>
    /// <exception cref="DivideByZeroException">The denominator is zero.</exception>
    public Rational(long numerator, long denominator)
    {
        if (denominator == 0)
        {
            throw new DivideByZeroException("A rational cannot have a zero denominator.");
        }

        // The sign is normalised onto the numerator, so the denominator is always positive and two
        // equal values always have the same representation.
        if (denominator < 0)
        {
            numerator = -numerator;
            denominator = -denominator;
        }

        long divisor = Gcd(Math.Abs(numerator), denominator);

        // Gcd(0, d) is d, and a zero numerator reduces to 0/1 either way; the guard keeps a
        // hypothetical zero divisor from turning into a division by zero of its own.
        if (divisor == 0)
        {
            divisor = 1;
        }

        this.Numerator = numerator / divisor;
        this.Denominator = denominator / divisor;
    }

    /// <summary>
    /// Creates a reduced rational.
    /// </summary>
    /// <param name="numerator">The numerator, which may be negative.</param>
    /// <param name="denominator">The denominator, which must not be zero.</param>
    /// <returns>The reduced rational.</returns>
    public static Rational Of(long numerator, long denominator) => new(numerator, denominator);

    /// <summary>Converts an integer to the rational it equals.</summary>
    /// <param name="value">The integer.</param>
    public static implicit operator Rational(long value) => new(value, 1);

    /// <summary>Converts an integer to the rational it equals.</summary>
    /// <param name="value">The integer.</param>
    public static implicit operator Rational(int value) => new(value, 1);

    /// <summary>
    /// Converts to the nearest floating-point value.
    /// </summary>
    /// <param name="value">The rational.</param>
    /// <remarks>Explicit rather than implicit: the point of the type is to avoid silent rounding.</remarks>
    public static explicit operator double(Rational value) => (double)value.Numerator / value.Denominator;

    /// <summary>
    /// Formats the exact fraction, <c>num/den</c>.
    /// </summary>
    /// <returns>The fraction, in the form Z3's <c>MkReal(string)</c> parses as a rational.</returns>
    public override string ToString() => $"{this.Numerator}/{this.Denominator}";

    /// <summary>
    /// Tests two rationals for equality.
    /// </summary>
    /// <param name="other">The other rational.</param>
    /// <returns>Whether the two are the same value.</returns>
    public bool Equals(Rational other) => this.Numerator == other.Numerator && this.Denominator == other.Denominator;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Rational other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(this.Numerator, this.Denominator);

    /// <summary>Tests a rational and a floating-point value for equality.</summary>
    /// <param name="left">The rational.</param>
    /// <param name="right">The floating-point value.</param>
    /// <returns>Whether the two are equal.</returns>
    public static bool operator ==(Rational left, double right) => (double)left == right;

    /// <summary>Tests a rational and a floating-point value for inequality.</summary>
    /// <param name="left">The rational.</param>
    /// <param name="right">The floating-point value.</param>
    /// <returns>Whether the two differ.</returns>
    public static bool operator !=(Rational left, double right) => (double)left != right;

    /// <summary>Tests whether a rational is below a floating-point value.</summary>
    /// <param name="left">The rational.</param>
    /// <param name="right">The floating-point value.</param>
    /// <returns>Whether <paramref name="left"/> is below <paramref name="right"/>.</returns>
    public static bool operator <(Rational left, double right) => (double)left < right;

    /// <summary>Tests whether a rational is at or below a floating-point value.</summary>
    /// <param name="left">The rational.</param>
    /// <param name="right">The floating-point value.</param>
    /// <returns>Whether <paramref name="left"/> is at or below <paramref name="right"/>.</returns>
    public static bool operator <=(Rational left, double right) => (double)left <= right;

    /// <summary>Tests whether a rational is above a floating-point value.</summary>
    /// <param name="left">The rational.</param>
    /// <param name="right">The floating-point value.</param>
    /// <returns>Whether <paramref name="left"/> is above <paramref name="right"/>.</returns>
    public static bool operator >(Rational left, double right) => (double)left > right;

    /// <summary>Tests whether a rational is at or above a floating-point value.</summary>
    /// <param name="left">The rational.</param>
    /// <param name="right">The floating-point value.</param>
    /// <returns>Whether <paramref name="left"/> is at or above <paramref name="right"/>.</returns>
    public static bool operator >=(Rational left, double right) => (double)left >= right;

    /// <summary>Tests a floating-point value and a rational for equality.</summary>
    /// <param name="left">The floating-point value.</param>
    /// <param name="right">The rational.</param>
    /// <returns>Whether the two are equal.</returns>
    public static bool operator ==(double left, Rational right) => left == (double)right;

    /// <summary>Tests a floating-point value and a rational for inequality.</summary>
    /// <param name="left">The floating-point value.</param>
    /// <param name="right">The rational.</param>
    /// <returns>Whether the two differ.</returns>
    public static bool operator !=(double left, Rational right) => left != (double)right;

    /// <summary>Tests whether a floating-point value is below a rational.</summary>
    /// <param name="left">The floating-point value.</param>
    /// <param name="right">The rational.</param>
    /// <returns>Whether <paramref name="left"/> is below <paramref name="right"/>.</returns>
    public static bool operator <(double left, Rational right) => left < (double)right;

    /// <summary>Tests whether a floating-point value is at or below a rational.</summary>
    /// <param name="left">The floating-point value.</param>
    /// <param name="right">The rational.</param>
    /// <returns>Whether <paramref name="left"/> is at or below <paramref name="right"/>.</returns>
    public static bool operator <=(double left, Rational right) => left <= (double)right;

    /// <summary>Tests whether a floating-point value is above a rational.</summary>
    /// <param name="left">The floating-point value.</param>
    /// <param name="right">The rational.</param>
    /// <returns>Whether <paramref name="left"/> is above <paramref name="right"/>.</returns>
    public static bool operator >(double left, Rational right) => left > (double)right;

    /// <summary>Tests whether a floating-point value is at or above a rational.</summary>
    /// <param name="left">The floating-point value.</param>
    /// <param name="right">The rational.</param>
    /// <returns>Whether <paramref name="left"/> is at or above <paramref name="right"/>.</returns>
    public static bool operator >=(double left, Rational right) => left >= (double)right;

    private static long Gcd(long a, long b)
    {
        while (b != 0)
        {
            (a, b) = (b, a % b);
        }

        return a;
    }
}
