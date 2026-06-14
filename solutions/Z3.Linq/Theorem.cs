namespace Z3.Linq;

using Microsoft.Z3;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

/// <summary>
/// Representation of a theorem with its constraints.
/// </summary>
public class Theorem
{
    /// <summary>
    /// Theorem constraints.
    /// </summary>
    private readonly IEnumerable<LambdaExpression> constraints;

    /// <summary>
    /// Z3 context under which the theorem is solved.
    /// </summary>
    private readonly Z3Context context;

    /// <summary>
    /// Creates a new theorem for the given Z3 context.
    /// </summary>
    /// <param name="context">Z3 context.</param>
    protected Theorem(Z3Context context)
        : this(context, new List<LambdaExpression>())
    {
    }

    /// <summary>
    /// Creates a new pre-constrained theorem for the given Z3 context.
    /// </summary>
    /// <param name="context">Z3 context.</param>
    /// <param name="constraints">Constraints to apply to the created theorem.</param>
    protected Theorem(Z3Context context, IEnumerable<LambdaExpression> constraints)
    {
        this.context = context;
        this.constraints = constraints;
    }

    /// <summary>
    /// Gets the constraints of the theorem.
    /// </summary>
    protected IEnumerable<LambdaExpression> Constraints => constraints;

    /// <summary>
    /// Gets the Z3 context under which the theorem is solved.
    /// </summary>
    protected Z3Context Context => context;

    /// <summary>
    /// Controls how collection (array/IEnumerable) properties are modeled in Z3.
    /// <list type="bullet">
    /// <item><term>Array</term><description>Z3 array theory: a single <c>ArrayExpr</c> with <c>Select</c>/<c>Store</c> (supports nested <c>int[][]</c>).</description></item>
    /// <item><term>Constants</term><description>One Z3 constant per element, created lazily on index access via <see cref="MultipleEnvironment"/> (the classic endjin binding model).</description></item>
    /// </list>
    /// Default is <see cref="CollectionHandling.Array"/> to preserve existing behavior (incl. nested-array support).
    /// </summary>
    public CollectionHandling DefaultCollectionHandling { get; set; } = CollectionHandling.Array;

    /// <summary>
    /// Returns a comma-separated representation of the constraints embodied in the theorem.
    /// </summary>
    /// <returns>Comma-separated string representation of the theorem's constraints.</returns>
    public override string ToString()
    {
        return string.Join(", ", (from c in constraints select c.Body.ToString()).ToArray());
    }

    /// <summary>
    /// Solves the theorem using Z3.
    /// </summary>
    /// <typeparam name="T">Theorem environment type.</typeparam>
    /// <returns>Result of solving the theorem; default(T) if the theorem cannot be satisfied.</returns>
    protected T? Solve<T>()
    {
        using Context ctx = this.context.CreateContext();
        var environment = GetEnvironment(ctx, typeof(T));

        // Solver solver = context.MkSimpleSolver();
        Solver solver = ctx.MkSolver();

        AssertConstraints<T>(ctx, solver, environment);

        Status status = solver.Check();

        if (status != Status.SATISFIABLE)
        {
            return default;
        }

        return GetSolution<T>(ctx, solver.Model, environment);
    }

    /// <summary>
    /// Solves the theorem using Z3.
    /// </summary>
    /// <typeparam name="T">Theorem environment type.</typeparam>
    /// <typeparam name="TResult">The Theorem Result.</typeparam>
    /// <returns>Result of solving the theorem; default(T) if the theorem cannot be satisfied.</returns>
    protected T Optimize<T, TResult>(Optimization direction, Expression<Func<T, TResult>> lambda)
    {
        using Context ctx = this.context.CreateContext();
        var environment = GetEnvironment(ctx, typeof(T));

        Optimize optimizer = ctx.MkOptimize();

        AssertConstraints<T>(ctx, optimizer, environment);

        var expression = ExpressionVisitor.Visit(ctx, environment, lambda.Body, lambda.Parameters[0]);

        switch (direction)
        {
            case Optimization.Maximize:
                optimizer.MkMaximize(expression);
                break;
            case Optimization.Minimize:
                optimizer.MkMinimize(expression);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
        }

        Status status = optimizer.Check();

        if (status != Status.SATISFIABLE)
        {
            return default!;
        }

        return GetSolution<T>(ctx, optimizer.Model, environment);
    }

    /// <summary>
    /// Asserts the theorem constraints on the Z3 context.
    /// </summary>
    /// <param name="context">Z3 context.</param>
    /// <param name="approach"></param>
    /// <param name="environment">Environment with bindings of theorem variables to Z3 handles.</param>
    /// <typeparam name="T">Theorem environment type.</typeparam>
    private void AssertConstraints<T>(Context context, Z3Object approach, Environment environment)
    {
        var constraintsToAssert = this.constraints;

        // Global rewriter registered?
        var rewriterAttr = typeof(T).GetCustomAttributes<TheoremGlobalRewriterAttribute>(false).SingleOrDefault();

        if (rewriterAttr != null)
        {
            // Make sure the specified rewriter type implements the ITheoremGlobalRewriter.
            var rewriterType = rewriterAttr.RewriterType;

            if (!typeof(ITheoremGlobalRewriter).IsAssignableFrom(rewriterType))
            {
                throw new InvalidOperationException("Invalid global rewriter type definition. Did you implement ITheoremGlobalRewriter?");
            }

            // Assume a parameterless public constructor to new up the rewriter.
            var rewriter = (ITheoremGlobalRewriter)Activator.CreateInstance(rewriterType)!;

            // Do the rewrite.
            constraintsToAssert = rewriter.Rewrite(constraintsToAssert);
        }

        // Visit, assert and log.
        foreach (var constraint in constraintsToAssert)
        {
            BoolExpr expression = (BoolExpr)ExpressionVisitor.Visit(context, environment, constraint.Body, constraint.Parameters[0]);

            switch (approach)
            {
                case Solver solver:
                    solver.Assert(expression);
                    break;
                case Optimize optimize:
                    optimize.Assert(expression);
                    break;
            }

            this.context.LogWriteLine(expression.ToString());
        }
    }

    private Environment GetEnvironment(Context context, Type targetType)
    {
        return GetEnvironment(context, targetType, targetType.Name, false);
    }

    private Environment GetEnvironment(Context context, Type targetType, string prefix, bool isArray)
    {
        var toReturn = new Environment();

        if (isArray || targetType.IsArray || (targetType.IsGenericType && typeof(IEnumerable).IsAssignableFrom(targetType.GetGenericTypeDefinition())))
        {
            Type? elType;

            if (targetType.IsArray)
            {
                elType = targetType.GetElementType();
            }
            else
            {
                elType = targetType.GetGenericArguments()[0];
            }

            // Constants mode: one Z3 constant per element, lazily created on index access.
            // The element sub-environments are materialized by the expression visitor when an
            // ArrayIndex node is encountered (see ExpressionVisitor.ResolveArrayElement).
            if (DefaultCollectionHandling == CollectionHandling.Constants)
            {
                return new MultipleEnvironment(prefix, elType!);
            }

            switch (Type.GetTypeCode(elType))
            {
                case TypeCode.String:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.DateTime:
                case TypeCode.Boolean:
                case TypeCode.Single:
                case TypeCode.Decimal:
                case TypeCode.Double:
                {
                    // Simple scalar array: create Z3 array with appropriate sorts
                    Sort arrDomain = GetArrayDomainSort(context, elType!);
                    Sort arrRange = GetArrayRangeSort(context, elType!);
                    toReturn.Expr = context.MkArrayConst(prefix, arrDomain, arrRange);
                    break;
                }
                case TypeCode.Object:
                {
                    // Nested array (e.g. int[][]) or collection of complex objects
                    if (elType!.IsArray || (elType.IsGenericType && typeof(IEnumerable).IsAssignableFrom(elType.GetGenericTypeDefinition())))
                    {
                        // Nested array: create an array whose range sort is the inner array sort.
                        // Recursively compute the inner array environment to get its sort.
                        var innerEnv = GetEnvironment(context, elType, prefix, true);
                        toReturn.Expr = context.MkArrayConst(prefix, context.IntSort, ((ArrayExpr)innerEnv.Expr!).Sort);
                        toReturn.IsArray = true;
                        toReturn.Properties[typeof(Array)] = innerEnv;
                    }
                    else
                    {
                        // Collection of complex objects (e.g. List<MyObject>)
                        toReturn.IsArray = true;

                        foreach (PropertyInfo parameter in elType!.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                        {
                            var newPrefix = parameter.Name;

                            if (!string.IsNullOrEmpty(prefix))
                            {
                                newPrefix = $"{prefix}_{newPrefix}";
                            }

                            toReturn.Properties[parameter] = GetEnvironment(context, parameter, newPrefix, true);
                        }
                    }

                    return toReturn;
                }
                default:
                    throw new NotSupportedException($"Unsupported member type {targetType.FullName}");
            }
        }
        else
        {
            // Scalar leaf type: create a Z3 constant
            switch (Type.GetTypeCode(targetType))
            {
                case TypeCode.String:
                    toReturn.Expr = context.MkConst(prefix, context.StringSort);
                    return toReturn;
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.DateTime:
                    toReturn.Expr = context.MkIntConst(prefix);
                    return toReturn;
                case TypeCode.Boolean:
                    toReturn.Expr = context.MkBoolConst(prefix);
                    return toReturn;
                case TypeCode.Single:
                case TypeCode.Decimal:
                case TypeCode.Double:
                    toReturn.Expr = context.MkRealConst(prefix);
                    return toReturn;
                case TypeCode.Object:
                    // Complex object: recurse into its properties/fields
                    break;
                default:
                    throw new NotSupportedException($"Unsupported parameter type for {prefix} ({targetType.FullName}).");
            }

            foreach (var parameter in targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var newPrefix = parameter.Name;
                if (!string.IsNullOrEmpty(prefix))
                {
                    newPrefix = $"{prefix}_{newPrefix}";
                }

                toReturn.Properties[parameter] = GetEnvironment(context, parameter, newPrefix, false);
            }

            foreach (var parameter in targetType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var newPrefix = parameter.Name;
                if (!string.IsNullOrEmpty(prefix))
                {
                    newPrefix = $"{prefix}_{newPrefix}";
                }

                toReturn.Properties[parameter] = GetEnvironment(context, parameter, newPrefix, false);
            }
        }

        return toReturn;
    }

    private Environment GetEnvironment(Context context, MemberInfo parameter, string prefix, bool isArray)
    {
        var parameterType = parameter switch
        {
            PropertyInfo parameterProperty => parameterProperty.PropertyType,
            FieldInfo parameterField => parameterField.FieldType,
            _ => throw new NotSupportedException(),
        };

        TheoremVariableTypeMappingAttribute? parameterTypeMapping = parameterType.GetCustomAttributes<TheoremVariableTypeMappingAttribute>(false).SingleOrDefault();

        if (parameterTypeMapping != null)
        {
            parameterType = parameterTypeMapping.RegularType;
        }

        // Delegate to the Type-based overload
        return GetEnvironment(context, parameterType, prefix, isArray);
    }

    /// <summary>
    /// Gets the Z3 domain sort for an array element type (used as the index sort).
    /// </summary>
    private static Sort GetArrayDomainSort(Context context, Type elementType)
    {
        // Arrays are indexed by integers in Z3
        return context.IntSort;
    }

    /// <summary>
    /// Gets the Z3 range sort for a scalar element type.
    /// </summary>
    private static Sort GetArrayRangeSort(Context context, Type elementType)
    {
        switch (Type.GetTypeCode(elementType))
        {
            case TypeCode.String:
                return context.StringSort;
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
            case TypeCode.DateTime:
                return context.IntSort;
            case TypeCode.Boolean:
                return context.BoolSort;
            case TypeCode.Single:
            case TypeCode.Decimal:
            case TypeCode.Double:
                return context.RealSort;
            default:
                throw new NotSupportedException($"Unsupported array element type {elementType.FullName}");
        }
    }

    private static object ConvertZ3Expression(object destinationObject, Context context, Model model, Environment subEnv, MemberInfo parameter)
    {
        // Normalize types when facing Z3. Theorem variable type mappings allow for strong
        // typing within the theorem, while underlying variable representations are Z3-
        // friendly types.
        var parameterType = parameter switch
        {
            PropertyInfo parameterProperty => parameterProperty.PropertyType,
            FieldInfo parameterField => parameterField.FieldType,
            _ => throw new NotSupportedException(),
        };

        TheoremVariableTypeMappingAttribute? parameterTypeMapping = parameterType.GetCustomAttributes<TheoremVariableTypeMappingAttribute>(false).SingleOrDefault();

        if (parameterTypeMapping != null)
        {
            parameterType = parameterTypeMapping.RegularType;
        }

        object value;
        TypeCode typeCode = Type.GetTypeCode(parameterType);
        if (typeCode == TypeCode.Object)
        {
            if (parameterType.IsArray || (parameterType.IsGenericType && typeof(IEnumerable).IsAssignableFrom(parameterType.GetGenericTypeDefinition())))
            {
                // Delegate to ExtractCollection for both simple and nested array extraction
                object? existingMember = parameter switch
                {
                    PropertyInfo info => info.GetValue(destinationObject, null),
                    FieldInfo info1 => info1.GetValue(destinationObject),
                    _ => null
                };

                value = ExtractCollection(existingMember, context, model, subEnv, parameter, parameterType);
            }
            else
            {
                value = GetSolution(parameterType, context, model, subEnv);
            }
        }
        else
        {
            Expr subEnvExpr = subEnv.Expr ?? throw new ArgumentException(
                $"nameof(ConvertZ3Expression) requires {nameof(subEnv)}.{nameof(subEnv.Expr)} to be non-null",
                nameof(subEnv));

            Expr val = model.Eval(subEnvExpr);

            switch (typeCode)
            {
                case TypeCode.String:
                    value = val.String;
                    break;
                case TypeCode.Int16:
                case TypeCode.Int32:
                    value = ((IntNum)val).Int;
                    break;
                case TypeCode.Int64:
                    value = ((IntNum)val).Int64;
                    break;
                case TypeCode.DateTime:
                    value = DateTime.FromFileTime(((IntNum)val).Int64);
                    break;
                case TypeCode.Boolean:
                    value = val.IsTrue;
                    break;
                case TypeCode.Single:
                    value = double.Parse(((RatNum)val).ToDecimalString(32), CultureInfo.InvariantCulture);
                    break;
                case TypeCode.Decimal:

                    string decValue = ((RatNum)val).ToDecimalString(128);

                    ReadOnlySpan<char> decValueSpan = decValue.AsSpan();
                    if (decValue.EndsWith('?'))
                    {
                        decValueSpan = decValueSpan[..^1];
                    }

                    value = decimal.Parse(decValueSpan, NumberStyles.Number, CultureInfo.InvariantCulture);
                    break;
                case TypeCode.Double:
                    value = double.Parse(((RatNum)val).ToDecimalString(64), CultureInfo.InvariantCulture);
                    break;

                default:
                    throw new NotSupportedException("Unsupported parameter type for " + parameter.Name + ".");
            }
        }

        // If there was a type mapping, we need to convert back to the original type.
        // In that case we expect a constructor with the mapped type to be available.
        if (parameterTypeMapping != null)
        {
            if (parameter is PropertyInfo propertyInfo)
            {
                var ctor = propertyInfo.PropertyType.GetConstructor(new Type[] { parameterType });

                if (ctor == null)
                {
                    throw new InvalidOperationException("Could not construct an instance of the mapped type " + propertyInfo.PropertyType.Name + ". No public constructor with parameter type " + parameterType + " found.");
                }

                value = ctor.Invoke(new object[] { value! });
            }
            if (parameter is FieldInfo fieldInfo)
            {
                var ctor = fieldInfo.FieldType.GetConstructor(new Type[] { parameterType });

                if (ctor == null)
                {
                    throw new InvalidOperationException("Could not construct an instance of the mapped type " + fieldInfo.FieldType.Name + ". No public constructor with parameter type " + parameterType + " found.");
                }

                value = ctor.Invoke(new object[] { value! });
            }
        }

        return value!;
    }

    /// <summary>
    /// Extracts a collection (simple or nested array) from the Z3 model.
    /// Handles both flat arrays (int[]) and nested arrays (int[][]) recursively.
    /// </summary>
    private static object ExtractCollection(object? existingMember, Context context, Model model, Environment subEnv, MemberInfo parameter, Type parameterType)
    {
        Type eltType = parameterType.IsArray ? parameterType.GetElementType()! : parameterType.GetGenericArguments()[0];

        if (eltType == null)
        {
            throw new NotSupportedException("Unsupported untyped array parameter type for " + parameter.Name + ".");
        }

        var results = new ArrayList();

        var arrVal = subEnv.Expr as ArrayExpr;
        var multiEnv = subEnv as MultipleEnvironment;

        // Determine the collection length from the existing member
        int length = 0;
        if (existingMember != null)
        {
            var existingCollection = new ArrayList((ICollection)existingMember);
            length = existingCollection.Count;
        }

        // Constants mode: the largest materialized sub-environment index extends the length, so that
        // every lazily-created Z3 constant (Cells_0, Cells_1, …) is read back from the model.
        bool isConstantsMode = multiEnv != null && multiEnv.SubEnvironments.Count > 0;
        if (isConstantsMode)
        {
            int maxBound = multiEnv!.SubEnvironments.Keys.Max(k => Convert.ToInt32(k));
            length = Math.Max(length, maxBound + 1);
        }

        // Check if this is a nested array (e.g. int[][]) — stored in Properties[typeof(Array)]
        Environment? innerArrayEnv = null;
        bool isNestedArray = eltType.IsArray && subEnv.Properties.TryGetValue(typeof(Array), out innerArrayEnv);

        for (int i = 0; i < length; i++)
        {
            object? elementVal = null;

            // Constants mode: read the scalar/element Z3 constant straight from its sub-environment.
            if (isConstantsMode && multiEnv!.SubEnvironments.TryGetValue(i, out var subSubEnv))
            {
                if (subSubEnv is MultipleEnvironment nestedMulti)
                {
                    // Nested Constants collection (e.g. int[][] modeled as constants): recurse per row.
                    object? existingSubMember = null;
                    if (existingMember is ICollection outerCollection)
                    {
                        var outerList = new ArrayList(outerCollection);
                        if (i < outerList.Count)
                        {
                            existingSubMember = outerList[i];
                        }
                    }

                    elementVal = ExtractCollection(existingSubMember, context, model, nestedMulti, parameter, eltType);
                }
                else if (subSubEnv.Expr != null)
                {
                    Expr val = model.Eval(subSubEnv.Expr);
                    elementVal = ConvertScalarExpr(val, eltType, context, model, subEnv, parameter);
                }
            }
            else if (arrVal != null)
            {
                // For nested arrays, Select(outer, i) yields the inner array (row i).
                // For simple arrays, Select(arr, i) yields a scalar value.
                Expr rowExpr = model.Eval(context.MkSelect(arrVal, context.MkInt(i)));

                if (isNestedArray && innerArrayEnv != null)
                {
                    // rowExpr is (Array Int Int) for this row — build a temp environment
                    // holding it and recurse to extract the inner scalar array.
                    var rowEnv = new Environment { Expr = rowExpr };
                    object? existingSubMember = null;

                    if (existingMember is ICollection outerCollection)
                    {
                        var outerList = new ArrayList(outerCollection);
                        if (i < outerList.Count)
                        {
                            existingSubMember = outerList[i];
                        }
                    }

                    elementVal = ExtractCollection(existingSubMember, context, model, rowEnv, parameter, eltType);
                }
                else
                {
                    elementVal = ConvertScalarExpr(rowExpr, eltType, context, model, subEnv, parameter);
                }
            }

            results.Add(elementVal);
        }

        object value = parameterType.IsArray ? results.ToArray(eltType) : Activator.CreateInstance(parameterType, results.ToArray(eltType))!;

        return value;
    }

    /// <summary>
    /// Converts a Z3 scalar expression (as evaluated against a model) into a CLR value of the given
    /// element type. Shared by the Array-mode and Constants-mode collection extraction paths so the
    /// two modes agree on how Z3 values are materialized.
    /// </summary>
    /// <param name="numValExpr">Evaluated Z3 expression of the scalar (already passed through <c>model.Eval</c> for Array mode, or a constant for Constants mode).</param>
    /// <param name="eltType">CLR element type to convert into.</param>
    /// <param name="context">Z3 context (used for Decimal/Real evaluation).</param>
    /// <param name="model">Z3 model (used for Decimal precision evaluation).</param>
    /// <param name="subEnv">Environment of the collection (used to recurse for complex-object elements).</param>
    /// <param name="parameter">Member being extracted (for error messages).</param>
    /// <returns>CLR scalar value.</returns>
    private static object? ConvertScalarExpr(Expr numValExpr, Type eltType, Context context, Model model, Environment subEnv, MemberInfo parameter)
    {
        switch (Type.GetTypeCode(eltType))
        {
            case TypeCode.String:
                return numValExpr.String;
            case TypeCode.Int16:
            case TypeCode.Int32:
                return ((IntNum)numValExpr).Int;
            case TypeCode.Int64:
                return ((IntNum)numValExpr).Int64;
            case TypeCode.DateTime:
                return DateTime.FromFileTime(((IntNum)numValExpr).Int64);
            case TypeCode.Boolean:
                return numValExpr.IsTrue;
            case TypeCode.Single:
                return double.Parse(((RatNum)numValExpr).ToDecimalString(32), CultureInfo.InvariantCulture);
            case TypeCode.Decimal:
            {
                Expr val = model.Eval(numValExpr);
                string numValue = ((RatNum)val).ToDecimalString(128);
                ReadOnlySpan<char> numValueSpan = numValue.AsSpan();
                if (numValue.EndsWith('?'))
                {
                    numValueSpan = numValueSpan[..^1];
                }
                return decimal.Parse(numValueSpan, NumberStyles.Number, CultureInfo.InvariantCulture);
            }
            case TypeCode.Double:
                return double.Parse(((RatNum)numValExpr).ToDecimalString(64), CultureInfo.InvariantCulture);
            case TypeCode.Object:
                // Complex object element within a collection
                return GetSolution(eltType, context, model, subEnv);
            default:
                throw new NotSupportedException($"Unsupported array parameter type for {parameter.Name} and array element type {eltType.Name}.");
        }
    }

    /// <summary>
    /// Gets the solution object for the solved theorem.
    /// </summary>
    /// <typeparam name="T">Environment type to create an instance of.</typeparam>
    /// <param name="context">Z3 context.</param>
    /// <param name="model">Z3 model to evaluate theorem parameters under.</param>
    /// <param name="environment">Environment with bindings of theorem variables to Z3 handles.</param>
    /// <returns>Instance of the environment type with theorem-satisfying values.</returns>
    private static T GetSolution<T>(Context context, Model model, Environment environment)
    {
        Type t = typeof(T);
        return (T) GetSolution(t, context, model, environment);
    }

    /// <summary>
    /// Gets the solution object for the solved theorem.
    /// </summary>
    /// <param name="t">Environment type to create an instance of.</param>
    /// <param name="context">Z3 context.</param>
    /// <param name="model">Z3 model to evaluate theorem parameters under.</param>
    /// <param name="environment">Environment with bindings of theorem variables to Z3 handles.</param>
    /// <returns>Instance of the environment type with theorem-satisfying values.</returns>
    private static object GetSolution(Type t, Context context, Model model, Environment environment)
    {
        // Determine whether T is a compiler-generated type, indicating an anonymous type.
        // This check might not be reliable enough but works for now.
        if (t.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false).Any())
        {
            // Anonymous types have a constructor that takes in values for all its properties.
            // However, we don't know the order and it's hard to correlate back the parameters
            // to the underlying properties. So, we want to bypass that constructor altogether
            // by using the FormatterServices to create an uninitialized (all-zero) instance.
            object result = RuntimeHelpers.GetUninitializedObject(t);

            // Here we take advantage of undesirable knowledge on how anonymous types are
            // implemented by the C# compiler. This is risky but we can live with it for
            // now in this POC. Because the properties are get-only, we need to perform
            // nominal matching with the corresponding backing fields.
            var fields = t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var parameter in environment.Properties.Keys.Cast<PropertyInfo>())
            {
                // Mapping from property to field.
                var field = fields.SingleOrDefault(f => f.Name.StartsWith($"<{parameter.Name}>"));

                if (field == null)
                {
                    continue;
                }

                // Evaluation of the values though the handle in the environment bindings.
                var subEnv = environment.Properties[parameter];

                Expr val = model.Eval(subEnv.Expr);
                if (parameter.PropertyType == typeof(bool))
                {
                    field.SetValue(result, val.IsTrue);
                }
                else if (parameter.PropertyType == typeof(int))
                {
                    field.SetValue(result, ((IntNum)val).Int);
                }
                else
                {
                    throw new NotSupportedException("Unsupported parameter type for " + parameter.Name + ".");
                }
            }

            return result;
        }
        else
        {
            // Straightforward case of having an "onymous type" at hand.
            object result = Activator.CreateInstance(t)!;

            foreach (var parameter in environment.Properties.Keys)
            {
                if (parameter is PropertyInfo)
                {
                    var prop = parameter as PropertyInfo;

                    if (prop == null)
                    {
                        continue;
                    }

                    // Evaluation of the values though the handle in the environment bindings.
                    object value;

                    var subEnv = environment.Properties[prop];

                    value = ConvertZ3Expression(result, context, model, subEnv, prop);

                    prop.SetValue(result, value, null);
                }

                if (parameter is FieldInfo)
                {
                    var prop = parameter as FieldInfo;

                    if (prop == null)
                    {
                        continue;
                    }

                    // Evaluation of the values though the handle in the environment bindings.
                    object value;

                    var subEnv = environment.Properties[prop];

                    value = ConvertZ3Expression(result, context, model, subEnv, prop);

                    prop.SetValue(result, value);
                }
            }

            return result;
        }
    }
}
