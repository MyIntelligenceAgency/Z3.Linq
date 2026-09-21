namespace Z3.Linq.Tests;

using System;
using System.Collections.Generic;
using Xunit;

/// <summary>
/// Round-trip regression tests for <see cref="short"/> (Int16) symbols (#17302).
/// The read path folded Int16 into the Int32 arm of the switch: <c>ReadIntegral</c>
/// returned an <see cref="int"/>, and <c>PropertyInfo.SetValue</c> on a short property
/// threw <see cref="ArgumentException"/> ("Object of type 'System.Int32' cannot be
/// converted to type 'System.Int16'") after a successful solve, making satisfiable
/// theorems on short members unusable. The arm now reads wide and converts through a
/// checked <c>ToInt16</c> that names the symbol on overflow, mirroring
/// <see cref="DateTime"/> handling.
/// </summary>
public class Int16RoundTripTests
{
    public class ShortBag
    {
        public short S { get; set; }
    }

    public class ShortListBag
    {
        public List<short> Values { get; set; } = new() { default, default };
    }

    [Theory]
    [InlineData(short.MinValue)]
    [InlineData(short.MaxValue)]
    [InlineData((short)0)]
    [InlineData((short)-123)]
    [InlineData((short)12345)]
    public void Solve_ShortAtEveryBoundary_RoundTrips(short expected)
    {
        // Before the fix each of these solved successfully, then threw ArgumentException
        // at PropertyInfo.SetValue during model read-back: the constraint is satisfiable
        // and the value fits the type, only the conversion was missing.
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<ShortBag>()
            .Where(b => b.S == expected)
            .Solve();

        Assert.NotNull(solution);
        Assert.Equal(expected, solution!.S);
    }

    [Fact]
    public void Solve_ShortListRoundTripsEveryElement()
    {
        // The element read goes through its own Int16 arm (ConvertScalarExpr): the fix has
        // to hold on the collection path too, not only on the scalar property path.
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<ShortListBag>()
            .Where(b => b.Values[0] == short.MinValue && b.Values[1] == short.MaxValue)
            .Solve();

        Assert.NotNull(solution);
        Assert.Equal(short.MinValue, solution!.Values[0]);
        Assert.Equal(short.MaxValue, solution.Values[1]);
    }

    [Fact]
    public void Solve_ShortBeyondRange_ThrowsNamingTheSymbol()
    {
        // The symbol is an unbounded integer to Z3: b.S > short.MaxValue is satisfiable
        // with a value no short can hold. The read throws OverflowException naming the
        // symbol, rather than letting reflection fail with a type-mismatch
        // ArgumentException far from the cause (checked read, per the DateTime pattern).
        using var ctx = new Z3Context();

        var ex = Assert.Throws<OverflowException>(() =>
            ctx.NewTheorem<ShortBag>()
                .Where(b => b.S > short.MaxValue)
                .Solve());

        Assert.Contains("S", ex.Message);
    }
}
