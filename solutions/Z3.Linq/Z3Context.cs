namespace Z3.Linq;

using Microsoft.Z3;

using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// The solver parameters set through <see cref="SetParameter(string, uint)"/>, applied to every
    /// solver this context builds.
    /// </summary>
    private readonly Dictionary<string, SolverParameter> solverParameters = new(StringComparer.Ordinal);

    /// <summary>
    /// The type Z3 declares for a solver parameter, and therefore the type it must be given as.
    /// </summary>
    private enum ParameterKind
    {
        /// <summary>A symbol, the form Z3's own parameter output uses.</summary>
        Symbol,

        /// <summary>An unsigned integer.</summary>
        Unsigned,

        /// <summary>A Boolean.</summary>
        Boolean,

        /// <summary>A real.</summary>
        Real,
    }

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
    /// Gets or sets how long a single solve or optimisation may run before Z3 gives up, or
    /// <see langword="null"/> for no limit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Some theorems cannot be decided - nonlinear integer arithmetic is undecidable in general -
    /// and without a limit Z3 searches until the process is killed. When the limit is reached the
    /// solve throws <see cref="TheoremUndecidedException"/>: the theorem has been proved neither
    /// satisfiable nor unsatisfiable. A theorem Z3 can decide within the limit is unaffected.
    /// See #85.
    /// </para>
    /// <para>
    /// Wall-clock time, so the same theorem may or may not hit the limit on a different machine.
    /// <see cref="ResourceLimit"/> is the deterministic alternative.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is not positive, or exceeds what Z3 can represent in milliseconds.
    /// </exception>
    public TimeSpan? Timeout
    {
        get => field;
        set
        {
            if (value is { } t && (t <= TimeSpan.Zero || t.TotalMilliseconds > uint.MaxValue))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The timeout must be positive and no more than uint.MaxValue milliseconds.");
            }

            field = value;
        }
    }

    /// <summary>
    /// Gets or sets how much work Z3 may do on a single solve or optimisation before giving up,
    /// in Z3's own resource units, or <see langword="null"/> for no limit.
    /// </summary>
    /// <remarks>
    /// The deterministic sibling of <see cref="Timeout"/>: the same theorem does the same amount
    /// of work everywhere, so a limit that is reached on one machine is reached on all of them.
    /// The unit is Z3's <c>rlimit</c>, which has no fixed relationship to time. When the limit is
    /// reached the solve throws <see cref="TheoremUndecidedException"/>. See #85.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero.</exception>
    public uint? ResourceLimit
    {
        get => field;
        set
        {
            if (value is 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The resource limit must be positive.");
            }

            field = value;
        }
    }

    /// <summary>
    /// Gets or sets how collection symbols are modelled in Z3 for the theorems this context
    /// creates.
    /// </summary>
    /// <remarks>
    /// Propagated to every <see cref="Theorem{T}"/> built here, and re-propagated by its
    /// <c>Where</c>. The default, <see cref="CollectionHandling.Array"/>, keeps the behaviour a
    /// nested array has always had; <see cref="CollectionHandling.Constants"/> is the mode that
    /// names each element.
    /// </remarks>
    public CollectionHandling DefaultCollectionHandling { get; set; } = CollectionHandling.Array;

    /// <summary>
    /// Gets or sets which Z3 solver the theorems of this context are checked with.
    /// </summary>
    /// <remarks>
    /// Overridden by <see cref="Tactics"/> when that is non-empty, and ignored by the two paths
    /// that need an object other than a solver: the optimizer a soft constraint needs, and the
    /// diagnostic path, which needs the hypothesis tracking a combined solver provides.
    /// </remarks>
    public SolverKind SolverKind { get; set; } = SolverKind.Default;

    /// <summary>
    /// Gets or sets the SMT-LIB logic name (<c>"QF_LIA"</c>, <c>"QF_BV"</c>, ...) used when
    /// <see cref="SolverKind"/> is <see cref="SolverKind.Logic"/>.
    /// </summary>
    /// <remarks>Ignored for every other solver kind.</remarks>
    public string? Logic { get; set; }

    /// <summary>
    /// Gets or sets an ordered list of Z3 tactic names composed left to right, whose solver is used
    /// instead of the one <see cref="SolverKind"/> selects.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A tactic sequence already ends in a decision procedure, so it takes precedence over
    /// <see cref="SolverKind"/>: layering the two would be a no-op at best. Names are Z3's own -
    /// <c>"simplify"</c>, <c>"solve-eqs"</c>, <c>"smt"</c>, <c>"bit-blast"</c>, <c>"sat"</c>,
    /// <c>"qfbv"</c> - and an unknown one throws
    /// <see cref="Microsoft.Z3.Z3Exception"/> from Z3 when the solver is built. The available names
    /// are what <c>Context.TacticNames</c> enumerates.
    /// </para>
    /// <para>
    /// This is the surface that lets a caller drive the strategy a plain solver picks
    /// heuristically: simplification, equality propagation, bit-blasting to SAT.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// using var ctx = new Z3Context { Tactics = ["simplify", "bit-blast", "sat"] };
    /// </code>
    /// </example>
    public IReadOnlyList<string>? Tactics { get; set; }

    /// <summary>
    /// Sets a Z3 <em>solver</em> parameter, such as <c>random_seed</c> or <c>phase_selection</c>, to
    /// be applied to every solver this context builds.
    /// </summary>
    /// <param name="name">The Z3 parameter name.</param>
    /// <param name="value">Its value, as the type Z3 declares for that parameter.</param>
    /// <returns>The same context, so calls can be chained.</returns>
    /// <remarks>
    /// <para>
    /// These are the parameters of Z3's solver, applied per check like <see cref="Timeout"/> and
    /// <see cref="ResourceLimit"/>, so changing one affects the next solve rather than a context
    /// already in use. That is deliberately not the <em>global</em> configuration: Z3's global
    /// parameters are a different, much smaller set, and a name such as <c>random_seed</c> is
    /// rejected there with a printed warning rather than applied.
    /// </para>
    /// <para>
    /// The value is typed because Z3 types its parameters: <c>random_seed</c> takes an unsigned
    /// integer, and handing it a symbol fails when the solver is configured. The four overloads
    /// cover the four types Z3 accepts, so the caller states the type rather than having this
    /// library guess one from the text.
    /// </para>
    /// <para>
    /// An unknown name is Z3's to reject, when the solver is checked, rather than silently ignored
    /// here: the set of parameters is Z3's, and enumerating it in this library would go stale.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or white space.</exception>
    /// <example>
    /// <code>
    /// using var ctx = new Z3Context().SetParameter("random_seed", 42u);
    /// </code>
    /// </example>
    public Z3Context SetParameter(string name, uint value)
    {
        return this.SetParameter(name, ParameterKind.Unsigned, value.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Sets a Z3 solver parameter that takes a Boolean. See
    /// <see cref="SetParameter(string, uint)"/>.
    /// </summary>
    /// <param name="name">The Z3 parameter name.</param>
    /// <param name="value">Its value.</param>
    /// <returns>The same context, so calls can be chained.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or white space.</exception>
    public Z3Context SetParameter(string name, bool value)
    {
        return this.SetParameter(name, ParameterKind.Boolean, value.ToString());
    }

    /// <summary>
    /// Sets a Z3 solver parameter that takes a real. See <see cref="SetParameter(string, uint)"/>.
    /// </summary>
    /// <param name="name">The Z3 parameter name.</param>
    /// <param name="value">Its value.</param>
    /// <returns>The same context, so calls can be chained.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or white space.</exception>
    public Z3Context SetParameter(string name, double value)
    {
        return this.SetParameter(name, ParameterKind.Real, value.ToString("R", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Sets a Z3 solver parameter that takes a symbol - the form Z3's own output uses, such as
    /// <c>restart_strategy=luby</c>. See <see cref="SetParameter(string, uint)"/>.
    /// </summary>
    /// <param name="name">The Z3 parameter name.</param>
    /// <param name="value">Its value, as a symbol name.</param>
    /// <returns>The same context, so calls can be chained.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or white space.</exception>
    public Z3Context SetParameter(string name, string value)
    {
        return this.SetParameter(name, ParameterKind.Symbol, value);
    }

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
    /// Creates a new theorem from a template instance, which supplies the environment type and
    /// the length of any collection symbols in it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The type is inferred from the instance, which is what lets an anonymous type be used as
    /// an environment "on the fly". The instance is also the template for the solution: a
    /// collection symbol takes its length from the corresponding collection on the template,
    /// because a solution never changes a collection's length and has to get it from somewhere.
    /// That is what lets a value tuple or an anonymous type - neither of which has anywhere to
    /// put an initialiser - hold a collection at all. Where the type has an initialiser of its
    /// own the template still wins. See #78.
    /// </para>
    /// <para>
    /// Nothing else about the template is read. Its values do not constrain the theorem and do
    /// not reach the solution, and it is never written to.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// ctx.NewTheorem(new { x = default(int), y = default(int) }).Where(t => t.x > t.y)
    /// ctx.NewTheorem((Values: new int[3], Total: 0)).Where(t => t.Values[0] + t.Values[1] + t.Values[2] == t.Total)
    /// </code>
    /// </example>
    /// <typeparam name="T">Theorem environment type (typically inferred).</typeparam>
    /// <param name="template">
    /// An instance of the environment type. Its collections give the solution's their length;
    /// nothing else about it is used.
    /// </param>
    /// <returns>New theorem object based on the given environment.</returns>
    public Theorem<T> NewTheorem<T>(T template)
    {
        return new Theorem<T>(this, template) { DefaultCollectionHandling = this.DefaultCollectionHandling };
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
    /// Records a solver parameter, after checking the name the four overloads share.
    /// </summary>
    /// <param name="name">The Z3 parameter name.</param>
    /// <param name="kind">The type Z3 declares for the parameter.</param>
    /// <param name="value">Its value, in the textual form the kind is parsed from.</param>
    /// <returns>The same context, so calls can be chained.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or white space.</exception>
    private Z3Context SetParameter(string name, ParameterKind kind, string value)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A parameter name is required.", nameof(name));
        }

        this.solverParameters[name] = new SolverParameter(kind, value);
        return this;
    }

    /// <summary>
    /// The solver parameters carrying <see cref="Timeout"/>, <see cref="ResourceLimit"/> and
    /// whatever <see cref="SetParameter(string, uint)"/> was given, or <see langword="null"/> when
    /// none is set.
    /// </summary>
    /// <param name="context">The native context the parameters are created in.</param>
    /// <remarks>
    /// Applied per solver rather than through the context configuration, so that a parameter is a
    /// property of the <see cref="Z3Context"/> that can be changed between solves.
    /// </remarks>
    internal Params? CreateSolverParameters(Context context)
    {
        if (this.Timeout is null && this.ResourceLimit is null && this.solverParameters.Count == 0)
        {
            return null;
        }

        Params parameters = context.MkParams();

        foreach (KeyValuePair<string, SolverParameter> parameter in this.solverParameters)
        {
            parameter.Value.Apply(parameters, parameter.Key);
        }

        if (this.Timeout is { } t)
        {
            // Round up, not truncate: Z3's timeout is in whole milliseconds, and a positive
            // TimeSpan below one millisecond truncates to 0, which Z3 reads as "no timeout" - so
            // a caller who asked for a tiny timeout would silently get none. Ceiling keeps any
            // positive timeout at least one millisecond, and stays within uint (the setter bounds
            // the value at uint.MaxValue milliseconds).
            parameters.Add("timeout", (uint)Math.Ceiling(t.TotalMilliseconds));
        }

        if (this.ResourceLimit is { } r)
        {
            parameters.Add("rlimit", r);
        }

        return parameters;
    }

    /// <summary>
    /// Builds the solver a theorem of this context is checked with, from
    /// <see cref="Tactics"/> when that is set, and from <see cref="SolverKind"/> and
    /// <see cref="Logic"/> otherwise.
    /// </summary>
    /// <param name="context">The native context the solver is built in.</param>
    /// <returns>The configured solver.</returns>
    /// <exception cref="InvalidOperationException">
    /// <see cref="SolverKind"/> is <see cref="SolverKind.Logic"/> and <see cref="Logic"/> is not
    /// set - Z3 would otherwise silently build the general-purpose solver instead.
    /// </exception>
    internal Solver CreateSolver(Context context)
    {
        if (this.Tactics is { Count: > 0 })
        {
            return context.MkSolver(ComposeTactics(context));
        }

        return this.SolverKind switch
        {
            SolverKind.Simple => context.MkSimpleSolver(),
            SolverKind.Logic when string.IsNullOrEmpty(this.Logic) => throw new InvalidOperationException(
                $"{nameof(SolverKind)}.{nameof(SolverKind.Logic)} requires {nameof(Logic)} to be set to an "
                + "SMT-LIB logic name, e.g. \"QF_LIA\"."),
            SolverKind.Logic => context.MkSolver(this.Logic!),
            _ => context.MkSolver(),
        };
    }

    /// <summary>
    /// Composes <see cref="Tactics"/> into a single tactic, left to right.
    /// </summary>
    /// <param name="context">The native context each tactic is built in.</param>
    /// <returns>The composed tactic.</returns>
    /// <remarks>
    /// <c>AndThen</c> needs at least two tactics, so a one-element sequence is returned as it is
    /// rather than wrapped in a composite of one.
    /// </remarks>
    private Tactic ComposeTactics(Context context)
    {
        var names = this.Tactics!;
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
        Log?.WriteLine(s);
    }

    /// <summary>
    /// A solver parameter, held as the value Z3 is given along with the type it is given as.
    /// </summary>
    /// <param name="Kind">The type Z3 declares for the parameter.</param>
    /// <param name="Value">Its value, in the textual form the kind is parsed from.</param>
    private readonly record struct SolverParameter(ParameterKind Kind, string Value)
    {
        /// <summary>
        /// Adds the parameter to the set handed to a solver.
        /// </summary>
        /// <param name="parameters">The set being built for the next check.</param>
        /// <param name="name">The parameter's name.</param>
        internal void Apply(Params parameters, string name)
        {
            switch (this.Kind)
            {
                case ParameterKind.Unsigned:
                    parameters.Add(name, uint.Parse(this.Value, CultureInfo.InvariantCulture));
                    break;
                case ParameterKind.Boolean:
                    parameters.Add(name, bool.Parse(this.Value));
                    break;
                case ParameterKind.Real:
                    parameters.Add(name, double.Parse(this.Value, CultureInfo.InvariantCulture));
                    break;
                default:
                    parameters.Add(name, this.Value);
                    break;
            }
        }
    }
}