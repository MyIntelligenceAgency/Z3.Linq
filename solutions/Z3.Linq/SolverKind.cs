namespace Z3.Linq;

/// <summary>
/// Selects which Z3 solver a <see cref="Z3Context"/> builds when a theorem is checked.
/// </summary>
/// <remarks>
/// The solve path used to hard-code <c>Context.MkSolver()</c>, so a solver specialised to the
/// constraint fragment could not be chosen even when the fragment was known - and a restricted
/// solver is markedly faster than the general-purpose one on the logic it was built for.
/// </remarks>
public enum SolverKind
{
    /// <summary>
    /// Z3's general-purpose combined solver. The default, and the one that accepts any supported
    /// constraint.
    /// </summary>
    Default = 0,

    /// <summary>
    /// The lightweight incremental solver, for many small incremental checks.
    /// </summary>
    /// <remarks>
    /// Worth knowing before choosing it: it does not support the hypothesis tracking an
    /// unsatisfiable core is extracted from, so a diagnostic path stays on the combined solver
    /// whatever this says.
    /// </remarks>
    Simple = 1,

    /// <summary>
    /// A solver specialised to the SMT-LIB logic named by <see cref="Z3Context.Logic"/>.
    /// </summary>
    /// <remarks>
    /// Faster on the restricted fragment. The logic name is a strategy hint for Z3, not a type
    /// gate: a solver built with <c>ctx.MkSolver(logic)</c> still answers constraints outside
    /// the named fragment (measured on Z3 4.12.2 - QF_BV, QF_LIA and QF_NIA solvers each
    /// returned SATISFIABLE on off-fragment formulas), so it must not be relied on to reject
    /// them.
    /// </remarks>
    Logic = 2,
}
