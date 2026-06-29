namespace Z3.Linq.Tests;

using Xunit;

/// <summary>
/// Tests for record (constructor-initialized) theorem environments
/// (DSL backlog item B9, #4616).
///
/// Before B9, <see cref="Theorem.GetSolution(System.Type, Microsoft.Z3.Context, Microsoft.Z3.Model, Environment)"/>
/// reconstructed solutions either through the anonymous-type branch (uninitialized object +
/// backing-field assignment) or through the "onymous" branch (<c>Activator.CreateInstance(t)</c>
/// followed by property setters). Neither handles a positional record
/// (<c>record Point(int X, int Y)</c>): it has no public parameterless constructor, so
/// <c>Activator.CreateInstance(t)</c> threw, exactly the limitation documented in
/// <c>Z3.Linq.Examples/RecordTheorem.cs</c>. B9 adds a constructor-based reconstruction branch
/// that evaluates each constructor parameter from the model and invokes the primary constructor.
/// </summary>
public class RecordEnvTheoryTests
{
    // Idiomatic modern C# environment: a positional record with no parameterless constructor.
    public record Point(int X, int Y);

    public record Mixed(int Count, bool Flag);

    public record Triple(int A, int B, int C);

    [Fact]
    public void PositionalRecord_IsConstructedFromModel()
    {
        // Two linear constraints; the record env must be rebuilt via its primary constructor.
        using var ctx = new Z3Context();
        var theorem = ctx.NewTheorem<Point>()
            .Where(p => p.X + p.Y == 12)
            .Where(p => p.X == 2 * p.Y);

        var result = theorem.Solve();

        Assert.NotNull(result);
        Assert.Equal(12, result!.X + result.Y);
        Assert.Equal(result.X, 2 * result.Y);
        // X = 8, Y = 4 is the unique solution.
        Assert.Equal(8, result.X);
        Assert.Equal(4, result.Y);
    }

    [Fact]
    public void PositionalRecord_MixedScalarTypes_AreReadBack()
    {
        // Exercises a record mixing an int and a bool constructor parameter.
        using var ctx = new Z3Context();
        var result = ctx.NewTheorem<Mixed>()
            .Where(m => m.Count > 3 && m.Count < 6)
            .Where(m => m.Flag)
            .Solve();

        Assert.NotNull(result);
        Assert.InRange(result!.Count, 4, 5);
        Assert.True(result.Flag);
    }

    [Fact]
    public void PositionalRecord_ThreeParameters_KeepConstructorOrder()
    {
        // Three parameters validate that parameters are matched by name (not declaration order
        // of the environment dictionary) and passed positionally to the constructor.
        using var ctx = new Z3Context();
        var result = ctx.NewTheorem<Triple>()
            .Where(t => t.A == 1)
            .Where(t => t.B == 2)
            .Where(t => t.C == 3)
            .Solve();

        Assert.NotNull(result);
        Assert.Equal(1, result!.A);
        Assert.Equal(2, result.B);
        Assert.Equal(3, result.C);
    }

    [Fact]
    public void UnsatRecordTheorem_ReturnsNull()
    {
        // The constructor branch must not run when the theorem is unsatisfiable.
        using var ctx = new Z3Context();
        var result = ctx.NewTheorem<Point>()
            .Where(p => p.X > 0)
            .Where(p => p.X < 0)
            .Solve();

        Assert.Null(result);
    }
}
