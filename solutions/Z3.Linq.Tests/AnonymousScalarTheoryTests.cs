namespace Z3.Linq.Tests;

using Xunit;

/// <summary>
/// Tests for anonymous-type theorem environments with scalar members beyond bool/int.
///
/// The anonymous-type branch of <see cref="Theorem.GetSolution(System.Type, Microsoft.Z3.Context, Microsoft.Z3.Model, Environment)"/>
/// used to materialize members through an inline bool/int-only dispatch, so an anonymous
/// environment holding a <c>double</c> (or string/decimal/DateTime) member threw
/// NotSupportedException at Solve time -- a gap the shipped demo never exercises
/// (its oil/warehouse problems use ValueTuple environments), so it is covered here
/// by tests. The branch now delegates scalar materialization
/// to <c>ConvertZ3Expression</c>'s full type dispatch, which already served the
/// "onymous" branch. (Finding 2, MyIntelligenceAgency/Z3.Linq#35; measured at CoursIA #18770.)
/// </summary>
public class AnonymousScalarTheoryTests
{
    [Fact]
    public void AnonymousType_DoubleMembers_AreReadBack()
    {
        // The demo's oil/warehouse problems express this arithmetic with ValueTuple
        // environments (covered by the separate ValueTuple fix); the same shape with an
        // anonymous environment used to throw NotSupportedException at Solve time.
        using var ctx = new Z3Context();
        var result = ctx.NewTheorem(new { price = default(double), shipping = default(double) })
            .Where(t => t.price + t.shipping == 10.5)
            .Where(t => t.price == 2 * t.shipping)
            .Solve();

        // price = 7, shipping = 3.5 exactly (rational arithmetic, not FP).
        Assert.Equal(7.0, result.price, precision: 9);
        Assert.Equal(3.5, result.shipping, precision: 9);
    }

    [Fact]
    public void AnonymousType_MixedBoolIntDouble_AllMembersSurviveRoundTrip()
    {
        // Regression guard: the former inline dispatch covered bool and int; the move to
        // the shared dispatch must not regress them while adding double.
        using var ctx = new Z3Context();
        var result = ctx.NewTheorem(new { flag = default(bool), count = default(int), ratio = default(double) })
            .Where(t => t.flag)
            .Where(t => t.count > 3 && t.count < 6)
            .Where(t => t.ratio == 0.5 * t.count)
            .Solve();

        Assert.True(result.flag);
        Assert.InRange(result.count, 4, 5);
        Assert.Equal(0.5 * result.count, result.ratio, precision: 9);
    }
}
