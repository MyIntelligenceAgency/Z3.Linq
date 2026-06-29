namespace Z3.Linq.Tests;

using System.Collections.Generic;
using System.Linq;
using Xunit;

/// <summary>
/// Coverage for generic-collection members (<see cref="List{T}"/> / <see cref="IList{T}"/>) used as
/// theorem variables. A <c>List&lt;T&gt;</c> indexer (<c>list[i]</c>) compiles to a <c>get_Item</c>
/// method call, NOT to an <see cref="System.Linq.Expressions.ExpressionType.ArrayIndex"/> node like
/// <c>int[]</c>. Before the visitor routed <c>get_Item</c> through the Constants-mode per-element path,
/// indexing a <c>List&lt;T&gt;</c> threw <see cref="System.NullReferenceException"/> in Constants mode
/// (it fell through to <c>MkSelect</c>, which assumes an <c>ArrayExpr</c> the Constants environment never builds).
///
/// Array mode already worked (the member binds to an <c>ArrayExpr</c>); these tests pin BOTH modes so the
/// two indexing front-ends (ArrayIndex node vs get_Item call) stay behaviorally aligned.
/// </summary>
public class ListCollectionTests
{
    public class IntListBag
    {
        public List<int> Values { get; set; } = new() { 0, 0, 0, 0 };
    }

    public class BoolListBag
    {
        public List<bool> Flags { get; set; } = new() { false, false, false };
    }

    [Theory]
    [InlineData(CollectionHandling.Array)]
    [InlineData(CollectionHandling.Constants)]
    public void ListInt_Indexer_SolvesInBothModes(CollectionHandling mode)
    {
        using var ctx = new Z3Context { DefaultCollectionHandling = mode };

        var theorem = ctx.NewTheorem<IntListBag>()
            .Where(b => b.Values[0] == 5)
            .Where(b => b.Values[1] == b.Values[0] + 1)
            .Where(b => b.Values[2] > b.Values[1])
            .Where(b => b.Values[2] < 10);

        var solution = theorem.Solve();

        Assert.NotNull(solution);
        Assert.Equal(5, solution!.Values[0]);
        Assert.Equal(6, solution.Values[1]);
        Assert.InRange(solution.Values[2], 7, 9);
    }

    [Theory]
    [InlineData(CollectionHandling.Array)]
    [InlineData(CollectionHandling.Constants)]
    public void ListBool_Indexer_SolvesInBothModes(CollectionHandling mode)
    {
        using var ctx = new Z3Context { DefaultCollectionHandling = mode };

        var theorem = ctx.NewTheorem<BoolListBag>()
            .Where(b => b.Flags[0])
            .Where(b => !b.Flags[1]);

        var solution = theorem.Solve();

        Assert.NotNull(solution);
        Assert.True(solution!.Flags[0]);
        Assert.False(solution.Flags[1]);
    }

    /// <summary>
    /// Regression mirror of <c>ConstantsMode_ValueTypeArray_WithInteriorIndexGap_DoesNotThrow</c> but for
    /// <see cref="List{T}"/>: an index the constraints never reference must come back as the element-type
    /// default, and model extraction must rebuild the <c>List&lt;int&gt;</c> (via its <c>int[]</c> constructor)
    /// without throwing.
    /// </summary>
    [Fact]
    public void ConstantsMode_ListInt_WithFreeIndex_ExtractsDefault()
    {
        using var ctx = new Z3Context { DefaultCollectionHandling = CollectionHandling.Constants };

        // Values[0] and Values[3] are constrained; Values[1]/[2] are deliberately never referenced.
        var theorem = ctx.NewTheorem<IntListBag>()
            .Where(b => b.Values[0] == 1)
            .Where(b => b.Values[3] == 4);

        var solution = theorem.Solve();

        Assert.NotNull(solution);
        Assert.Equal(1, solution!.Values[0]);
        Assert.Equal(4, solution.Values[3]);
    }
}
