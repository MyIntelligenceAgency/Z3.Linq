namespace Z3.Linq;

using System;

/// <summary>
/// An <b>exact</b> rational constant for real-arithmetic constraints (DSL backlog item B7, #4616). Real
/// literals in the DSL were previously emitted as <c>MkReal(value.ToString())</c> from a CLR
/// <see cref="double"/> — fine for terminating decimals (<c>0.5</c>, <c>0.25</c>) but lossy for
/// non-terminating rationals: <c>1.0 / 3.0</c> stringifies to <c>"0.3333333333333333"</c>, i.e.
/// 3333333333333333/10^16, so <c>3 * x == 1</c> becomes <em>unsatisfiable</em>. A <see cref="Rational"/>
/// is carried into the expression tree as a constant of fraction form <c>num/den</c> and emitted to Z3 as an
/// exact rational, so <c>x == Rational.Of(1, 3)</c> together with <c>3 * x == 1</c> is satisfiable.
/// <para>
/// The struct defines comparison operators against <see cref="double"/> (both orders) so it can appear
/// directly in a <c>.Where</c> predicate over a real-valued member (<c>c.X &gt;= Rational.Of(1, 2)</c>); the
/// expression visitor recognises the <see cref="Rational"/> constant and emits the exact value (see
/// <c>ExpressionVisitor.VisitConstantValue</c>).
/// </para>
/// </summary>
public readonly struct Rational : IEquatable<Rational>
{
    /// <summary>The numerator, carrying the sign; the fraction is stored fully reduced.</summary>
    public long Numerator { get; }

    /// <summary>The denominator; always strictly positive and coprime with <see cref="Numerator"/>.</summary>
    public long Denominator { get; }

    /// <summary>Creates a reduced rational <paramref name="numerator"/>/<paramref name="denominator"/>.</summary>
    /// <param name="numerator">The numerator (may be negative).</param>
    /// <param name="denominator">The denominator (must be non-zero).</param>
    public Rational(long numerator, long denominator)
    {
        if (denominator == 0)
        {
            throw new DivideByZeroException("A rational cannot have a zero denominator.");
        }

        // Normalise the sign onto the numerator so the denominator is always positive.
        if (denominator < 0)
        {
            numerator = -numerator;
            denominator = -denominator;
        }

        long divisor = Gcd(Math.Abs(numerator), denominator);
        if (divisor == 0)
        {
            divisor = 1; // numerator == 0: reduce 0/d to 0/1
        }

        Numerator = numerator / divisor;
        Denominator = denominator / divisor;
    }

    /// <summary>Factory for a reduced rational <paramref name="numerator"/>/<paramref name="denominator"/>.</summary>
    public static Rational Of(long numerator, long denominator) => new(numerator, denominator);

    /// <summary>An integer is the rational n/1.</summary>
    public static implicit operator Rational(long value) => new(value, 1);

    /// <summary>An integer is the rational n/1.</summary>
    public static implicit operator Rational(int value) => new(value, 1);

    /// <summary>The (possibly lossy) floating-point value; explicit, since the whole point is to avoid silent doubles.</summary>
    public static explicit operator double(Rational value) => (double)value.Numerator / value.Denominator;

    /// <summary>The exact <c>num/den</c> fraction form, as parsed by Z3's <c>MkReal(string)</c>.</summary>
    public override string ToString() => $"{Numerator}/{Denominator}";

    public bool Equals(Rational other) => Numerator == other.Numerator && Denominator == other.Denominator;

    public override bool Equals(object? obj) => obj is Rational other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Numerator, Denominator);

    // Comparison operators against double, in both orders, so the rational can sit on either side of a
    // relational constraint over a real member. The bodies use the floating-point value (only relevant if the
    // predicate is ever evaluated in plain CLR); inside the DSL the visitor uses the exact fraction instead.
    public static bool operator ==(Rational left, double right) => (double)left == right;
    public static bool operator !=(Rational left, double right) => (double)left != right;
    public static bool operator <(Rational left, double right) => (double)left < right;
    public static bool operator <=(Rational left, double right) => (double)left <= right;
    public static bool operator >(Rational left, double right) => (double)left > right;
    public static bool operator >=(Rational left, double right) => (double)left >= right;

    public static bool operator ==(double left, Rational right) => left == (double)right;
    public static bool operator !=(double left, Rational right) => left != (double)right;
    public static bool operator <(double left, Rational right) => left < (double)right;
    public static bool operator <=(double left, Rational right) => left <= (double)right;
    public static bool operator >(double left, Rational right) => left > (double)right;
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
