namespace Z3.Linq;

using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The satisfiability outcome of a theorem, as reported by the diagnostic <c>Explain</c> surface (gap B6 of
/// #4616). Unlike <c>Solve()</c> — which returns <c>default(T)</c> for every non-satisfiable outcome — this
/// distinguishes the three Z3 statuses so that "no solution" and "could not decide" are not conflated.
/// </summary>
public enum SolveStatus
{
    /// <summary>The constraints are jointly satisfiable; a model exists.</summary>
    Satisfiable,

    /// <summary>The constraints are jointly unsatisfiable; an UNSAT core identifies a conflicting subset.</summary>
    Unsatisfiable,

    /// <summary>Z3 could not decide satisfiability (e.g. an undecidable fragment or a timeout).</summary>
    Unknown,
}

/// <summary>
/// A reference to one of a theorem's hard <c>.Where</c> constraints, by its declaration order and the
/// string form of its source expression. Used to report the members of an UNSAT core (B6, #4616).
/// </summary>
/// <param name="Index">The 0-based position of the constraint in the <c>.Where</c> chain.</param>
/// <param name="Expression">The source expression of the constraint (<c>constraint.Body.ToString()</c>).</param>
public readonly record struct ConstraintRef(int Index, string Expression)
{
    /// <summary>Returns a compact "<c>#index: expression</c>" form for diagnostics and logging.</summary>
    public override string ToString() => $"#{Index}: {Expression}";
}

/// <summary>
/// The result of diagnosing a theorem's satisfiability (B6, #4616): the <see cref="Status"/> plus, when
/// <see cref="Status"/> is <see cref="SolveStatus.Unsatisfiable"/>, the <see cref="UnsatCore"/> — the subset
/// of hard constraints that are jointly unsatisfiable (minimal as reported by Z3). For satisfiable or unknown
/// outcomes the core is empty.
/// </summary>
public sealed class Explanation
{
    internal Explanation(SolveStatus status, IReadOnlyList<ConstraintRef> unsatCore)
    {
        Status = status;
        UnsatCore = unsatCore;
    }

    /// <summary>The satisfiability status of the theorem.</summary>
    public SolveStatus Status { get; }

    /// <summary>
    /// On <see cref="SolveStatus.Unsatisfiable"/>, the conflicting hard constraints (ordered by index);
    /// empty otherwise.
    /// </summary>
    public IReadOnlyList<ConstraintRef> UnsatCore { get; }

    /// <summary>Convenience: whether the theorem is satisfiable.</summary>
    public bool IsSatisfiable => Status == SolveStatus.Satisfiable;

    /// <summary>Returns a human-readable summary, listing the UNSAT core on conflict.</summary>
    public override string ToString()
    {
        if (Status != SolveStatus.Unsatisfiable || UnsatCore.Count == 0)
        {
            return Status.ToString();
        }

        return $"{Status} (core: {string.Join(", ", UnsatCore.Select(c => c.ToString()))})";
    }
}
