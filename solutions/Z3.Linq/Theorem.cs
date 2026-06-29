namespace Z3.Linq;

using Microsoft.Z3;

using MiaPlaza.ExpressionUtils;
using MiaPlaza.ExpressionUtils.Evaluating;

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
    /// Theorem constraints (hard: must hold for a solution to exist).
    /// </summary>
    private readonly IEnumerable<LambdaExpression> constraints;

    /// <summary>
    /// Soft theorem constraints (weighted: each may be violated at the cost of its weight; the solver
    /// minimizes the total weight of violated soft constraints — weighted MaxSAT, gap B1 of #4616).
    /// </summary>
    private readonly IEnumerable<SoftConstraint> softConstraints;

    /// <summary>
    /// Z3 context under which the theorem is solved.
    /// </summary>
    private readonly Z3Context context;

    /// <summary>
    /// A soft (weighted) constraint: a boolean predicate that the solver tries — but is not required — to
    /// satisfy. Violating it costs <see cref="Weight"/>; the optimizer minimizes the sum of violated weights.
    /// Constraints sharing a <see cref="Group"/> name are aggregated into one MaxSAT objective by Z3.
    /// </summary>
    /// <param name="Constraint">The boolean predicate lambda over the theorem environment.</param>
    /// <param name="Weight">The penalty incurred when the constraint is left unsatisfied (must be &gt; 0).</param>
    /// <param name="Group">The MaxSAT objective group the constraint belongs to.</param>
    protected internal readonly record struct SoftConstraint(LambdaExpression Constraint, int Weight, string Group);

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
    /// <param name="constraints">Hard constraints to apply to the created theorem.</param>
    protected Theorem(Z3Context context, IEnumerable<LambdaExpression> constraints)
        : this(context, constraints, new List<SoftConstraint>())
    {
    }

    /// <summary>
    /// Creates a new pre-constrained theorem (hard + soft) for the given Z3 context.
    /// </summary>
    /// <param name="context">Z3 context.</param>
    /// <param name="constraints">Hard constraints to apply to the created theorem.</param>
    /// <param name="softConstraints">Weighted soft constraints (MaxSAT) to apply to the created theorem.</param>
    protected Theorem(Z3Context context, IEnumerable<LambdaExpression> constraints, IEnumerable<SoftConstraint> softConstraints)
    {
        this.context = context;
        this.constraints = constraints;
        this.softConstraints = softConstraints;
    }

    /// <summary>
    /// Gets the hard constraints of the theorem.
    /// </summary>
    protected IEnumerable<LambdaExpression> Constraints => constraints;

    /// <summary>
    /// Gets the soft (weighted MaxSAT) constraints of the theorem.
    /// </summary>
    protected IEnumerable<SoftConstraint> SoftConstraints => softConstraints;

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
        return Solve<T>(null);
    }

    /// <summary>
    /// Solves the theorem and, if requested, invokes <paramref name="inspect"/> with a witness evaluator
    /// while the Z3 model and context are still alive (gap B5 of #4616). The evaluator translates an arbitrary
    /// sub-expression over the theorem environment and evaluates it under the satisfying model — exposing the
    /// model that <c>Solve&lt;T&gt;()</c> otherwise computes then discards. The witness is valid only for the
    /// duration of the callback (the Z3 context is disposed when this method returns).
    /// </summary>
    /// <typeparam name="T">Theorem environment type.</typeparam>
    /// <param name="inspect">Optional callback receiving a witness evaluator; not invoked on UNSAT.</param>
    /// <returns>Result of solving the theorem; default(T) if the theorem cannot be satisfied.</returns>
    protected T? Solve<T>(Action<ModelWitness<T>>? inspect)
    {
        using Context ctx = this.context.CreateContext();
        var environment = GetEnvironment(ctx, typeof(T));

        Model? model;

        // Soft (weighted MaxSAT) constraints require an Optimize object: AssertSoft defines an implicit
        // minimize-total-violated-weight objective with no explicit Maximize/Minimize term. A plain Solver
        // cannot carry soft constraints, so route through the optimizer whenever any are present (B1, #4616).
        if (softConstraints.Any())
        {
            Optimize softOptimizer = ctx.MkOptimize();
            AssertConstraints<T>(ctx, softOptimizer, environment);
            model = softOptimizer.Check() == Status.SATISFIABLE ? softOptimizer.Model : null;
        }
        else
        {
            // Solver solver = context.MkSimpleSolver();
            Solver solver = ctx.MkSolver();
            AssertConstraints<T>(ctx, solver, environment);
            model = solver.Check() == Status.SATISFIABLE ? solver.Model : null;
        }

        if (model == null)
        {
            return default;
        }

        // Witness inspection (B5): hand the caller an evaluator over the live model before disposing the
        // context, so it can read back the model value of any sub-expression (not just the bound result type).
        inspect?.Invoke(new ModelWitness<T>(ctx, model, environment));

        return GetSolution<T>(ctx, model, environment);
    }

    /// <summary>
    /// Evaluates arbitrary sub-expressions over the theorem environment under a satisfying Z3 model
    /// (witness generation, B5 of #4616). Handed to the caller by <see cref="Solve{T}(Action{ModelWitness{T}})"/>
    /// while the model and context are alive. Reuses the same <see cref="ExpressionVisitor"/> + scalar-conversion
    /// pipeline that <see cref="Solve{T}()"/> uses for the result type, so a witness value agrees with the
    /// corresponding member of the returned solution object.
    /// </summary>
    /// <typeparam name="T">Theorem environment type.</typeparam>
    public sealed class ModelWitness<T>
    {
        private readonly Context context;
        private readonly Model model;
        private readonly Environment environment;

        internal ModelWitness(Context context, Model model, Environment environment)
        {
            this.context = context;
            this.model = model;
            this.environment = environment;
        }

        /// <summary>
        /// Translates <paramref name="expression"/> to a Z3 expression and evaluates it under the model.
        /// </summary>
        /// <typeparam name="TResult">Scalar result type (int/long/bool/double/decimal/string/DateTime).</typeparam>
        /// <param name="expression">A sub-expression over the theorem environment, e.g. <c>t =&gt; t.X + t.Y</c>.</param>
        /// <returns>The value of the sub-expression under the satisfying model.</returns>
        public TResult Eval<TResult>(Expression<Func<T, TResult>> expression)
        {
            var body = PartialEvaluator.PartialEval(expression.Body, ExpressionInterpreter.Instance);
            Expr handle = ExpressionVisitor.Visit(context, environment, body, expression.Parameters[0]);
            Expr value = model.Eval(handle, true);
            return (TResult)ConvertScalarExpr(value, typeof(TResult), context, model, environment, ResultMember)!;
        }
    }

    // Placeholder MemberInfo for witness scalar conversion error messages (no real member is being extracted).
    private static readonly MemberInfo ResultMember = typeof(Theorem).GetMethod(nameof(ToString))!;

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
        var constraintsToAssert = GetConstraintsToAssert<T>();

        // Visit, assert and log.
        foreach (var constraint in constraintsToAssert)
        {
            // Partially evaluate the constraint body before visiting. This folds host-captured
            // constants (locals declared outside the theorem's parameter scope, e.g. in another
            // .NET Interactive "submission" or closure) into literal ConstantExpressions, which
            // sidesteps the reflective MemberExpression resolution in ExpressionVisitor.VisitMember
            // that crashes across dynamic-assembly boundaries -- see issue #2:
            // "Field 'X' defined on type 'Submission#N' is not a field on the target object of
            // type 'Submission#M'". Subtrees that reference the theorem parameter (e.g. t.Values[i])
            // are left untouched because they are not evaluable without a parameter binding.
            var body = PartialEvaluator.PartialEval(constraint.Body, ExpressionInterpreter.Instance);
            BoolExpr expression = (BoolExpr)ExpressionVisitor.Visit(context, environment, body, constraint.Parameters[0]);

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

        // Soft (weighted MaxSAT) constraints: only an Optimize object can carry them. Each is asserted
        // with its weight and group, so Z3 minimizes the total weight of the soft constraints it leaves
        // unsatisfied (B1, #4616). A plain Solver silently ignores them — but Solve<T>() routes through the
        // optimizer whenever any soft constraint exists, so the Solver branch is only ever reached with none.
        if (approach is Optimize softOptimize)
        {
            foreach (var soft in softConstraints)
            {
                var softBody = PartialEvaluator.PartialEval(soft.Constraint.Body, ExpressionInterpreter.Instance);
                BoolExpr softExpr = (BoolExpr)ExpressionVisitor.Visit(context, environment, softBody, soft.Constraint.Parameters[0]);

                softOptimize.AssertSoft(softExpr, (uint)soft.Weight, soft.Group);
                this.context.LogWriteLine($"[soft w={soft.Weight} g={soft.Group}] {softExpr}");
            }
        }
    }

    /// <summary>
    /// Resolves the hard constraints to assert, applying a registered <see cref="TheoremGlobalRewriterAttribute"/>
    /// if present. Shared by <see cref="AssertConstraints{T}"/> (solve path) and <see cref="Explain{T}"/>
    /// (diagnostic path, B6).
    /// </summary>
    private IEnumerable<LambdaExpression> GetConstraintsToAssert<T>()
    {
        // Global rewriter registered?
        var rewriterAttr = typeof(T).GetCustomAttributes<TheoremGlobalRewriterAttribute>(false).SingleOrDefault();

        if (rewriterAttr == null)
        {
            return this.constraints;
        }

        // Make sure the specified rewriter type implements the ITheoremGlobalRewriter.
        var rewriterType = rewriterAttr.RewriterType;

        if (!typeof(ITheoremGlobalRewriter).IsAssignableFrom(rewriterType))
        {
            throw new InvalidOperationException("Invalid global rewriter type definition. Did you implement ITheoremGlobalRewriter?");
        }

        // Assume a parameterless public constructor to new up the rewriter.
        var rewriter = (ITheoremGlobalRewriter)Activator.CreateInstance(rewriterType)!;

        // Do the rewrite.
        return rewriter.Rewrite(this.constraints);
    }

    /// <summary>
    /// Diagnoses the theorem's satisfiability without extracting a solution object (gap B6 of #4616). Unlike
    /// <see cref="Solve{T}()"/> — which collapses every non-SAT outcome to <c>default</c> — this distinguishes
    /// <see cref="SolveStatus.Satisfiable"/>, <see cref="SolveStatus.Unsatisfiable"/> and
    /// <see cref="SolveStatus.Unknown"/>, and on UNSAT returns the minimal <b>UNSAT core</b>: the subset of the
    /// hard <c>.Where</c> constraints (by their original 0-based index and source expression) that are jointly
    /// unsatisfiable. Each constraint is asserted under a fresh tracking literal so Z3 can report which ones
    /// participate in the conflict.
    /// </summary>
    /// <remarks>
    /// Soft (MaxSAT) constraints are intentionally ignored here: they never cause UNSAT (they are sacrificed
    /// instead), so the diagnostic concerns only the hard constraints. <see cref="Solve{T}()"/> is left
    /// completely unchanged — B6 adds a parallel diagnostic surface rather than altering the solve contract.
    /// </remarks>
    /// <typeparam name="T">Theorem environment type.</typeparam>
    /// <returns>An <see cref="Explanation"/> describing the status and, on UNSAT, the conflicting constraints.</returns>
    protected Explanation Explain<T>()
    {
        using Context ctx = this.context.CreateContext();
        var environment = GetEnvironment(ctx, typeof(T));

        // UNSAT-core extraction needs assumption tracking on the solver.
        Solver solver = ctx.MkSolver();

        var constraintList = GetConstraintsToAssert<T>().ToList();
        var trackerToConstraint = new Dictionary<string, ConstraintRef>(constraintList.Count);

        for (int i = 0; i < constraintList.Count; i++)
        {
            var constraint = constraintList[i];
            var body = PartialEvaluator.PartialEval(constraint.Body, ExpressionInterpreter.Instance);
            BoolExpr expression = (BoolExpr)ExpressionVisitor.Visit(ctx, environment, body, constraint.Parameters[0]);

            // A fresh boolean tracking literal per constraint: AssertAndTrack ties the constraint to it, so a
            // returned UnsatCore lists exactly the trackers (hence constraints) that participate in the conflict.
            string trackerName = $"__track_{i}";
            BoolExpr tracker = ctx.MkBoolConst(trackerName);
            trackerToConstraint[trackerName] = new ConstraintRef(i, constraint.Body.ToString());
            solver.AssertAndTrack(expression, tracker);
        }

        Status status = solver.Check();

        switch (status)
        {
            case Status.SATISFIABLE:
                return new Explanation(SolveStatus.Satisfiable, Array.Empty<ConstraintRef>());

            case Status.UNKNOWN:
                return new Explanation(SolveStatus.Unknown, Array.Empty<ConstraintRef>());

            default:
                var core = solver.UnsatCore
                    .Select(tracker => trackerToConstraint.TryGetValue(tracker.ToString(), out var cref) ? cref : (ConstraintRef?)null)
                    .Where(cref => cref.HasValue)
                    .Select(cref => cref!.Value)
                    .OrderBy(cref => cref.Index)
                    .ToArray();

                return new Explanation(SolveStatus.Unsatisfiable, core);
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

            // Constants mode does not materialize a Z3 constant for an index that the constraints never
            // reference (e.g. V[2] left free while V[0] and V[1] are constrained, or any interior gap).
            // Such an index is unconstrained, so any assignment satisfies the theorem; we materialize it
            // as the element type's default. Without this, a null leaks into a value-type result array and
            // ArrayList.ToArray(valueType) throws InvalidCastException ("could not be cast down to the
            // destination array type") — observed for bool[] and for int[] with an interior index gap.
            if (elementVal == null && eltType.IsValueType)
            {
                elementVal = Activator.CreateInstance(eltType);
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
