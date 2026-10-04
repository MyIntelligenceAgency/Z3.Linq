namespace Z3.Linq.Tests;

/// <summary>
/// Round-trip coverage for <see cref="short"/> elements inside collections, ported from the
/// downstream fork's Int16 suite (MyIntelligenceAgency/Z3.Linq, the #17302 investigation) and
/// aimed at this branch's short-symbols fix.
/// </summary>
/// <remarks>
/// <para>
/// The scalar arm of the downstream suite - a short property round-tripping at its boundaries -
/// is already covered by the <c>DataRow</c> set of
/// <c>SymbolTypeMarshallingTests.Solve_ShortSymbol_RoundTripsTheValue</c>, so it is not
/// duplicated here. What the downstream investigation added on top is the element read: an
/// element goes through its own arm of the marshalling switch, and the #63 fix had to reach it
/// separately from the property read. This file pins that arm on a <c>List{short}</c>.
/// </para>
/// <para>
/// On this branch a scalar symbol is not yet bounded to the range of its type (#87, #98), so an
/// element constrained outside the range stays satisfiable and meets the type only on the way
/// out - both halves of the arm are worth pinning: the value that fits must come back exact,
/// and the value that does not must fail loudly rather than wrap.
/// </para>
/// </remarks>
[TestClass]
public class Int16RoundTripTests
{
    private sealed class ShortListBag
    {
        public List<short> Values { get; set; } = [default, default];
    }

    /// <summary>
    /// Both ends of the type round-trip through the element read of a <c>List{short}</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both ends are pinned in one theorem so a wide read - an <see cref="int"/> handed to a
    /// short element through reflection - cannot pass by luck of the value.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Solve_ShortListElements_RoundTripBothEndsOfTheType()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<ShortListBag>()
            .Where(b => b.Values[0] == short.MinValue && b.Values[1] == short.MaxValue)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Values[0].ShouldBe(short.MinValue);
        result.Values[1].ShouldBe(short.MaxValue);
    }

    /// <summary>
    /// A <c>short</c> element whose model value no <c>short</c> can hold fails loudly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Same spelling as the scalar case of
    /// <c>SymbolTypeMarshallingTests.Solve_ShortSymbolConstrainedOutsideShortRange_ThrowsOverflowException</c>,
    /// one collection deep: C# blocks the direct comparison against a short, so it takes an
    /// <c>int</c> local to reach, the element symbol is an unbounded integer to Z3, and the
    /// checked conversion on the way out is the whole point - an unchecked one would wrap and
    /// hand back a wrong answer that looks like a right one.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Solve_ShortListElementConstrainedOutsideShortRange_ThrowsOverflowException()
    {
        // Arrange
        using var context = new Z3Context();
        int beyondShortRange = 40000;
        var theorem = context.NewTheorem<ShortListBag>()
            .Where(b => b.Values[0] > beyondShortRange);

        // Act & Assert
        Should.Throw<OverflowException>(() => theorem.Solve());
    }
}
