namespace Z3.Linq;

using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The satisfiability outcome of a theorem, as reported by <c>Explain</c>.
/// </summary>
/// <remarks>
/// <see cref="Theorem{T}.Solve(CancellationToken)"/> answers <c>default(T)</c> for every outcome that is not
/// satisfiable, so "the constraints contradict each other" and "Z3 could not decide" arrive as the
/// same value. The diagnostic surface keeps the three Z3 statuses apart.
/// </remarks>
public enum SolveStatus
{
    /// <summary>The constraints are jointly satisfiable; a model exists.</summary>
    Satisfiable,

    /// <summary>The constraints are jointly unsatisfiable; an unsatisfiable core names a conflicting subset.</summary>
    Unsatisfiable,

    /// <summary>Z3 could not decide satisfiability - a resource limit was reached, or it gave up.</summary>
    Unknown,
}

/// <summary>
/// One of a theorem's hard <c>Where</c> constraints, by its declaration order and the source text
/// of its expression.
/// </summary>
/// <param name="Index">The 0-based position of the constraint in the <c>Where</c> chain.</param>
/// <param name="Expression">The source expression of the constraint.</param>
public readonly record struct ConstraintRef(int Index, string Expression)
{
    /// <summary>
    /// Formats the reference as <c>#index: expression</c>.
    /// </summary>
    /// <returns>The compact form, for diagnostics and logging.</returns>
    public override string ToString() => $"#{this.Index}: {this.Expression}";
}

/// <summary>
/// The result of diagnosing a theorem's satisfiability: the status, and on a conflict the core.
/// </summary>
/// <remarks>
/// The core is the subset of hard constraints Z3 reports as jointly unsatisfiable - a subset, not
/// necessarily the smallest one; Z3 names constraints that participate in its refutation, and
/// asking for a minimum core is a separate, more expensive question. It is empty for any status
/// other than <see cref="SolveStatus.Unsatisfiable"/>, and it can be empty on a conflict whose
/// refutation needs none of the constraints individually (see
/// <see cref="Theorem{T}.Explain"/> for the one case the translation can produce).
/// </remarks>
public sealed class Explanation
{
    internal Explanation(SolveStatus status, IReadOnlyList<ConstraintRef> unsatCore)
    {
        this.Status = status;
        this.UnsatCore = unsatCore;
    }

    /// <summary>Gets the satisfiability status of the theorem.</summary>
    public SolveStatus Status { get; }

    /// <summary>
    /// Gets the conflicting hard constraints on <see cref="SolveStatus.Unsatisfiable"/>, ordered by
    /// index; empty for any other status.
    /// </summary>
    public IReadOnlyList<ConstraintRef> UnsatCore { get; }

    /// <summary>Gets a value indicating whether the theorem is satisfiable.</summary>
    public bool IsSatisfiable => this.Status == SolveStatus.Satisfiable;

    /// <summary>
    /// Formats the status, listing the core when there is one.
    /// </summary>
    /// <returns>A human-readable summary.</returns>
    public override string ToString()
    {
        if (this.Status != SolveStatus.Unsatisfiable || this.UnsatCore.Count == 0)
        {
            return this.Status.ToString();
        }

        return $"{this.Status} (core: {string.Join(", ", this.UnsatCore.Select(core => core.ToString()))})";
    }
}
