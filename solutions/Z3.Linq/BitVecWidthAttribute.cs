namespace Z3.Linq;

using System;

/// <summary>
/// Declares that an integral theorem variable is a fixed-width bit-vector rather than a
/// mathematical integer.
/// </summary>
/// <remarks>
/// <para>
/// Bit-vector semantics are what make modular arithmetic expressible: <c>a + b</c> over an n-bit
/// bit-vector wraps modulo 2^n, so the classic overflow predicate <c>a + b &lt; a</c> becomes
/// satisfiable - something the unbounded integer theory can never reproduce, since there it is
/// simply false.
/// </para>
/// <para>
/// Applied to an integral member of an environment type, it binds that member to a bit-vector
/// constant of <see cref="Width"/> bits; <c>+</c>, <c>-</c> and <c>*</c> then go through the
/// bit-vector adders and multipliers, and the relational operators through the <b>unsigned</b>
/// bit-vector comparisons. The value read back is the unsigned one, so a width has to cover the
/// magnitudes the theorem expects: an n-bit variable ranges over 0 to 2^n - 1.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class BitVecWidthAttribute : Attribute
{
    /// <summary>
    /// Initialises a new instance of the <see cref="BitVecWidthAttribute"/> class.
    /// </summary>
    /// <param name="width">The width, in bits, which must be strictly positive.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> is zero.</exception>
    public BitVecWidthAttribute(uint width)
    {
        if (width == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Bit-vector width must be strictly positive.");
        }

        this.Width = width;
    }

    /// <summary>Gets the width of the bit-vector, in bits.</summary>
    public uint Width { get; }
}
