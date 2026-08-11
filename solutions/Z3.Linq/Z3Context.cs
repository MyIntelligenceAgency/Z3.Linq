namespace Z3.Linq;

using Microsoft.Z3;

using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Context object for Z3 theorem proving through LINQ. Manages the configuration
/// of the theorem prover and provides centralized infrastructure for logging.
/// </summary>
public sealed class Z3Context : IDisposable
{
    /// <summary>
    /// Z3 configuration object.
    /// </summary>
    private readonly Dictionary<string, string> config;

    /// <summary>
    /// Creates a new Z3 context for theorem proving.
    /// </summary>
    public Z3Context()
    {
        this.config = new Dictionary<string, string>
        {
            { "MODEL", "true" }
        };
    }

    /// <summary>
    /// Gets/sets the logger used for diagnostic output.
    /// </summary>
    public TextWriter? Log { get; set; }

    /// <summary>
    /// Selects which Z3 solver is created when a theorem is solved by this context.
    /// Default is <see cref="SolverKind.Default"/> (Z3's general-purpose combined solver,
    /// <c>ctx.MkSolver()</c>), which preserves existing behavior.
    /// </summary>
    public SolverKind SolverKind { get; set; } = SolverKind.Default;

    /// <summary>
    /// The SMT-LIB logic name (e.g. <c>"QF_LIA"</c>, <c>"QF_BV"</c>, <c>"LIA"</c>) used when
    /// <see cref="SolverKind"/> is <see cref="SolverKind.Logic"/>. A logic-specialized solver
    /// can be markedly faster on a restricted fragment, at the cost of rejecting constraints
    /// outside that fragment. Ignored for the other solver kinds.
    /// </summary>
    public string? Logic { get; set; }

    /// <summary>
    /// Optional ordered list of Z3 tactic names (e.g. <c>"simplify"</c>, <c>"solve-eqs"</c>,
    /// <c>"smt"</c>, <c>"bit-blast"</c>, <c>"sat"</c>, <c>"qfbv"</c>) composed left-to-right with
    /// <c>AndThen</c> to build the solver. When non-empty, this takes precedence over
    /// <see cref="SolverKind"/>: the composed tactic's solver is used instead of a plain
    /// <c>MkSolver</c>/<c>MkSimpleSolver</c>/<c>MkSolver(logic)</c>.
    ///
    /// Tactics expose Z3's proof-search pipeline (simplification, equality propagation,
    /// bit-blasting to SAT, ...) to the DSL — a tactic sequence lets the caller drive the
    /// strategy that a plain solver picks heuristically. Available names are enumerable via
    /// <c>Context.NumTactics</c>/<c>Context.TacticNames</c>; unknown names throw
    /// <see cref="Microsoft.Z3.Z3Exception"/> at solve time.
    /// </summary>
    /// <remarks>
    /// DSL backlog item B10 (#4616): <see cref="SolverKind"/> reached the solver half of
    /// "configurable solver/tactics"; this property reaches the tactics half. The two compose:
    /// a non-empty <see cref="Tactics"/> overrides <see cref="SolverKind"/> because a tactic
    /// already subsumes solver selection (a tactic ends in a SAT/SMT decision procedure).
    /// </remarks>
    /// <example>
    /// Bit-vector problem solved through the dedicated bit-blasting pipeline:
    /// <code>
    /// using var ctx = new Z3Context { Tactics = new[] { "simplify", "bit-blast", "sat" } };
    /// </code>
    /// </example>
    public IReadOnlyList<string>? Tactics { get; set; }

    /// <summary>
    /// Sets a Z3 module/global parameter (e.g. <c>"timeout"</c>, <c>"random_seed"</c>,
    /// <c>"unsat_core"</c>) applied when the underlying <see cref="Context"/> is created.
    /// Returns this context to allow fluent chaining.
    /// </summary>
    /// <param name="name">Parameter name.</param>
    /// <param name="value">Parameter value (Z3 parses booleans/integers from their string form).</param>
    /// <returns>This context.</returns>
    public Z3Context SetParameter(string name, string value)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Parameter name must be non-empty.", nameof(name));
        }

        this.config[name] = value ?? throw new ArgumentNullException(nameof(value));
        return this;
    }

    /// <summary>
    /// Gets/sets how collection (array/IEnumerable) properties are modeled in Z3 for theorems created
    /// by this context. Propagated to each <see cref="Theorem{T}"/> built via <see cref="NewTheorem{T}()"/>.
    /// Default is <see cref="CollectionHandling.Array"/> to preserve existing behavior (incl. nested int[][]
    /// support); set to <see cref="CollectionHandling.Constants"/> to model collections as one Z3 constant
    /// per element (the classic endjin binding model, resurrected).
    /// </summary>
    public CollectionHandling DefaultCollectionHandling { get; set; } = CollectionHandling.Array;

    /// <summary>
    /// Closes the native resources held by the Z3 theorem prover.
    /// </summary>
    public void Dispose()
    {
    }

    /// <summary>
    /// Creates a new theorem based on the given type to establish the environment with
    /// the variables constrained by the theorem.
    /// </summary>
    /// <typeparam name="T">Theorem environment type.</typeparam>
    /// <returns>New theorem object based on the given environment.</returns>
    public Theorem<T> NewTheorem<T>()
    {
        return new Theorem<T>(this) { DefaultCollectionHandling = this.DefaultCollectionHandling };
    }

    /// <summary>
    /// Creates a new theorem based on a skeleton object used to infer the environment
    /// type with the variables constrained by the theorem.
    ///
    /// This overload is useful if one wants to use an anonymous type "on the fly" to
    /// create a new theorem based on the type's properties as variables.
    /// </summary>
    /// <example>
    /// <code>
    /// ctx.NewTheorem(new { x = default(int), y = default(int) }).Where(t => t.x > t.y)
    /// </code>
    /// </example>
    /// <typeparam name="T">Theorem environment type (typically inferred).</typeparam>
    /// <param name="dummy">Dummy parameter, typically an anonymous type instance.</param>
    /// <returns>New theorem object based on the given environment.</returns>
    public Theorem<T> NewTheorem<T>(T dummy)
    {
        return new Theorem<T>(this) { DefaultCollectionHandling = this.DefaultCollectionHandling };
    }

    /// <summary>
    /// Factory method for Z3 contexts based on the given configuration.
    /// </summary>
    /// <returns>New Z3 context.</returns>
    internal Context CreateContext()
    {
        return new Context(config);
    }

    /// <summary>
    /// Creates the Z3 solver for a theorem according to <see cref="Tactics"/> (when set) or
    /// <see cref="SolverKind"/> and <see cref="Logic"/>. Centralizes the solver-factory choice
    /// that was previously hard-coded to <c>ctx.MkSolver()</c> in <see cref="Theorem"/>. (DSL
    /// backlog B10, #4616: solver half via <see cref="SolverKind"/>, tactics half via
    /// <see cref="Tactics"/>.)
    /// </summary>
    /// <param name="context">The native Z3 context the solver is created under.</param>
    /// <returns>The configured solver. A tactic-composed solver when <see cref="Tactics"/> is
    /// non-empty; otherwise the <see cref="SolverKind"/>-selected solver.</returns>
    internal Solver CreateSolver(Context context)
    {
        // Tactics take precedence over SolverKind: a tactic sequence already ends in a
        // decision procedure, so layering SolverKind on top would be a no-op or a contradiction.
        if (Tactics is { Count: > 0 })
        {
            return context.MkSolver(ComposeTactics(context));
        }

        switch (SolverKind)
        {
            case SolverKind.Simple:
                return context.MkSimpleSolver();

            case SolverKind.Logic:
                if (string.IsNullOrEmpty(Logic))
                {
                    throw new InvalidOperationException(
                        $"{nameof(SolverKind)}.{nameof(SolverKind.Logic)} requires {nameof(Logic)} " +
                        "to be set to an SMT-LIB logic name (e.g. \"QF_LIA\").");
                }

                return context.MkSolver(Logic);

            case SolverKind.Default:
            default:
                return context.MkSolver();
        }
    }

    /// <summary>
    /// Composes the <see cref="Tactics"/> sequence into a single Z3 tactic via
    /// <c>AndThen</c> (left-to-right sequencing). <c>AndThen(t1, t2, ...)</c> requires at least
    /// two tactics, so a single-element sequence is returned unwrapped.
    /// </summary>
    /// <param name="context">The native Z3 context used to build each tactic.</param>
    /// <returns>The composed tactic.</returns>
    /// <exception cref="Microsoft.Z3.Z3Exception">Thrown by Z3 if any name is not a known
    /// tactic (the exception carries the offending name; available names are enumerable via
    /// <c>Context.TacticNames</c>).</exception>
    private Tactic ComposeTactics(Context context)
    {
        var names = Tactics!;
        if (names.Count == 1)
        {
            return context.MkTactic(names[0]);
        }

        Tactic[] built = names.Select(context.MkTactic).ToArray();
        return context.AndThen(built[0], built[1], built[2..]);
    }

    /// <summary>
    /// Helpers to write diagnostic log output to the registered logger, if any.
    /// </summary>
    /// <param name="s">Log output string.</param>
    internal void LogWriteLine(string s)
    {
        if (Log != null)
        {
            Log.WriteLine(s);
        }
    }
}