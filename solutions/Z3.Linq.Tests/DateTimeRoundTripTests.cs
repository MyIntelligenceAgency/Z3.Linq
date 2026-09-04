namespace Z3.Linq.Tests;

using System;
using System.Collections.Generic;
using Xunit;

/// <summary>
/// Round-trip regression tests for <see cref="DateTime"/> symbols (#14445).
/// The write path encoded an instant as a Windows file time (<c>ToFileTimeUtc</c>, counted
/// from 1601-01-01) and the read path decoded with <c>FromFileTime</c> (local time): nothing
/// before 1601 could be written (<see cref="ArgumentOutOfRangeException"/>), and a read-back
/// came out <see cref="DateTimeKind.Local"/> with the machine's UTC offset baked in. Port of
/// endjin/Z3.Linq#95 (their #56/#83): ticks on the UTC timeline both ways.
/// </summary>
public class DateTimeRoundTripTests
{
    public class DateTimeBag
    {
        public DateTime D { get; set; }
    }

    public class DateTimeListBag
    {
        public List<DateTime> Values { get; set; } = new() { default, default };
    }

    [Fact]
    public void Solve_DateTimeBefore1601_RoundTrips()
    {
        // ToFileTimeUtc threw ArgumentOutOfRangeException for any instant before 1601-01-01,
        // so this theorem could not even be expressed (#14445 defect 1).
        var instant = new DateTime(1500, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<DateTimeBag>()
            .Where(b => b.D == instant)
            .Solve();

        Assert.NotNull(solution);
        Assert.Equal(instant, solution!.D);
        Assert.Equal(DateTimeKind.Utc, solution.D.Kind);
    }

    [Fact]
    public void Solve_DateTime_RoundTripsAsUtcWithSameTicks()
    {
        // FromFileTime returned Kind=Local: the Kind assertion fails on every runner (a UTC
        // runner has a zero offset but the Kind is still Local), the Ticks assertion fails on
        // every non-UTC one. Asserting BOTH pins the defect on the CI runner itself, per the
        // #14445 acceptance amendment (a ToUniversalTime() comparison passes before the fix
        // and measures nothing).
        var instant = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<DateTimeBag>()
            .Where(b => b.D == instant)
            .Solve();

        Assert.NotNull(solution);
        Assert.Equal(DateTimeKind.Utc, solution!.D.Kind);
        Assert.Equal(instant.Ticks, solution.D.Ticks);
    }

    [Fact]
    public void Solve_DateTimeListBefore1601_RoundTripsEveryElement()
    {
        // The element read goes through its own DateTime arm: the encoding fix has to hold
        // there too, not only on the scalar path.
        var instant = new DateTime(1582, 10, 15, 0, 0, 0, DateTimeKind.Utc);
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<DateTimeListBag>()
            .Where(b => b.Values[0] == instant)
            .Solve();

        Assert.NotNull(solution);
        Assert.Equal(instant, solution!.Values[0]);
        Assert.Equal(DateTimeKind.Utc, solution.Values[0].Kind);
    }

    [Fact]
    public void Solve_DateTimeBeyondRange_ThrowsNamingTheSymbol()
    {
        // The symbol is an unbounded integer to Z3: t.D > DateTime.MaxValue is satisfiable
        // with ticks no DateTime can hold. The read throws OverflowException naming the
        // symbol, rather than letting the DateTime constructor throw namelessly
        // (endjin/Z3.Linq#87 for the bounding follow-up).
        using var ctx = new Z3Context();

        var ex = Assert.Throws<OverflowException>(() =>
            ctx.NewTheorem<DateTimeBag>()
                .Where(b => b.D > DateTime.MaxValue)
                .Solve());

        Assert.Contains("D", ex.Message);
    }
}
