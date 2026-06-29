namespace Z3.Linq;

/// <summary>
/// Selects which Z3 solver a <see cref="Z3Context"/> creates when solving a theorem.
/// Configurable solver/tactic selection is DSL backlog item B10 (#4616): the previous code
/// hard-coded <c>ctx.MkSolver()</c>, so a logic-specialized or lightweight solver could not
/// be chosen even when the constraint fragment allowed a faster one.
/// </summary>
public enum SolverKind
{
    /// <summary>
    /// Z3's general-purpose combined solver (<c>ctx.MkSolver()</c>). Default; preserves
    /// the historical behavior and accepts any supported constraint.
    /// </summary>
    Default = 0,

    /// <summary>
    /// The lightweight incremental solver (<c>ctx.MkSimpleSolver()</c>) — the solver the
    /// original endjin code referenced in a commented-out line. Useful for many small
    /// incremental checks.
    /// </summary>
    Simple = 1,

    /// <summary>
    /// A solver specialized to a named SMT-LIB logic (<c>ctx.MkSolver(logic)</c>); the logic
    /// is taken from <see cref="Z3Context.Logic"/> (e.g. <c>"QF_LIA"</c>, <c>"QF_BV"</c>).
    /// Faster on the restricted fragment, but rejects constraints outside it.
    /// </summary>
    Logic = 2,
}
