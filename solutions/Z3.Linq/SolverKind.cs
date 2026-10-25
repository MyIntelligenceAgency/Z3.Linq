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
    /// Faster on the restricted fragment, and it rejects constraints outside it - which is the
    /// point when the fragment is what a broken constraint violates.
    /// </remarks>
    Logic = 2,
}
