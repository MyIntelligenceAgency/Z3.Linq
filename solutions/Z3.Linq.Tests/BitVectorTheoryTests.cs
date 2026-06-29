namespace Z3.Linq.Tests;

using Xunit;

/// <summary>
/// Regression tests for fixed-width bit-vector variables via <c>[BitVecWidth(n)]</c> (DSL backlog item B4,
/// #4616). Before B4 every integral theorem variable was a mathematical (unbounded) integer, so modular and
/// overflow arithmetic — the entire point of fixed-width machine integers — was inexpressible. B4 binds a
/// <c>[BitVecWidth(n)]</c> property to an n-bit Z3 bit-vector, routes <c>+</c>/<c>-</c>/<c>*</c> through the
/// modular bit-vector operators and the relational operators through the unsigned bit-vector comparisons.
///
/// The centrepiece is <see cref="OverflowPredicate_IsSat_OnBitVector_ButUnsat_OnInteger"/>: the predicate
/// <c>a + b &lt; a</c> with <c>b &gt;= 1</c> is satisfiable under bit-vectors (the sum wraps) yet unsatisfiable
/// under integers (a natural sum never shrinks). A trivial example would not exercise this distinction; the
/// pair makes the bit-vector engine's distinctive capability visible in the result.
/// </summary>
public class BitVectorTheoryTests
{
    /// <summary>A pair of 4-bit unsigned registers (values 0..15, arithmetic modulo 16).</summary>
    public class Reg4
    {
        [BitVecWidth(4)]
        public int A { get; set; }

        [BitVecWidth(4)]
        public int B { get; set; }
    }

    /// <summary>An 8-bit register (values 0..255).</summary>
    public class Reg8
    {
        [BitVecWidth(8)]
        public int X { get; set; }
    }

    /// <summary>A plain integer pair: the mathematical-integer foil for the bit-vector overflow predicate.</summary>
    public class PlainPair
    {
        public int A { get; set; }

        public int B { get; set; }
    }

    /// <summary>
    /// The defining property of fixed-width arithmetic: addition wraps. <c>a + b &lt; a</c> with <c>b &gt;= 1</c>
    /// is satisfiable over bit-vectors (the sum overflows the 4-bit range and wraps below <c>a</c>) but
    /// unsatisfiable over mathematical integers (a sum with a positive addend never decreases). This is the
    /// non-trivial problem that distinguishes the bit-vector theory from the integer theory.
    /// </summary>
    [Fact]
    public void OverflowPredicate_IsSat_OnBitVector_ButUnsat_OnInteger()
    {
        using var ctx = new Z3Context();

        // Bit-vector: overflow is reachable.
        var bvSolution = ctx.NewTheorem<Reg4>()
            .Where(r => r.B >= 1)
            .Where(r => r.A + r.B < r.A) // wraps modulo 16 => sum lands below A
            .Solve();

        Assert.NotNull(bvSolution);
        Assert.InRange(bvSolution!.A, 0, 15);
        Assert.InRange(bvSolution.B, 1, 15);
        Assert.True((bvSolution.A + bvSolution.B) % 16 < bvSolution.A, "the witnessed sum must wrap below A");

        // Integer theory: the same predicate (with non-negative operands) is unsatisfiable — no overflow exists.
        var intSolution = ctx.NewTheorem<PlainPair>()
            .Where(p => p.A >= 0)
            .Where(p => p.B >= 1)
            .Where(p => p.A + p.B < p.A)
            .Solve();

        Assert.Null(intSolution);
    }

    /// <summary>
    /// Exact modular value: over 4 bits, <c>15 + 1 == 0</c> (16 wraps to 0). The DSL must accept the equality
    /// against the wrapped literal and return the pinned operands.
    /// </summary>
    [Fact]
    public void ModularAddition_WrapsToZero()
    {
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<Reg4>()
            .Where(r => r.A == 15)
            .Where(r => r.B == 1)
            .Where(r => r.A + r.B == 0) // 16 mod 16 == 0
            .Solve();

        Assert.NotNull(solution);
        Assert.Equal(15, solution!.A);
        Assert.Equal(1, solution.B);
    }

    /// <summary>
    /// Multiplication wraps too: over 4 bits, <c>4 * 4 == 16 == 0</c>. A non-zero product collapsing to zero
    /// is only possible under modular semantics.
    /// </summary>
    [Fact]
    public void ModularMultiplication_WrapsToZero()
    {
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<Reg4>()
            .Where(r => r.A == 4)
            .Where(r => r.B == 4)
            .Where(r => r.A * r.B == 0) // 16 mod 16 == 0
            .Solve();

        Assert.NotNull(solution);
        Assert.Equal(4, solution!.A);
        Assert.Equal(4, solution.B);
    }

    /// <summary>
    /// When the sum stays within range there is no wrap, and the no-overflow predicate <c>a + b &gt;= a</c>
    /// holds — confirming the operators behave like ordinary arithmetic away from the boundary.
    /// </summary>
    [Fact]
    public void NoOverflow_WhenSumFitsInWidth()
    {
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<Reg4>()
            .Where(r => r.A == 3)
            .Where(r => r.B == 4)
            .Where(r => r.A + r.B >= r.A) // 7 >= 3, no wrap
            .Solve();

        Assert.NotNull(solution);
        Assert.Equal(3, solution!.A);
        Assert.Equal(4, solution.B);
    }

    /// <summary>
    /// An integer literal in a relational constraint is coerced to a width-matched bit-vector, and the
    /// comparison uses unsigned bit-vector ordering. <c>A &gt; 8</c> restricts a 4-bit value to 9..15.
    /// </summary>
    [Fact]
    public void IntegerLiteral_IsCoercedToBitVectorWidth()
    {
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<Reg4>()
            .Where(r => r.A > 8)
            .Where(r => r.B == 0)
            .Solve();

        Assert.NotNull(solution);
        Assert.True(solution!.A > 8 && solution.A <= 15);
        Assert.Equal(0, solution.B);
    }

    /// <summary>
    /// The declared width is honored at read-back: a value of 200 round-trips intact on an 8-bit register
    /// (it would have wrapped to 8 on a 4-bit one), proving the bit-vector sort carries the attribute's width.
    /// </summary>
    [Fact]
    public void DeclaredWidth_IsRespected_OnReadBack()
    {
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<Reg8>()
            .Where(r => r.X == 200)
            .Solve();

        Assert.NotNull(solution);
        Assert.Equal(200, solution!.X); // 200 needs 8 bits; survives intact
    }

    /// <summary>
    /// Unsigned comparison semantics: over 4 bits there is no value strictly greater than 15, so an
    /// out-of-range lower bound is unsatisfiable. <c>A &gt; 15</c> has no 4-bit witness.
    /// </summary>
    [Fact]
    public void UnsignedComparison_HasNoWitnessAboveMax()
    {
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<Reg4>()
            .Where(r => r.A > 15) // 15 is the 4-bit maximum; nothing exceeds it
            .Solve();

        Assert.Null(solution);
    }
}
