namespace Z3.Linq.Tests;

#pragma warning disable CS1591

/// <summary>
/// Generic collection members (<see cref="List{T}"/> / <see cref="IList{T}"/>) used as theorem
/// variables.
/// </summary>
/// <remarks>
/// A <c>List&lt;T&gt;</c> indexer (<c>list[i]</c>) compiles to a <c>get_Item</c> method call, not to an
/// <see cref="System.Linq.Expressions.ExpressionType.ArrayIndex"/> node like <c>int[]</c>. Before the
/// visitor routed <c>get_Item</c> through the Constants-mode per-element path, indexing a
/// <c>List&lt;T&gt;</c> fell through to <c>MkSelect</c>, which assumes an <c>ArrayExpr</c> the
/// Constants environment never builds.
/// <para>
/// Array mode already worked (the member binds to an <c>ArrayExpr</c>); these tests pin both modes so
/// the two indexing front-ends stay behaviourally aligned.
/// </para>
/// </remarks>
[TestClass]
public class ListCollectionTests
{
    [TestMethod]
    [DataRow(CollectionHandling.Array)]
    [DataRow(CollectionHandling.Constants)]
    public void ListInt_Indexer_SolvesInBothModes(CollectionHandling mode)
    {
        using var ctx = new Z3Context { DefaultCollectionHandling = mode };

        var solution = ctx.NewTheorem<IntListBag>()
            .Where(b => b.Values[0] == 5)
            .Where(b => b.Values[1] == b.Values[0] + 1)
            .Where(b => b.Values[2] > b.Values[1])
            .Where(b => b.Values[2] < 10)
            .Solve();

        solution.ShouldNotBeNull();
        solution!.Values[0].ShouldBe(5);
        solution.Values[1].ShouldBe(6);
        solution.Values[2].ShouldBeInRange(7, 9);
    }

    [TestMethod]
    [DataRow(CollectionHandling.Array)]
    [DataRow(CollectionHandling.Constants)]
    public void ListBool_Indexer_SolvesInBothModes(CollectionHandling mode)
    {
        using var ctx = new Z3Context { DefaultCollectionHandling = mode };

        var solution = ctx.NewTheorem<BoolListBag>()
            .Where(b => b.Flags[0])
            .Where(b => !b.Flags[1])
            .Solve();

        solution.ShouldNotBeNull();
        solution!.Flags[0].ShouldBeTrue();
        solution.Flags[1].ShouldBeFalse();
    }

    [TestMethod]
    public void ConstantsMode_ListInt_WithFreeIndex_ExtractsDefault()
    {
        // An index the constraints never reference must come back as the element-type default, and
        // model extraction must rebuild the List<int> (via its int[] constructor) without throwing.
        using var ctx = new Z3Context { DefaultCollectionHandling = CollectionHandling.Constants };

        var solution = ctx.NewTheorem<IntListBag>()
            .Where(b => b.Values[0] == 1)
            .Where(b => b.Values[3] == 4)
            .Solve();

        solution.ShouldNotBeNull();
        solution!.Values[0].ShouldBe(1);
        solution.Values[3].ShouldBe(4);
    }

    /// <summary>Four integers behind a generic list.</summary>
    public class IntListBag
    {
        /// <summary>Gets or sets the values.</summary>
        public List<int> Values { get; set; } = [0, 0, 0, 0];
    }

    /// <summary>Three flags behind a generic list.</summary>
    public class BoolListBag
    {
        /// <summary>Gets or sets the flags.</summary>
        public List<bool> Flags { get; set; } = [false, false, false];
    }
}
