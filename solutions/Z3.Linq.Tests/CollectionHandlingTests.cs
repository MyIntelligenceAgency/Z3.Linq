namespace Z3.Linq.Tests;

using Microsoft.Z3;
using System;
using System.Linq;
using Xunit;

/// <summary>
/// Keystone tests for the resurrected <see cref="CollectionHandling"/> modes.
///
/// Both modes must solve the same constraint problem on a nested int[][] grid and produce a
/// satisfying assignment. The Array mode exercises Z3 array theory (a single ArrayExpr with
/// Select); the Constants mode exercises one Z3 constant per element (the classic endjin binding
/// model, lazily created on index access via <see cref="MultipleEnvironment"/>).
///
/// These tests are the proof that the previously-dead CollectionHandling enum is now wired end-to-end
/// (environment construction → constraint visiting → model extraction) in both modes.
/// </summary>
public class CollectionHandlingTests
{
    /// <summary>
    /// 4x4 Sudoku grid modeled as int[][]: the smallest nested-array CSP that exercises both modes.
    /// </summary>
    public class SudokuGrid4
    {
        public int[][] Cells { get; set; } = new int[4][];

        public SudokuGrid4()
        {
            for (int i = 0; i < 4; i++)
            {
                Cells[i] = new int[4];
            }
        }
    }

    private static Theorem<SudokuGrid4> BuildSudoku4x4(Z3Context ctx)
    {
        var theorem = ctx.NewTheorem<SudokuGrid4>();

        // Each cell in [1,4].
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                int i1 = i, j1 = j;
                theorem = theorem.Where(g => g.Cells[i1][j1] >= 1 && g.Cells[i1][j1] <= 4);
            }
        }

        // Row distinctness (explicit args — the pattern supported by the Distinct rewriter).
        for (int r = 0; r < 4; r++)
        {
            int r1 = r;
            theorem = theorem.Where(g => Z3Methods.Distinct(
                g.Cells[r1][0], g.Cells[r1][1], g.Cells[r1][2], g.Cells[r1][3]));
        }

        // Column distinctness.
        for (int c = 0; c < 4; c++)
        {
            int c1 = c;
            theorem = theorem.Where(g => Z3Methods.Distinct(
                g.Cells[0][c1], g.Cells[1][c1], g.Cells[2][c1], g.Cells[3][c1]));
        }

        return theorem;
    }

    [Theory]
    [InlineData(CollectionHandling.Array)]
    [InlineData(CollectionHandling.Constants)]
    public void Sudoku4x4_IsSolvable_InBothModes(CollectionHandling mode)
    {
        using var ctx = new Z3Context { DefaultCollectionHandling = mode };
        var solution = BuildSudoku4x4(ctx).Solve();

        Assert.NotNull(solution);
        Assert.All(solution!.Cells, row => Assert.All(row, v => Assert.InRange(v, 1, 4)));

        // A valid 4x4 Latin square: each row and each column is a permutation of 1..4.
        for (int r = 0; r < 4; r++)
        {
            Assert.Equal(new int[] { 1, 2, 3, 4 }, solution.Cells[r].OrderBy(v => v));
        }

        for (int c = 0; c < 4; c++)
        {
            Assert.Equal(new int[] { 1, 2, 3, 4 }, Enumerable.Range(0, 4).Select(r => solution.Cells[r][c]).OrderBy(v => v));
        }
    }

    [Theory]
    [InlineData(CollectionHandling.Array)]
    [InlineData(CollectionHandling.Constants)]
    public void Sudoku4x4_RespectsHint_InBothModes(CollectionHandling mode)
    {
        using var ctx = new Z3Context { DefaultCollectionHandling = mode };
        var theorem = BuildSudoku4x4(ctx).Where(g => g.Cells[0][0] == 2);

        var solution = theorem.Solve();

        Assert.NotNull(solution);
        Assert.Equal(2, solution!.Cells[0][0]);
    }

    [Fact]
    public void ConstantsMode_DefaultsToArray_OnBareContext()
    {
        // Anti-regression guard: a context with no explicit mode must default to Array,
        // preserving the historical (pre-resurrection) behavior.
        using var ctx = new Z3Context();
        Assert.Equal(CollectionHandling.Array, ctx.DefaultCollectionHandling);
    }

    /// <summary>
    /// Flat int[] (non-nested) collection in Constants mode — the simplest shape that must work
    /// now that MultipleEnvironment is wired. Three cells, all equal to 7.
    /// </summary>
    public class Triple
    {
        public int[] Values { get; set; } = new int[3];
    }

    [Fact]
    public void ConstantsMode_FlatIntArray_SolvesWithPerElementConstants()
    {
        using var ctx = new Z3Context { DefaultCollectionHandling = CollectionHandling.Constants };

        var theorem = ctx.NewTheorem<Triple>()
            .Where(t => t.Values[0] == t.Values[1])
            .Where(t => t.Values[1] == t.Values[2])
            .Where(t => t.Values[0] == 7);

        var solution = theorem.Solve();

        Assert.NotNull(solution);
        Assert.Equal(7, solution!.Values[0]);
        Assert.Equal(7, solution.Values[1]);
        Assert.Equal(7, solution.Values[2]);
    }

    /// <summary>
    /// Regression: in Constants mode an index that the constraints never reference is not materialized
    /// as a Z3 constant. Such a free index used to leak a null into the extracted result, and
    /// ArrayList.ToArray(typeof(int)) then threw InvalidCastException
    /// ("could not be cast down to the destination array type"). The free index must come back as
    /// default(int) = 0, since an unconstrained cell admits any value.
    /// </summary>
    [Fact]
    public void ConstantsMode_ValueTypeArray_WithInteriorIndexGap_DoesNotThrow()
    {
        using var ctx = new Z3Context { DefaultCollectionHandling = CollectionHandling.Constants };

        // Values[0] and Values[2] are constrained; Values[1] is deliberately never referenced.
        var theorem = ctx.NewTheorem<Triple>()
            .Where(t => t.Values[0] == 1)
            .Where(t => t.Values[2] == 3);

        var solution = theorem.Solve();

        Assert.NotNull(solution);
        Assert.Equal(1, solution!.Values[0]);
        Assert.Equal(3, solution.Values[2]);
        Assert.Equal(0, solution.Values[1]); // unconstrained -> element-type default
    }

    /// <summary>
    /// Boolean per-element constants in Constants mode: exercises MkBoolConst leaf creation and
    /// IsTrue extraction. Flags[2] is left free, so it also covers the same value-type default-fill
    /// path as the gap test above (bool[] previously threw InvalidCastException at extraction).
    /// </summary>
    public class BoolTriple
    {
        public bool[] Flags { get; set; } = new bool[3];
    }

    [Fact]
    public void ConstantsMode_BoolArray_SolvesWithPerElementConstants()
    {
        using var ctx = new Z3Context { DefaultCollectionHandling = CollectionHandling.Constants };

        var theorem = ctx.NewTheorem<BoolTriple>()
            .Where(t => t.Flags[0])
            .Where(t => !t.Flags[1]);

        var solution = theorem.Solve();

        Assert.NotNull(solution);
        Assert.True(solution!.Flags[0]);
        Assert.False(solution.Flags[1]);
        Assert.False(solution.Flags[2]); // unconstrained -> default(bool) = false
    }
}
