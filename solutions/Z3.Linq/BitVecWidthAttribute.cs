namespace Z3.Linq;

using System;

/// <summary>
/// Declares that an integral theorem variable is modelled as a fixed-width <b>bit-vector</b> of the given
/// <see cref="Width"/> rather than as a mathematical integer (gap B4 of the DSL backlog, #4616). Bit-vector
/// semantics are what make modular/overflow arithmetic expressible: <c>a + b</c> over an n-bit bit-vector
/// wraps modulo 2^n, so the classic overflow predicate <c>a + b &lt; a</c> becomes satisfiable — something the
/// unbounded integer theory can never reproduce.
/// <para>
/// Apply to an <see cref="int"/> (or <see cref="long"/>) property of an environment type. The DSL then binds
/// it to a Z3 bit-vector constant of <see cref="Width"/> bits, routes <c>+</c>/<c>-</c>/<c>*</c> through the
/// bit-vector adders/multipliers (<c>MkBVAdd</c>/<c>MkBVSub</c>/<c>MkBVMul</c>) and the relational operators
/// through the <b>unsigned</b> bit-vector comparisons (<c>MkBVULT</c>/<c>MkBVULE</c>/<c>MkBVUGT</c>/<c>MkBVUGE</c>),
/// and reads the model value back into the CLR integer. Values are interpreted as unsigned, so keep
/// <see cref="Width"/> wide enough for the magnitudes you expect (an n-bit variable ranges over 0..2^n-1).
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class BitVecWidthAttribute : Attribute
{
    /// <summary>Creates the attribute for a bit-vector of <paramref name="width"/> bits.</summary>
    /// <param name="width">The bit-vector width, in bits (must be strictly positive).</param>
    public BitVecWidthAttribute(uint width)
    {
        if (width == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Bit-vector width must be strictly positive.");
        }

        this.Width = width;
    }

    /// <summary>The width of the bit-vector, in bits.</summary>
    public uint Width { get; }
}
