namespace Z3.Linq;

using Microsoft.Z3;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Controls how collection properties are modeled in Z3.
/// Constants = one Z3 constant per element (enables constraints on individual elements).
/// Array = Z3 array theory (single ArrayExpr with Select/Store).
/// </summary>
public enum CollectionHandling
{
    Constants,
    Array
}

public class Environment
{
    public Expr? Expr { get; set; }

    public bool IsArray { get; set; }

    public Dictionary<MemberInfo, Environment> Properties { get; private set; } = new Dictionary<MemberInfo, Environment>();
}

/// <summary>
/// Environment for a collection modeled as individual Z3 constants (CollectionHandling.Constants).
/// Lazily creates sub-environments per element index on access.
/// </summary>
public class MultipleEnvironment : Environment
{
    public MultipleEnvironment(string prefix, Type elementType)
    {
        Prefix = prefix;
        ElementType = elementType;
    }

    public string Prefix { get; set; }
    public Type ElementType { get; set; }
    public Dictionary<object, Environment> SubEnvironments { get; set; } = new Dictionary<object, Environment>();
}