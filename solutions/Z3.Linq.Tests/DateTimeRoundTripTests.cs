namespace Z3.Linq.Tests;

/// <summary>
/// Round-trip coverage for <see cref="DateTime"/> symbols, ported from the downstream fork's
/// reproducing suite (MyIntelligenceAgency/Z3.Linq, the #14445 investigation) and aimed at
/// this branch's ticks encoding. The suite was written against the defect this PR fixes -
/// a DateTime that travelled as a Windows file time could express nothing before 1601, and
/// a read beyond the range blamed a <c>fileTime</c> parameter no caller supplied - so it
/// double-checks the fix from the outside: same theorems, independently chosen shapes. The
/// environments are declared locally and name their symbol <c>D</c> so the overflow guard
/// is pinned on a second name, not only the <c>X1</c> of
/// <c>SymbolTypeMarshallingTests</c>.
/// </summary>
[TestClass]
public class DateTimeRoundTripTests
{
    private sealed class DateTimeBag
    {
        public DateTime D { get; set; }
    }

    private sealed class DateTimeListBag
    {
        public List<DateTime> Values { get; set; } = [default, default];

        public int Length { get; set; }
    }

    /// <summary>
    /// An instant the file-time encoding could not write round-trips exactly.
    /// </summary>
    [TestMethod]
    public void Solve_DateTimeBefore1601_RoundTrips()
    {
        // Arrange: 1500-01-01 is 101 years before the file-time epoch, so the write path
        // threw ArgumentOutOfRangeException before Z3 ever saw the theorem.
        var instant = new DateTime(1500, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<DateTimeBag>()
            .Where(b => b.D == instant)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.D.ShouldBe(instant);
        result.D.Kind.ShouldBe(DateTimeKind.Utc);
    }

    /// <summary>
    /// A present-day instant round-trips with both its ticks and its kind intact.
    /// </summary>
    /// <remarks>
    /// The ticks and the kind are asserted separately on purpose: comparing the values alone
    /// would pass under a local-time read on a UTC runner, and comparing kinds alone would
    /// pass under any shifted instant. Before the fork fixed #56 on its side, the read came
    /// back <see cref="DateTimeKind.Local"/> with the machine's offset baked in.
    /// </remarks>
    [TestMethod]
    public void Solve_DateTime_RoundTripsAsUtcWithSameTicks()
    {
        var instant = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<DateTimeBag>()
            .Where(b => b.D == instant)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.D.Kind.ShouldBe(DateTimeKind.Utc);
        result.D.Ticks.ShouldBe(instant.Ticks);
    }

    /// <summary>
    /// The element read has its own DateTime arm: an instant before 1601 inside a
    /// <c>List{DateTime}</c> round-trips like the scalar does.
    /// </summary>
    [TestMethod]
    public void Solve_DateTimeListBefore1601_RoundTripsEveryElement()
    {
        // Arrange: the Gregorian calendar's first day, as it happens - 1582-10-15.
        var instant = new DateTime(1582, 10, 15, 0, 0, 0, DateTimeKind.Utc);
        using var context = new Z3Context();

        // Act
        var result = context.NewTheorem<DateTimeListBag>()
            .Where(b => b.Values[0] == instant)
            .Where(b => b.Length == 2)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result.Values[0].ShouldBe(instant);
        result.Values[0].Kind.ShouldBe(DateTimeKind.Utc);
    }

    /// <summary>
    /// A model value no DateTime can hold throws <see cref="OverflowException"/> naming the
    /// symbol, whatever the symbol is called.
    /// </summary>
    /// <remarks>
    /// The symbol is an unbounded integer to Z3, so <c>b.D &gt; max</c> is satisfiable with
    /// ticks beyond the type. The read path guards the range and says which symbol chose the
    /// value (#87 is the follow-up to bound it properly); the assertion checks the name
    /// <c>D</c> reaches the message, complementing the X1 pin in
    /// <c>SymbolTypeMarshallingTests</c>. Like that test, the bound is captured into a local
    /// first: a static field member in the tree is not partial-evaluated away on this branch.
    /// </remarks>
    [TestMethod]
    public void Solve_DateTimeBeyondRange_ThrowsNamingTheSymbol()
    {
        // Arrange
        DateTime max = DateTime.MaxValue;
        using var context = new Z3Context();

        // Act
        OverflowException exception = Should.Throw<OverflowException>(() =>
            context.NewTheorem<DateTimeBag>()
                .Where(b => b.D > max)
                .Solve());

        // Assert
        exception.Message.ShouldContain("D");
    }
}
