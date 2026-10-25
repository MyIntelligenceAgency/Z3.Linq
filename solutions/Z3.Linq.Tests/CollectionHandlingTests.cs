namespace Z3.Linq.Tests;

#pragma warning disable CS1591

/// <summary>
/// The two <see cref="CollectionHandling"/> modes must solve the same constraint problem and
/// produce the same kind of satisfying assignment.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CollectionHandling.Array"/> exercises Z3 array theory: one array constant, read
/// with Select. <see cref="CollectionHandling.Constants"/> exercises one constant per element,
/// created lazily on index access through <see cref="MultipleEnvironment"/>. A nested
/// <c>int[][]</c> grid is the smallest shape that exercises both the mode switch and the
/// recursion into a row.
/// </para>
/// <para>
/// These tests are the proof that the mode is wired end to end - environment construction,
/// constraint translation and model extraction - rather than being a flag nothing reads.
/// </para>
/// </remarks>
[TestClass]
public class CollectionHandlingTests
{
    /// <summary>
    /// A 4x4 Sudoku grid modelled as <c>int[][]</c>: a nested collection, one row per element, in
    /// both modes.
    /// </summary>
    public class SudokuGrid4
    {
        /// <summary>
        /// Gets or sets the cells of the grid, four rows of four.
        /// </summary>
        public int[][] Cells { get; set; } = [new int[4], new int[4], new int[4], new int[4]];
    }

    private static Theorem<SudokuGrid4> BuildNestedSudoku4x4(Z3Context ctx)
    {
        var theorem = ctx.NewTheorem<SudokuGrid4>();

        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                int row = i, column = j;
                theorem = theorem.Where(g => g.Cells[row][column] >= 1 && g.Cells[row][column] <= 4);
            }
        }

        for (int r = 0; r < 4; r++)
        {
            int row = r;
            theorem = theorem.Where(g => Z3Methods.Distinct(
                g.Cells[row][0], g.Cells[row][1], g.Cells[row][2], g.Cells[row][3]));
        }

        for (int c = 0; c < 4; c++)
        {
            int column = c;
            theorem = theorem.Where(g => Z3Methods.Distinct(
                g.Cells[0][column], g.Cells[1][column], g.Cells[2][column], g.Cells[3][column]));
        }

        return theorem;
    }

    [TestMethod]
    [DataRow(CollectionHandling.Array)]
    [DataRow(CollectionHandling.Constants)]
    public void NestedGrid_IsSolvable_InBothModes(CollectionHandling mode)
    {
        using var ctx = new Z3Context { DefaultCollectionHandling = mode };

        var solution = BuildNestedSudoku4x4(ctx).Solve();

        solution.ShouldNotBeNull();

        foreach (int[] row in solution!.Cells)
        {
            row.OrderBy(v => v).ShouldBe([1, 2, 3, 4]);
        }

        for (int c = 0; c < 4; c++)
        {
            int column = c;
            Enumerable.Range(0, 4).Select(r => solution.Cells[r][column]).OrderBy(v => v).ShouldBe([1, 2, 3, 4]);
        }
    }

    [TestMethod]
    [DataRow(CollectionHandling.Array)]
    [DataRow(CollectionHandling.Constants)]
    public void NestedGrid_RespectsHint_InBothModes(CollectionHandling mode)
    {
        using var ctx = new Z3Context { DefaultCollectionHandling = mode };

        var solution = BuildNestedSudoku4x4(ctx).Where(g => g.Cells[1][2] == 3).Solve();

        solution.ShouldNotBeNull();
        solution!.Cells[1][2].ShouldBe(3);
    }

    /// <summary>
    /// A 4x4 Sudoku grid held in a flat <c>int[16]</c>: the smallest CSP that exercises both modes
    /// with more than a handful of symbols.
    /// </summary>
    public class FlatSudokuGrid4
    {
        /// <summary>
        /// Gets or sets the sixteen cells, row-major: cell (r, c) is <c>Cells[4 * r + c]</c>.
        /// </summary>
        public int[] Cells { get; set; } = new int[16];
    }

    private static Theorem<FlatSudokuGrid4> BuildSudoku4x4(Z3Context ctx)
    {
        var theorem = ctx.NewTheorem<FlatSudokuGrid4>();

        for (int i = 0; i < 16; i++)
        {
            int cell = i;
            theorem = theorem.Where(g => g.Cells[cell] >= 1 && g.Cells[cell] <= 4);
        }

        for (int r = 0; r < 4; r++)
        {
            int first = 4 * r;
            theorem = theorem.Where(g => Z3Methods.Distinct(
                g.Cells[first], g.Cells[first + 1], g.Cells[first + 2], g.Cells[first + 3]));
        }

        for (int c = 0; c < 4; c++)
        {
            int first = c;
            theorem = theorem.Where(g => Z3Methods.Distinct(
                g.Cells[first], g.Cells[first + 4], g.Cells[first + 8], g.Cells[first + 12]));
        }

        return theorem;
    }

    [TestMethod]
    [DataRow(CollectionHandling.Array)]
    [DataRow(CollectionHandling.Constants)]
    public void Sudoku4x4_IsSolvable_InBothModes(CollectionHandling mode)
    {
        using var ctx = new Z3Context { DefaultCollectionHandling = mode };

        var solution = BuildSudoku4x4(ctx).Solve();

        solution.ShouldNotBeNull();

        foreach (int cell in solution!.Cells)
        {
            cell.ShouldBeInRange(1, 4);
        }

        // A valid 4x4 Latin square: every row and every column is a permutation of 1..4.
        for (int r = 0; r < 4; r++)
        {
            int first = 4 * r;
            Enumerable.Range(0, 4).Select(o => solution.Cells[first + o]).OrderBy(v => v).ShouldBe([1, 2, 3, 4]);
        }

        for (int c = 0; c < 4; c++)
        {
            int first = c;
            Enumerable.Range(0, 4).Select(o => solution.Cells[first + (4 * o)]).OrderBy(v => v).ShouldBe([1, 2, 3, 4]);
        }
    }

    [TestMethod]
    [DataRow(CollectionHandling.Array)]
    [DataRow(CollectionHandling.Constants)]
    public void Sudoku4x4_RespectsHint_InBothModes(CollectionHandling mode)
    {
        using var ctx = new Z3Context { DefaultCollectionHandling = mode };

        var solution = BuildSudoku4x4(ctx).Where(g => g.Cells[0] == 2).Solve();

        solution.ShouldNotBeNull();
        solution!.Cells[0].ShouldBe(2);
    }

    [TestMethod]
    public void ConstantsMode_DefaultsToArray_OnBareContext()
    {
        // Anti-regression guard: a context that never sets the mode must keep the historical
        // behaviour, which is the array-theory one.
        using var ctx = new Z3Context();

        ctx.DefaultCollectionHandling.ShouldBe(CollectionHandling.Array);
    }

    /// <summary>
    /// A flat (non-nested) <c>int[]</c> in constants mode: the simplest shape that exercises the
    /// per-element path.
    /// </summary>
    public class Triple
    {
        /// <summary>
        /// Gets or sets three integer values.
        /// </summary>
        public int[] Values { get; set; } = new int[3];
    }

    [TestMethod]
    public void ConstantsMode_FlatIntArray_SolvesWithPerElementConstants()
    {
        using var ctx = new Z3Context { DefaultCollectionHandling = CollectionHandling.Constants };

        var solution = ctx.NewTheorem<Triple>()
            .Where(t => t.Values[0] == t.Values[1])
            .Where(t => t.Values[1] == t.Values[2])
            .Where(t => t.Values[0] == 7)
            .Solve();

        solution.ShouldNotBeNull();
        solution!.Values[0].ShouldBe(7);
        solution.Values[1].ShouldBe(7);
        solution.Values[2].ShouldBe(7);
    }

    /// <summary>
    /// An index the constraints never reference is not materialised as a Z3 constant. Such a free
    /// index used to leak a null into the extracted result and blow up in
    /// <c>ArrayList.ToArray(typeof(int))</c>; it must come back as the element type's default.
    /// </summary>
    [TestMethod]
    public void ConstantsMode_ValueTypeArray_WithInteriorIndexGap_DoesNotThrow()
    {
        using var ctx = new Z3Context { DefaultCollectionHandling = CollectionHandling.Constants };

        // Values[0] and Values[2] are constrained; Values[1] is deliberately never referenced.
        var solution = ctx.NewTheorem<Triple>()
            .Where(t => t.Values[0] == 1)
            .Where(t => t.Values[2] == 3)
            .Solve();

        solution.ShouldNotBeNull();
        solution!.Values[0].ShouldBe(1);
        solution.Values[2].ShouldBe(3);
        solution.Values[1].ShouldBe(0);
    }

    /// <summary>
    /// Three boolean flags, so the per-element path has to create boolean constants and read them
    /// back, not just integer ones.
    /// </summary>
    public class BoolTriple
    {
        /// <summary>
        /// Gets or sets three flags.
        /// </summary>
        public bool[] Flags { get; set; } = new bool[3];
    }

    [TestMethod]
    public void ConstantsMode_BoolArray_SolvesWithPerElementConstants()
    {
        using var ctx = new Z3Context { DefaultCollectionHandling = CollectionHandling.Constants };

        var solution = ctx.NewTheorem<BoolTriple>()
            .Where(t => t.Flags[0])
            .Where(t => !t.Flags[1])
            .Solve();

        solution.ShouldNotBeNull();
        solution!.Flags[0].ShouldBeTrue();
        solution.Flags[1].ShouldBeFalse();
        solution.Flags[2].ShouldBeFalse();
    }
}
