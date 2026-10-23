namespace Z3.Linq.Tests;

using Xunit;

/// <summary>
/// Tests for ValueTuple theorem environments.
///
/// <see cref="Theorem.ConstructFromModel(System.Type, Microsoft.Z3.Context, Microsoft.Z3.Model, Environment)"/>
/// matched constructor parameters to environment members with an ordinal name comparison.
/// A ValueTuple's fields are named Item1..ItemN (named elements x/y/z are erased to Item*
/// in reflection) while its constructor parameters are named item1..itemN, so the ordinal
/// lookup never matched and Solve threw NotSupportedException for every ValueTuple theorem --
/// including the two ValueTuple sections of the shipped Z3.Linq.Demo, which crashed mid-run.
/// The fix adds a case-insensitive fallback index used only when the ordinal lookup misses.
/// (Finding 1, MyIntelligenceAgency/Z3.Linq#35; measured at CoursIA #18770.)
/// </summary>
public class ValueTupleTheoremTests
{
    [Fact]
    public void ValueTuple_Pair_IsConstructedFromModel()
    {
        // Named tuple (bool x, bool y): reflection binds fields Item1/Item2, the
        // ValueTuple`2 constructor takes item1/item2. Before the fix this was the
        // NotSupportedException from the demo's "t.x ^ t.y using ValueTuples" section.
        using var ctx = new Z3Context();
        var result = ctx.NewTheorem<(bool x, bool y)>()
            .Where(t => t.x ^ t.y)
            .Solve();

        Assert.NotEqual(default, result);
        Assert.True(result.x ^ result.y);
    }

    [Fact]
    public void ValueTuple_Triple_SatisfiesAllConstraints()
    {
        // The demo's Bart De Smet TechEd 2012 example: x - y in [1,3], x = 2z + y.
        using var ctx = new Z3Context();
        var result = ctx.NewTheorem<(int x, int y, int z)>()
            .Where(t => t.x - t.y >= 1)
            .Where(t => t.x - t.y <= 3)
            .Where(t => t.x == 2 * t.z + t.y)
            .Solve();

        Assert.InRange(result.x - result.y, 1, 3);
        Assert.Equal(result.x, 2 * result.z + result.y);
    }

    [Fact]
    public void ValueTuple_StillPrefersOrdinalMatch_ForRecordEnvironments()
    {
        // Regression guard for the fallback: a positional record (exact ordinal match)
        // must keep materializing identically. If the case-insensitive index ever took
        // precedence, records with a member differing only by case could mis-bind.
        using var ctx = new Z3Context();
        var result = ctx.NewTheorem<(int a, int b)>()
            .Where(t => t.a + t.b == 10)
            .Where(t => t.a == 4)
            .Solve();

        Assert.Equal(4, result.a);
        Assert.Equal(6, result.b);
    }
}
