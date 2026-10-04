namespace Z3.Linq.Tests;

/// <summary>
/// Tests that <see cref="DateTime"/>'s static fields - <see cref="DateTime.MaxValue"/>,
/// <see cref="DateTime.MinValue"/>, <see cref="DateTime.UnixEpoch"/> - work when written
/// <i>inline</i> in the constraint, as callers naturally do.
/// </summary>
/// <remarks>
/// <para>
/// Every DateTime test elsewhere in this suite smuggles the boundary in through a local
/// variable - <c>DateTime max = DateTime.MaxValue;</c> then <c>t.X1 == max</c>. A captured
/// local is a closure constant, the visitor already reduces constants, and the tests pass.
/// Written inline, <c>t.X1 == DateTime.MaxValue</c> is nothing of the sort: a static readonly
/// field is not a compile-time constant in C#, so the expression tree carries it as a member
/// access with no instance, and <c>VisitMember</c> answered
/// <c>NotSupportedException: Unknown parameter encountered: MaxValue.</c> - for the very
/// boundaries the DateTime work is about.
/// </para>
/// <para>
/// A static <see cref="System.Reflection.FieldInfo"/> that is <c>InitOnly</c> - readonly, assigned once in its
/// static initializer - has one value for the life of the process, so translating it as its
/// value is the same reduction the closure constant already gets. The fix reads the field and
/// hands it to the constant path.
/// </para>
/// <para>
/// The last test is the payoff: the read path (<c>Theorem.ToDateTime</c>) is written, and
/// documented, to answer a model beyond the DateTime range with an <see cref="OverflowException"/>
/// naming the symbol - but with the boundary unusable inline, that behaviour was unreachable
/// from the inline form. It threw <c>NotSupportedException</c> about a field instead.
/// </para>
/// </remarks>
[TestClass]
public class DateTimeStaticFieldsTests
{
    /// <summary>
    /// <see cref="DateTime.MaxValue"/> written inline in the constraint round-trips.
    /// </summary>
    [TestMethod]
    public void Solve_InlineDateTimeMaxValue_RoundTripsTheValue()
    {
        // Arrange
        using var context = new Z3Context();

        // Act: MaxValue written directly in the lambda - no local to hide behind.
        var result = context.NewTheorem<Symbols<DateTime, int>>()
            .Where(t => t.X1 == DateTime.MaxValue)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.Kind.ShouldBe(DateTimeKind.Utc);
        result.X1.ShouldBe(DateTime.MaxValue);
    }

    /// <summary>
    /// <see cref="DateTime.MinValue"/> written inline in the constraint round-trips.
    /// </summary>
    /// <remarks>
    /// Tick zero, the bottom of the range, and a different static field than the one above:
    /// the reduction has to read the field it was given, not special-case a known one.
    /// </remarks>
    [TestMethod]
    public void Solve_InlineDateTimeMinValue_RoundTripsTheValue()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<DateTime, int>>()
            .Where(t => t.X1 == DateTime.MinValue)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.Kind.ShouldBe(DateTimeKind.Utc);
        result.X1.ShouldBe(DateTime.MinValue);
    }

    /// <summary>
    /// <see cref="DateTime.UnixEpoch"/> written inline in the constraint round-trips.
    /// </summary>
    [TestMethod]
    public void Solve_InlineDateTimeUnixEpoch_RoundTripsTheValue()
    {
        // Arrange
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<Symbols<DateTime, int>>()
            .Where(t => t.X1 == DateTime.UnixEpoch)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.X1.Kind.ShouldBe(DateTimeKind.Utc);
        result.X1.ShouldBe(DateTime.UnixEpoch);
    }

    /// <summary>
    /// An inline <c>t.X1 &gt; DateTime.MaxValue</c> constraint reaches the documented read-path
    /// failure instead of dying on the field itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The DateTime symbol is an unbounded integer, so a bound beyond <see cref="DateTime.MaxValue"/>
    /// is satisfiable in Z3's integers with a model no <see cref="DateTime"/> can hold. The read
    /// path's contract for that model is an <see cref="OverflowException"/> naming the symbol -
    /// the same contract the captured-local form of this theorem already exercises in
    /// <c>SymbolTypeMarshallingTests</c>. Inline, that contract was unreachable: the theorem died
    /// at the field with <c>NotSupportedException: Unknown parameter encountered: MaxValue.</c>
    /// before Z3 was ever asked anything.
    /// </para>
    /// <para>
    /// The companion test pins both halves: the exception type the read path promises, and the
    /// name of the symbol in its message.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Solve_InlineDateTimeMaxValueInStrictBound_ThrowsOverflowExceptionNamingIt()
    {
        // Arrange
        using var context = new Z3Context();
        var theorem = context.NewTheorem<Symbols<DateTime, int>>()
            .Where(t => t.X1 > DateTime.MaxValue && t.X2 == 0);

        // Act
        OverflowException exception = Should.Throw<OverflowException>(() => theorem.Solve());

        // Assert
        exception.Message.ShouldContain("X1");
        exception.Message.ShouldContain("0001-01-01 to 9999-12-31");
    }
}
