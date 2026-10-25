namespace Z3.Linq.Tests;

#pragma warning disable CS1591

/// <summary>
/// Constraints that read an element of a captured collection.
/// </summary>
/// <remarks>
/// The visitor translates one theorem symbol at a time and has no encoding for a whole array, so
/// a constraint such as <c>p.Cost == weights[1]</c> used to reach the scalar path with the array
/// as the constant to translate and fail there with "Unsupported constant System.Int32[]". The
/// constraints are now folded before translation, which turns the element access into the constant
/// it denotes. Measured on a real notebook before it was written here: a meal-planner model whose
/// daily calorie window was expressed as <c>kcalArr[i]</c> failed on the ported library and solved
/// on the fork's; these tests pin the difference at its smallest size.
/// </remarks>
[TestClass]
public class CapturedCollectionTests
{
    [TestMethod]
    [DataRow(CollectionHandling.Array)]
    [DataRow(CollectionHandling.Constants)]
    public void CapturedArrayElement_IsFoldedIntoAConstant(CollectionHandling mode)
    {
        // Arrange: the array is closed - it mentions nothing of the theorem - so folding it is
        // exactly what PartialEval is for, and it must hold in both collection modes because the
        // element never becomes a symbol of its own.
        using var ctx = new Z3Context { DefaultCollectionHandling = mode };
        int[] weights = [3, 5];

        // Act
        var result = ctx.NewTheorem<Plate>()
            .Where(p => p.Cost == weights[1])
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result!.Cost.ShouldBe(5);
    }

    [TestMethod]
    public void CapturedArrayElement_MultipliedByASymbol_KeepsBothSides()
    {
        // Arrange: a subtree that mixes a captured element with the theorem's own symbol must
        // survive folding with the symbol still free - 4 * Cost == 40 leaves Cost = 10.
        using var ctx = new Z3Context();
        int[] factors = [2, 4];

        // Act
        var result = ctx.NewTheorem<Plate>()
            .Where(p => p.Cost * factors[1] == 40)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result!.Cost.ShouldBe(10);
    }

    [TestMethod]
    public void CapturedListElement_IsFoldedIntoAConstant()
    {
        // Arrange: a List<T> element is a get_Item call rather than an ArrayIndex node, so it
        // arrives by a different route; both must fold.
        using var ctx = new Z3Context();
        var factors = new List<int> { 7, 11 };

        // Act
        var result = ctx.NewTheorem<Plate>()
            .Where(p => p.Cost == factors[1])
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result!.Cost.ShouldBe(11);
    }

    [TestMethod]
    public void CapturedArrayElement_InsideASoftConstraint_IsFoldedToo()
    {
        // Arrange: the soft path translates through the same helper, so a captured element there
        // must fold as well - the two would otherwise drift apart.
        using var ctx = new Z3Context();
        int[] targets = [1, 6];

        // Act
        var result = ctx.NewTheorem<Plate>()
            .Where(p => p.Cost >= 0 && p.Cost <= 9)
            .SoftWhere(p => p.Cost == targets[1], weight: 5)
            .Solve();

        // Assert
        result.ShouldNotBeNull();
        result!.Cost.ShouldBe(6);
    }

    /// <summary>One integer symbol, enough to show what was folded into it.</summary>
    public sealed class Plate
    {
        /// <summary>Gets or sets the cost.</summary>
        public int Cost { get; set; }
    }
}
