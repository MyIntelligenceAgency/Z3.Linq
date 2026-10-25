namespace Z3.Linq.Tests;

#pragma warning disable CS1591

/// <summary>
/// Fixed-width bit-vector variables declared with <c>[BitVecWidth(n)]</c>.
/// </summary>
/// <remarks>
/// Every integral theorem variable is otherwise a mathematical (unbounded) integer, so modular and
/// overflow arithmetic - the entire point of fixed-width machine integers - is inexpressible. The
/// attribute binds a property to an n-bit Z3 bit-vector, routes <c>+</c>/<c>-</c>/<c>*</c> through the
/// modular bit-vector operators and the relational operators through the unsigned bit-vector
/// comparisons.
/// <para>
/// The centrepiece is <see cref="OverflowPredicate_IsSat_OnBitVector_ButUnsat_OnInteger"/>: the
/// predicate <c>a + b &lt; a</c> with <c>b &gt;= 1</c> is satisfiable under bit-vectors (the sum wraps)
/// yet unsatisfiable under integers (a natural sum never shrinks). A trivial example would not
/// exercise this distinction; the pair makes the bit-vector engine's distinctive capability visible
/// in the result.
/// </para>
/// </remarks>
[TestClass]
public class BitVectorTheoryTests
{
    [TestMethod]
    public void OverflowPredicate_IsSat_OnBitVector_ButUnsat_OnInteger()
    {
        using var ctx = new Z3Context();

        // Bit-vector: overflow is reachable.
        var bitVectorSolution = ctx.NewTheorem<Reg4>()
            .Where(r => r.B >= 1)
            .Where(r => r.A + r.B < r.A) // wraps modulo 16, so the sum lands below A
            .Solve();

        bitVectorSolution.ShouldNotBeNull();
        bitVectorSolution!.A.ShouldBeInRange(0, 15);
        bitVectorSolution.B.ShouldBeInRange(1, 15);
        ((bitVectorSolution.A + bitVectorSolution.B) % 16 < bitVectorSolution.A)
            .ShouldBeTrue("the witnessed sum must wrap below A");

        // Integer theory: the same predicate (with non-negative operands) is unsatisfiable - no
        // overflow exists, so the pair above is not something a plain integer theorem could answer.
        var integerSolution = ctx.NewTheorem<PlainPair>()
            .Where(p => p.A >= 0)
            .Where(p => p.B >= 1)
            .Where(p => p.A + p.B < p.A)
            .Solve();

        integerSolution.ShouldBeNull();
    }

    [TestMethod]
    public void ModularAddition_WrapsToZero()
    {
        // Over 4 bits, 15 + 1 == 0 (16 wraps to 0). The DSL must accept the equality against the
        // wrapped literal and return the pinned operands.
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<Reg4>()
            .Where(r => r.A == 15)
            .Where(r => r.B == 1)
            .Where(r => r.A + r.B == 0)
            .Solve();

        solution.ShouldNotBeNull();
        solution!.A.ShouldBe(15);
        solution.B.ShouldBe(1);
    }

    [TestMethod]
    public void ModularMultiplication_WrapsToZero()
    {
        // Multiplication wraps too: over 4 bits, 4 * 4 == 16 == 0. A non-zero product collapsing to
        // zero is only possible under modular semantics.
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<Reg4>()
            .Where(r => r.A == 4)
            .Where(r => r.B == 4)
            .Where(r => r.A * r.B == 0)
            .Solve();

        solution.ShouldNotBeNull();
        solution!.A.ShouldBe(4);
        solution.B.ShouldBe(4);
    }

    [TestMethod]
    public void NoOverflow_WhenSumFitsInWidth()
    {
        // Away from the boundary the operators behave like ordinary arithmetic: 3 + 4 = 7 >= 3.
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<Reg4>()
            .Where(r => r.A == 3)
            .Where(r => r.B == 4)
            .Where(r => r.A + r.B >= r.A)
            .Solve();

        solution.ShouldNotBeNull();
        solution!.A.ShouldBe(3);
        solution.B.ShouldBe(4);
    }

    [TestMethod]
    public void IntegerLiteral_IsCoercedToBitVectorWidth()
    {
        // A literal carries no sort of its own, so it has to be coerced to the width of the
        // bit-vector it faces. A > 8 restricts a 4-bit value to 9..15.
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<Reg4>()
            .Where(r => r.A > 8)
            .Where(r => r.B == 0)
            .Solve();

        solution.ShouldNotBeNull();
        (solution!.A > 8 && solution.A <= 15).ShouldBeTrue($"Expected A in 9..15, got {solution.A}.");
        solution.B.ShouldBe(0);
    }

    [TestMethod]
    public void DeclaredWidth_IsRespected_OnReadBack()
    {
        // The width is honoured on the way out too: 200 survives intact on an 8-bit register, where
        // a 4-bit one would have wrapped it to 8.
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<Reg8>()
            .Where(r => r.X == 200)
            .Solve();

        solution.ShouldNotBeNull();
        solution!.X.ShouldBe(200);
    }

    [TestMethod]
    public void UnsignedComparison_HasNoWitnessAboveMax()
    {
        // Unsigned semantics: over 4 bits nothing is strictly greater than 15, so an out-of-range
        // lower bound has no witness.
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<Reg4>()
            .Where(r => r.A > 15)
            .Solve();

        solution.ShouldBeNull();
    }

    /// <summary>A pair of 4-bit unsigned registers (values 0..15, arithmetic modulo 16).</summary>
    public class Reg4
    {
        /// <summary>Gets or sets the first register.</summary>
        [BitVecWidth(4)]
        public int A { get; set; }

        /// <summary>Gets or sets the second register.</summary>
        [BitVecWidth(4)]
        public int B { get; set; }
    }

    /// <summary>An 8-bit register (values 0..255).</summary>
    public class Reg8
    {
        /// <summary>Gets or sets the register.</summary>
        [BitVecWidth(8)]
        public int X { get; set; }
    }

    /// <summary>A plain integer pair: the mathematical-integer foil for the overflow predicate.</summary>
    public class PlainPair
    {
        /// <summary>Gets or sets the first integer.</summary>
        public int A { get; set; }

        /// <summary>Gets or sets the second integer.</summary>
        public int B { get; set; }
    }
}
