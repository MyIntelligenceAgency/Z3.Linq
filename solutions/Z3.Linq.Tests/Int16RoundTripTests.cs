namespace Z3.Linq.Tests;

#pragma warning disable CS1591

/// <summary>
/// Round trips for <see cref="short"/> symbols.
/// </summary>
/// <remarks>
/// The read path used to fold Int16 into the Int32 arm, so a solve succeeded and then failed while
/// the model was written back: an <see cref="int"/> boxed for a <see cref="short"/> property throws
/// at assignment. Every value here is satisfiable and fits the type - only the conversion was
/// missing.
/// </remarks>
[TestClass]
public class Int16RoundTripTests
{
    [TestMethod]
    [DataRow(short.MinValue)]
    [DataRow(short.MaxValue)]
    [DataRow((short)0)]
    [DataRow((short)-123)]
    [DataRow((short)12345)]
    public void Solve_ShortAtEveryBoundary_RoundTrips(short expected)
    {
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<ShortBag>()
            .Where(b => b.S == expected)
            .Solve();

        solution.ShouldNotBeNull();
        solution!.S.ShouldBe(expected);
    }

    [TestMethod]
    public void Solve_ShortListRoundTripsEveryElement()
    {
        // The element read has its own Int16 arm, so the conversion has to hold on the collection
        // path too, not only on the scalar property path.
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<ShortListBag>()
            .Where(b => b.Values[0] == short.MinValue && b.Values[1] == short.MaxValue)
            .Solve();

        solution.ShouldNotBeNull();
        solution!.Values[0].ShouldBe(short.MinValue);
        solution.Values[1].ShouldBe(short.MaxValue);
    }

    [TestMethod]
    public void Solve_ShortBeyondRange_IsUnsatisfiable()
    {
        // The scalar is bounded to its type's range, so this is "no model" rather than a model the
        // read cannot convert.
        using var ctx = new Z3Context();

        var solution = ctx.NewTheorem<ShortBag>()
            .Where(b => b.S > short.MaxValue)
            .Solve();

        solution.ShouldBeNull();
    }

    [TestMethod]
    public void Solve_ShortInListBeyondRange_ThrowsNamingTheSymbol()
    {
        // A collection element is still read with a checked conversion - its length is unknown when
        // the constraints are asserted, so it cannot be bounded. This pins that read.
        using var ctx = new Z3Context();

        var exception = Should.Throw<OverflowException>(() =>
            ctx.NewTheorem<ShortListBag>()
                .Where(b => b.Values[0] > short.MaxValue)
                .Solve());

        exception.Message.ShouldContain("Values");
    }

    /// <summary>One short.</summary>
    public class ShortBag
    {
        /// <summary>Gets or sets the value.</summary>
        public short S { get; set; }
    }

    /// <summary>Two shorts, for the element path.</summary>
    public class ShortListBag
    {
        /// <summary>Gets or sets the values.</summary>
        public List<short> Values { get; set; } = [default, default];
    }
}
