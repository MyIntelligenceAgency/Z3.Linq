namespace Z3.Linq.Tests;

#pragma warning disable CS1591

/// <summary>
/// Round trips for <see cref="DateTime"/> symbols: an instant on the UTC timeline, both ways.
/// </summary>
/// <remarks>
/// An instant is encoded as ticks on the UTC timeline. Encoding it as a Windows file time instead
/// (counted from 1601-01-01) meant nothing earlier could be written at all, and decoding it as local
/// time came back <see cref="DateTimeKind.Local"/> with the machine's UTC offset baked in - so the
/// same theorem answered differently on two runners. Both assertions below are needed: a Kind
/// assertion fails everywhere, and a ticks assertion fails on every runner whose offset is not zero.
/// </remarks>
[TestClass]
public class DateTimeRoundTripTests
{
    [TestMethod]
    public void Solve_DateTimeBefore1601_RoundTrips()
    {
        var instant = new DateTime(1500, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<DateTimeBag>()
            .Where(b => b.D == instant)
            .Solve();

        solution.ShouldNotBeNull();
        solution!.D.ShouldBe(instant);
        solution.D.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [TestMethod]
    public void Solve_DateTime_RoundTripsAsUtcWithSameTicks()
    {
        var instant = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<DateTimeBag>()
            .Where(b => b.D == instant)
            .Solve();

        solution.ShouldNotBeNull();
        solution!.D.Kind.ShouldBe(DateTimeKind.Utc);
        solution.D.Ticks.ShouldBe(instant.Ticks);
    }

    [TestMethod]
    public void Solve_DateTimeListBefore1601_RoundTripsEveryElement()
    {
        // A collection element is read through its own DateTime arm, so the encoding has to hold
        // there too, not only on the scalar path.
        var instant = new DateTime(1582, 10, 15, 0, 0, 0, DateTimeKind.Utc);
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<DateTimeListBag>()
            .Where(b => b.Values[0] == instant)
            .Solve();

        solution.ShouldNotBeNull();
        solution!.Values[0].ShouldBe(instant);
        solution.Values[0].Kind.ShouldBe(DateTimeKind.Utc);
    }

    [TestMethod]
    public void Solve_DateTimeBeyondRange_IsUnsatisfiable()
    {
        // A scalar symbol is bounded to its type's range, so an instant past DateTime.MaxValue is
        // "no model" rather than a model whose ticks no DateTime can hold.
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<DateTimeBag>()
            .Where(b => b.D > DateTime.MaxValue)
            .Solve();

        solution.ShouldBeNull();
    }

    [TestMethod]
    public void Solve_DateTimeInListBeyondRange_ThrowsNamingTheSymbol()
    {
        // The bounds reach scalars only: a collection is an array from Int to the element sort and
        // its length is not known when the theorem is built, so the element read stays a checked
        // conversion. This pins that read - dropping it would leave the element path uncovered.
        using var ctx = new Z3Context();

        var exception = Should.Throw<OverflowException>(() =>
            ctx.NewTheorem<DateTimeListBag>()
                .Where(b => b.Values[0] > DateTime.MaxValue)
                .Solve());

        exception.Message.ShouldContain("Values");
    }

    /// <summary>One instant.</summary>
    public class DateTimeBag
    {
        /// <summary>Gets or sets the instant.</summary>
        public DateTime D { get; set; }
    }

    /// <summary>Two instants, for the element path.</summary>
    public class DateTimeListBag
    {
        /// <summary>Gets or sets the instants.</summary>
        public List<DateTime> Values { get; set; } = [default, default];
    }
}
