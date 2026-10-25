namespace Z3.Linq;

using Microsoft.Z3;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// The Z3 handles standing in for the members of an environment type while a theorem is solved.
/// </summary>
/// <remarks>
/// Built once per solve by walking the environment type. A scalar member gets a constant of its
/// type's sort in <see cref="Expr"/>, a collection gets an array from <c>Int</c> to that sort,
/// and a nested object gets an <see cref="Environment"/> of its own under
/// <see cref="Properties"/>, with no <see cref="Expr"/>. Public because
/// the translator takes one; there is no reason to build one directly.
/// </remarks>
public class Environment
{
    /// <summary>
    /// Gets or sets the Z3 handle for a scalar or collection symbol, or <see langword="null"/>
    /// for a nested object, which has <see cref="Properties"/> instead.
    /// </summary>
    public Expr? Expr { get; set; }

    /// <summary>
    /// Gets or sets whether this environment describes a collection of objects, in which case
    /// each entry in <see cref="Properties"/> is an array indexed by position rather than a
    /// single handle.
    /// </summary>
    /// <remarks>
    /// Set by the environment builder and read by nothing: neither the translator nor the
    /// marshaller supports a collection of objects. See #89.
    /// </remarks>
    public bool IsArray { get; set; }

    /// <summary>
    /// Gets the environments of the members of a nested object, keyed by member.
    /// </summary>
    public Dictionary<MemberInfo, Environment> Properties { get; private set; } = new Dictionary<MemberInfo, Environment>();
}

/// <summary>
/// How a collection symbol is modelled in Z3.
/// </summary>
public enum CollectionHandling
{
    /// <summary>
    /// One Z3 constant per element, created on first index access. This is the classic binding
    /// model, and it is the only one that can constrain individual elements by name.
    /// </summary>
    Constants,

    /// <summary>
    /// Z3 array theory: a single array constant driven by Select/Store. This is the default,
    /// and the only mode that supports a symbolic index.
    /// </summary>
    Array,
}

/// <summary>
/// The environment of a collection modelled as one Z3 constant per element
/// (<see cref="CollectionHandling.Constants"/>).
/// </summary>
/// <remarks>
/// The per-element environments are created lazily, on first index access, so a collection that
/// the constraints never read costs nothing. An element whose type is itself a collection gets a
/// further <see cref="MultipleEnvironment"/>, which is what lets <c>int[][]</c> recurse.
/// </remarks>
public class MultipleEnvironment : Environment
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MultipleEnvironment"/> class.
    /// </summary>
    /// <param name="prefix">Symbol prefix used to name the per-element constants.</param>
    /// <param name="elementType">Element type of the modelled collection.</param>
    public MultipleEnvironment(string prefix, Type elementType)
    {
        this.Prefix = prefix;
        this.ElementType = elementType;
    }

    /// <summary>
    /// Gets the symbol prefix used to name the per-element constants.
    /// </summary>
    public string Prefix { get; }

    /// <summary>
    /// Gets the element type of the modelled collection.
    /// </summary>
    public Type ElementType { get; }

    /// <summary>
    /// Gets the per-element environments, keyed by the index they were created for.
    /// </summary>
    public Dictionary<object, Environment> SubEnvironments { get; } = new Dictionary<object, Environment>();
}
