namespace Z3.Linq.Examples;

// Records are now supported as theorem environments. Solution reconstruction
// (Theorem.ConstructFromModel) detects types that lack a public parameterless constructor --
// including positional records like `record Point(int X, int Y)` -- and rebuilds them by
// evaluating each constructor parameter from the Z3 model and invoking the primary constructor.
// (DSL backlog B9, #4616; see Z3.Linq.Tests/RecordEnvTheoryTests.cs.) Anonymous types and
// ordinary mutable types keep their existing dedicated branches.
public record RecordTheorem<T1, T2>
{
    public T1 X { get; init; } = default!;

    public T2 Y { get; init; } = default!;
}