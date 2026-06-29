namespace Z3.Linq;

using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

using MiaPlaza.ExpressionUtils;
using MiaPlaza.ExpressionUtils.Evaluating;

using Microsoft.Z3;

public static class ExpressionVisitor
{
    /// <summary>
    /// Main visitor method to translate the LINQ expression tree into a Z3 expression handle.
    /// </summary>
    /// <param name="context">Z3 context.</param>
    /// <param name="environment">Environment with bindings of theorem variables to Z3 handles.</param>
    /// <param name="expression">LINQ expression tree node to be translated.</param>
    /// <param name="param">Parameter used to express the constraint on.</param>
    /// <returns>Z3 expression handle.</returns>
    public static Expr Visit(Context context, Environment environment, Expression expression, ParameterExpression param)
    {
        // Largely table-driven mechanism, providing constructor lambdas to generic Visit* methods, classified by type and arity.
        switch (expression.NodeType)
        {
            case ExpressionType.And:
            case ExpressionType.AndAlso:
                return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkAnd((BoolExpr)a, (BoolExpr)b));

            case ExpressionType.Or:
            case ExpressionType.OrElse:
                return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkOr((BoolExpr)a, (BoolExpr)b));

            case ExpressionType.ExclusiveOr:
                return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkXor((BoolExpr)a, (BoolExpr)b));

            case ExpressionType.Not:
                return VisitUnary(context, environment, (UnaryExpression)expression, param, (ctx, a) => ctx.MkNot((BoolExpr)a));

            case ExpressionType.Negate:
            case ExpressionType.NegateChecked:
                return VisitUnary(context, environment, (UnaryExpression)expression, param, (ctx, a) => ctx.MkUnaryMinus((ArithExpr)a));

            case ExpressionType.Add:
            case ExpressionType.AddChecked:
                return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkAdd((ArithExpr)a, (ArithExpr)b));

            case ExpressionType.Subtract:
            case ExpressionType.SubtractChecked:
                return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkSub((ArithExpr)a, (ArithExpr)b));

            case ExpressionType.Multiply:
            case ExpressionType.MultiplyChecked:
                return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkMul((ArithExpr)a, (ArithExpr)b));

            case ExpressionType.Divide:
                return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkDiv((ArithExpr)a, (ArithExpr)b));

            case ExpressionType.Modulo:
                return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkRem((IntExpr)a, (IntExpr)b));

            case ExpressionType.LessThan:
                return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkLt((ArithExpr)a, (ArithExpr)b));

            case ExpressionType.LessThanOrEqual:
                return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkLe((ArithExpr)a, (ArithExpr)b));

            case ExpressionType.GreaterThan:
                return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkGt((ArithExpr)a, (ArithExpr)b));

            case ExpressionType.GreaterThanOrEqual:
                return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkGe((ArithExpr)a, (ArithExpr)b));

            case ExpressionType.Equal:
                return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkEq(a, b));

            case ExpressionType.NotEqual:
                return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkNot(ctx.MkEq(a, b)));

            case ExpressionType.MemberAccess:
                return VisitMember(context, environment, (MemberExpression)expression, param);

            case ExpressionType.Constant:
                return VisitConstant(context, (ConstantExpression)expression);

            case ExpressionType.Call:
                return VisitCall(context, environment, (MethodCallExpression)expression, param);

/*               case ExpressionType.Parameter:
                return VisitParameter(context, environment, (ParameterExpression)expression, param);
            */
            case ExpressionType.ArrayIndex:
                return VisitArrayIndex(context, environment, (BinaryExpression)expression, param);
                
            case ExpressionType.Index:
                return VisitIndex(context, environment, (IndexExpression)expression, param, (ctx, a, b) => ctx.MkSelect((ArrayExpr)a, b));

            case ExpressionType.Convert:
                return VisitConvert(context, environment, (UnaryExpression)expression, param);

            case ExpressionType.Power:
                return VisitBinary(context, environment, (BinaryExpression)expression, param, (ctx, a, b) => ctx.MkPower((ArithExpr)a, (ArithExpr)b));

            default:
                throw new NotSupportedException("Unsupported expression node type encountered: " + expression.NodeType);
        }
    }

    private static Expr VisitConvert(Context context, Environment environment, UnaryExpression expression, ParameterExpression param)
    {
        if (expression.Type == expression.Operand.Type)
        {
            return Visit(context, environment, expression.Operand, param);
        }

        var inner = Visit(context, environment, expression.Operand, param);

        switch (Type.GetTypeCode(expression.Operand.Type))
        {
            case TypeCode.Int16:
            case TypeCode.Int32:
                break;
        }

        switch (Type.GetTypeCode(expression.Type))
        {
            case TypeCode.Double:
                return context.MkInt2Real((IntExpr)inner);
            case TypeCode.Int32:
                return context.MkReal2Int((RealExpr)inner);
            case TypeCode.Char:
                if (inner.IsInt)
                {
                    return inner;// context.MkInt(1);// ((IntExpr)inner).int);
                }
                break;
        }

        throw new NotImplementedException($"Cast '{expression.Operand} ({expression.Operand.Type})' to {expression.Type}");
    }

    /// <summary>
    /// Visitor method to translate a binary expression.
    /// </summary>
    /// <param name="context">Z3 context.</param>
    /// <param name="environment">Environment with bindings of theorem variables to Z3 handles.</param>
    /// <param name="expression">Binary expression.</param>
    /// <param name="ctor">Constructor to combine recursive visitor results.</param>
    /// <param name="param">Parameter used to express the constraint on.</param>
    /// <returns>Z3 expression handle.</returns>
    private static Expr VisitBinary(Context context, Environment environment, BinaryExpression expression, ParameterExpression param, Func<Context, Expr, Expr, Expr> ctor)
    {
        return ctor(context, Visit(context, environment, expression.Left, param), Visit(context, environment, expression.Right, param));
    }

    /// <summary>
    /// Resolves an array index expression <c>array[i]</c> (a <see cref="ExpressionType.ArrayIndex"/> binary node).
    /// Two collection-handling modes are supported:
    /// <list type="bullet">
    /// <item><term>Constants</term><description>The array's environment is a <see cref="MultipleEnvironment"/>; the
    /// element sub-environment for index <c>i</c> is created lazily on first access and its Z3 constant returned.
    /// For nested arrays (<c>int[][]</c>) this recurses: <c>Cells[i]</c> yields a row <c>MultipleEnvironment</c>,
    /// and <c>row[j]</c> yields the scalar constant <c>Cells_i_j</c>.</description></item>
    /// <item><term>Array</term><description>The array's environment holds an <c>ArrayExpr</c>; Z3 <c>Select</c> yields the element.</description></item>
    /// </list>
    /// </summary>
    private static Expr VisitArrayIndex(Context context, Environment environment, BinaryExpression expression, ParameterExpression param)
    {
        // Constants mode: try to resolve the array environment WITHOUT going through Visit (which would lose the
        // MultipleEnvironment, since Visit only returns an Expr). For nested arrays the Left operand is itself an
        // ArrayIndex, so we recurse one level down through TryResolveArrayEnvironment.
        if (TryResolveArrayEnvironment(context, environment, expression, param, out var arrayEnv) && arrayEnv is MultipleEnvironment multiEnv
            && TryResolveElementFromMultiEnv(context, multiEnv, expression.Right, out var element))
        {
            return element;
        }

        // Array mode (default), or a symbolic index in Constants mode: array.Left is an ArrayExpr; Select yields the element.
        return context.MkSelect((ArrayExpr)Visit(context, environment, expression.Left, param), Visit(context, environment, expression.Right, param));
    }

    /// <summary>
    /// Materializes (or retrieves) the Constants-mode Z3 constant for a single element of a
    /// <see cref="MultipleEnvironment"/> at a compile-time-constant index. Shared by both the
    /// <c>int[]</c> <see cref="ExpressionType.ArrayIndex"/> path (<see cref="VisitArrayIndex"/>) and the
    /// <c>List&lt;T&gt;</c>/<c>IList&lt;T&gt;</c> indexer (<c>get_Item</c>) path in <see cref="VisitCall"/>.
    /// Returns <c>false</c> for a symbolic (non-constant) index or when the resolved sub-environment is itself
    /// a nested collection with no scalar <see cref="Environment.Expr"/> yet (the caller then falls back to Select).
    /// </summary>
    private static bool TryResolveElementFromMultiEnv(Context context, MultipleEnvironment multiEnv, Expression indexExpression, [NotNullWhen(true)] out Expr? result)
    {
        var indexExpr = PartialEvaluator.PartialEval(indexExpression, ExpressionInterpreter.Instance);
        if (indexExpr.NodeType == ExpressionType.Constant)
        {
            var index = ExpressionInterpreter.Instance.Interpret(indexExpr);

            if (!multiEnv.SubEnvironments.TryGetValue(index!, out var subSubEnv))
            {
                var newPrefix = $"{multiEnv.Prefix}_{index}";
                subSubEnv = ResolveElementEnvironment(context, multiEnv, newPrefix);
                multiEnv.SubEnvironments[index!] = subSubEnv;
            }

            // Scalar element: return its Z3 constant. Nested element (a row of int[][]): its Expr is null and
            // the caller's own index will recurse to resolve the next level.
            if (subSubEnv.Expr != null)
            {
                result = subSubEnv.Expr;
                return true;
            }
        }

        result = null;
        return false;
    }

    /// <summary>
    /// Attempts to resolve the <see cref="Environment"/> bound to an array-typed left operand of an
    /// <c>ArrayIndex</c> node, without going through <see cref="Visit"/> (which only returns an <see cref="Expr"/>
    /// and would therefore lose a <see cref="MultipleEnvironment"/> that has no Expr yet). Handles two shapes:
    /// <list type="bullet">
    /// <item><term><c>Cells[i]</c></term><description>Left is a <see cref="MemberExpression"/> — resolve the bound env.</description></item>
    /// <item><term><c>Cells[i][j]</c></term><description>Left is itself an <c>ArrayIndex</c> — recurse to materialize the
    /// row sub-environment first, then index into it.</description></item>
    /// </list>
    /// </summary>
    /// <returns><c>true</c> if the left operand resolves to a bound environment (Constants-mode candidate).</returns>
    private static bool TryResolveArrayEnvironment(Context context, Environment environment, BinaryExpression expression, ParameterExpression param, [NotNullWhen(true)] out Environment? arrayEnv)
    {
        var left = expression.Left;

        if (left is MemberExpression member)
        {
            arrayEnv = ResolveMemberEnvironment(environment, member);
            return true;
        }

        // Nested array index, e.g. the `Cells[i]` sub-expression of `Cells[i][j]`: recurse to obtain the row
        // sub-environment (a MultipleEnvironment for an int[][] row), materializing it lazily.
        if (left is BinaryExpression { NodeType: ExpressionType.ArrayIndex } nested)
        {
            if (TryResolveArrayEnvironment(context, environment, nested, param, out var parentEnv) && parentEnv is MultipleEnvironment parentMulti)
            {
                var nestedIndexExpr = PartialEvaluator.PartialEval(nested.Right, ExpressionInterpreter.Instance);
                if (nestedIndexExpr.NodeType == ExpressionType.Constant)
                {
                    var nestedIndex = ExpressionInterpreter.Instance.Interpret(nestedIndexExpr);
                    if (!parentMulti.SubEnvironments.TryGetValue(nestedIndex!, out arrayEnv))
                    {
                        var newPrefix = $"{parentMulti.Prefix}_{nestedIndex}";
                        arrayEnv = ResolveElementEnvironment(context, parentMulti, newPrefix);
                        parentMulti.SubEnvironments[nestedIndex!] = arrayEnv;
                    }
                    return true;
                }
            }
        }

        arrayEnv = null;
        return false;
    }

    /// <summary>
    /// Creates the element environment for a Constants-mode collection. For a scalar element type this
    /// is a single Z3 constant (e.g. <c>Cells_0</c>); for a nested array element type (<c>int[][]</c>)
    /// it is a further <see cref="MultipleEnvironment"/> so that <c>Cells[i][j]</c> recurses one level down.
    /// </summary>
    private static Environment ResolveElementEnvironment(Context context, MultipleEnvironment multiEnv, string newPrefix)
    {
        var elementType = multiEnv.ElementType;

        if (elementType.IsArray || (elementType.IsGenericType && typeof(IEnumerable).IsAssignableFrom(elementType.GetGenericTypeDefinition())))
        {
            // Nested array element (e.g. a row of int[][]): a further MultipleEnvironment, so a subsequent
            // ArrayIndex on this sub-env recurses and produces Cells_i_j.
            var nestedElt = elementType.IsArray ? elementType.GetElementType()! : elementType.GetGenericArguments()[0];
            return new MultipleEnvironment(newPrefix, nestedElt);
        }

        // Scalar element: create one Z3 constant of the appropriate sort.
        var env = new Environment();
        env.Expr = Type.GetTypeCode(elementType) switch
        {
            TypeCode.String => context.MkConst(newPrefix, context.StringSort),
            TypeCode.Int16 or TypeCode.Int32 or TypeCode.Int64 or TypeCode.DateTime => context.MkIntConst(newPrefix),
            TypeCode.Boolean => context.MkBoolConst(newPrefix),
            TypeCode.Single or TypeCode.Decimal or TypeCode.Double => context.MkRealConst(newPrefix),
            _ => throw new NotSupportedException($"Unsupported Constants-mode element type {elementType.FullName} for {newPrefix}."),
        };
        return env;
    }

    /// <summary>
    /// Visitor method to translate a method call expression.
    /// </summary>
    /// <param name="context">Z3 context.</param>
    /// <param name="environment">Environment with bindings of theorem variables to Z3 handles.</param>
    /// <param name="call">Method call expression.</param>
    /// <param name="param">Parameter used to express the constraint on.</param>
    /// <returns>Z3 expression handle.</returns>
    private static Expr VisitCall(Context context, Environment environment, MethodCallExpression call, ParameterExpression param)
    {
        var method = call.Method;

        // Does the method have a rewriter attribute applied?
        var rewriterAttr = method.GetCustomAttributes<TheoremPredicateRewriterAttribute>(false).SingleOrDefault();

        if (rewriterAttr != null)
        {
            // Make sure the specified rewriter type implements the ITheoremPredicateRewriter.
            var rewriterType = rewriterAttr.RewriterType;

            if (!typeof(ITheoremPredicateRewriter).IsAssignableFrom(rewriterType))
            {
                throw new InvalidOperationException("Invalid predicate rewriter type definition. Did you implement ITheoremPredicateRewriter?");
            }

            // Assume a parameterless public constructor to new up the rewriter.
            var rewriter = (ITheoremPredicateRewriter)Activator.CreateInstance(rewriterType)!;

            // Make sure we don't get stuck when the rewriter just returned its input. Valid
            // rewriters should satisfy progress guarantees.
            var result = rewriter.Rewrite(call);

            if (result == call)
            {
                throw new InvalidOperationException("The expression tree rewriter of type " + rewriterType.Name + " did not perform any rewrite. Aborting compilation to avoid infinite looping.");
            }

            // Visit the rewritten expression.
            return Visit(context, environment, result, param);
        }

        // Filter for known Z3 operators.
        if (method.IsGenericMethod && method.GetGenericMethodDefinition() == typeof(Z3Methods).GetMethod("Distinct"))
        {
            // We know the signature of the Distinct method call. Its argument is a params
            // array, hence we expect a NewArrayExpression.
            IEnumerable? distinctExps = null;

            var itemsExpression = call.Arguments[0];
            if (itemsExpression is MethodCallExpression mExp)
            {
                if (mExp.Method.IsGenericMethod && mExp.Method.GetGenericMethodDefinition() == typeof(Enumerable)
                    .GetMethods().First(m => m.Name == nameof(Enumerable.ToArray)))
                {
                    var callerToArrayExp = mExp.Arguments[0];
                    if (callerToArrayExp is MethodCallExpression callerToArrayMethodExp)
                    {
                        if (callerToArrayMethodExp.Method.IsGenericMethod && callerToArrayMethodExp.Method.GetGenericMethodDefinition() == typeof(Enumerable).GetMethods().First(m => m.Name == nameof(Enumerable.Select) && m.GetParameters().Length == 2))
                        {
                            var caller = (ICollection)ExpressionInterpreter.Instance.Interpret(callerToArrayMethodExp.Arguments[0]);
                            var arg = callerToArrayMethodExp.Arguments[1] as LambdaExpression;
                            var subExps = new List<Expression>(caller.Count);
                                
                            foreach (var item in caller)
                            {
                                var substitutedExpression = ParameterSubstituter.SubstituteParameter(arg, Expression.Constant(item));
                                var newlyFlattened = PartialEvaluator.PartialEval(substitutedExpression, ExpressionInterpreter.Instance);
                                subExps.Add(newlyFlattened);
                            }

                            distinctExps = subExps;
                        }
                    }
                }
            }
            else
            {
                if (itemsExpression is NewArrayExpression arrExp)
                {
                    distinctExps = arrExp.Expressions;
                }
            }

            if (distinctExps == null)
            {
                throw new NotSupportedException("unsuported method call:" + method.ToString() + "with sub expression " + call.Arguments[0].ToString());
            }

            IEnumerable<Expr> args = from Expression arg in distinctExps 
                                        select Visit(context, environment, arg, param);

            return context.MkDistinct(args.ToArray());
        }

        if (method.Name.StartsWith("get_"))
        {
            // Assuming it's an indexed property
            string prop = method.Name[4..];
            var propinfo = method.DeclaringType?.GetProperty(prop);
            var target = call.Object;

            if (target != null)
            {
                // Constants mode: a List<T>/IList<T> indexer (compiled to a get_Item call) on a theorem
                // collection variable resolves to a per-element Z3 constant, exactly like an int[] ArrayIndex
                // node. The environment of a generic collection member is a MultipleEnvironment in Constants
                // mode (Theorem.GetEnvironment), so reuse the same lazy materialization. Without this, the
                // indexer falls through to MakeIndex -> VisitIndex -> MkSelect, which assumes an ArrayExpr and
                // throws a NullReferenceException in Constants mode. In Array mode the member environment is an
                // ArrayExpr (not a MultipleEnvironment), so the guard fails and we fall through to Select below.
                if (call.Arguments.Count == 1
                    && target is MemberExpression collectionMember
                    && GetMemberHierarchy(collectionMember)[0].Expression == param
                    && ResolveMemberEnvironment(environment, collectionMember) is MultipleEnvironment multiEnv
                    && TryResolveElementFromMultiEnv(context, multiEnv, call.Arguments[0], out var element))
                {
                    return element;
                }

                var args = call.Arguments;
                var indexer = Expression.MakeIndex(target, propinfo, args);

                return Visit(context, environment, indexer, param);
            }
        }

        throw new NotSupportedException("Unknown method call:" + method.ToString());
    }

    /// <summary>
    /// Visitor method to translate a constant expression.
    /// </summary>
    /// <param name="context">Z3 context.</param>
    /// <param name="constant">Constant expression.</param>
    /// <returns>Z3 expression handle.</returns>
    private static Expr VisitConstant(Context context, ConstantExpression constant)
    {
        return VisitConstantValue(context, constant.Value!);
    }

    private static Expr VisitConstantValue(Context context, object val)
    {
        switch (Type.GetTypeCode(val.GetType()))
        {
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
                return context.MkInt(Convert.ToInt64(val));
            case TypeCode.Boolean:
                return context.MkBool((bool)val);
            case TypeCode.Single:
            case TypeCode.Double:
            case TypeCode.Decimal:
                return context.MkReal(val.ToString());
            case TypeCode.DateTime:
                return context.MkInt(((DateTime)val).ToFileTimeUtc());
            case TypeCode.String:
                return context.MkString(val.ToString());
            default:
                throw new NotSupportedException($"Unsupported constant {val}");
        }
    }

    private static Expr VisitIndex(Context context, Environment environment, IndexExpression expression, ParameterExpression param, Func<Context, Expr, Expr[], Expr> ctor)
    {
        var args = expression.Arguments.Select(argExp => Visit(context, environment, argExp, param)).ToArray();
        return ctor(context, Visit(context, environment, expression.Object!, param), args);
    }

    /// <summary>
    /// Visitor method to translate a member expression.
    /// </summary>
    /// <param name="context">the Z3 context to manipulate</param>
    /// <param name="environment">Environment with bindings of theorem variables to Z3 handles.</param>
    /// <param name="member">Member expression.</param>
    /// <param name="param">Parameter used to express the constraint on.</param>
    /// <returns>Z3 expression handle.</returns>
    private static Expr VisitMember(Context context, Environment environment, MemberExpression member, ParameterExpression param)
    {
        var topMember = GetMemberHierarchy(member).First();

        if (topMember.Expression != param)
        {
            if ((topMember.Expression is ConstantExpression expression))
            {
                // We only ever get here if SimplifyLambda is set to false, otherwise partial evaluation does it earlier
                var target = expression.Value;
                var hierarchyIdx = 0;
                object? val = target;

                var hierarchy = GetMemberHierarchy(member);
                while (hierarchyIdx < hierarchy.Count)
                {
                    var currentMember = hierarchy[hierarchyIdx].Member;

                    switch (currentMember.MemberType)
                    {
                        case MemberTypes.Property:
                            var property = (PropertyInfo)currentMember;
                            val = property.GetValue(target);
                            break;
                        case MemberTypes.Field:
                            var field = (FieldInfo)currentMember;
                            val = field.GetValue(target);
                            break;
                        default:
                            throw new NotSupportedException($"Unsupported constant {target} .");
                    }

                    hierarchyIdx++;
                }

                if (val != null)
                {
                    return VisitConstantValue(context, val);
                }

                throw new NotSupportedException($"Could not reduce expression {topMember.Expression}");
            }
            else
            {
                //Debugger.Break();
            }
        }

        return ResolveMemberEnvironment(environment, member).Expr!;
    }

    /// <summary>
    /// Builds the bottom-up hierarchy of member expressions from the leaf (the accessed member) up
    /// to the root, then reverses it to a root-to-leaf traversal.
    /// </summary>
    private static List<MemberExpression> GetMemberHierarchy(MemberExpression member)
    {
        var hierarchy = new List<MemberExpression> { member };
        var mExp = member;

        while (mExp.Expression is MemberExpression parent)
        {
            mExp = parent;
            hierarchy.Add(parent);
        }

        hierarchy.Reverse();
        return hierarchy;
    }

    /// <summary>
    /// Walks the member-expression hierarchy (e.g. <c>sudoku.Cells</c>, or <c>obj.A.B</c>) to find the
    /// <see cref="Environment"/> bound to the deepest member. This is the shared resolution logic used
    /// both by <see cref="VisitMember"/> (to retrieve a Z3 expression) and by the Constants-mode
    /// array-index resolution (to find a <see cref="MultipleEnvironment"/> and materialize a sub-env).
    /// </summary>
    /// <param name="environment">Root environment.</param>
    /// <param name="member">Member expression to resolve.</param>
    /// <returns>The environment bound to the deepest member in the hierarchy.</returns>
    /// <exception cref="NotSupportedException">Thrown when a member in the hierarchy is not bound.</exception>
    private static Environment ResolveMemberEnvironment(Environment environment, MemberExpression member)
    {
        var hierarchy = GetMemberHierarchy(member);

        // Only members we allow currently are direct accesses to the theorem's variables
        // in the environment type. So we just try to find the mapping from the environment
        // bindings table.
        Environment subEnv = environment;

        foreach (var memberExpression in hierarchy)
        {
            // Nullability rules require us to give TryGetValue a nullable holder because it
            // might not succeed. However, C#'s flow analysis is able to determine that if we
            // make it past this if statement, the result definitely wasn't null, so it is
            // happy for us to assign it into the never-null subEnv.
            Environment? nextSubEnv;
            if (!((memberExpression.Member is PropertyInfo property && subEnv.Properties.TryGetValue(property, out nextSubEnv)) ||
                    (memberExpression.Member is FieldInfo field && subEnv.Properties.TryGetValue(field, out nextSubEnv))))
            {
                throw new NotSupportedException("Unknown parameter encountered: " + member.Member.Name + ".");
            }
            subEnv = nextSubEnv;
        }

        return subEnv;
    }

/*      
    private static Expr VisitParameter(Context context, Environment environment, ParameterExpression expression, ParameterExpression param)
    {
        Expr value;

        if (!environment.Properties.TryGetValue(expression., out value))
        {
            throw new NotSupportedException("Unknown parameter encountered: " + expression.Name + ".");
        }

        return value;
    }
*/

    /// <summary>
    /// Visitor method to translate a unary expression.
    /// </summary>
    /// <param name="context">Z3 context.</param>
    /// <param name="environment">Environment with bindings of theorem variables to Z3 handles.</param>
    /// <param name="expression">Unary expression.</param>
    /// <param name="ctor">Constructor to combine recursive visitor results.</param>
    /// <param name="param">Parameter used to express the constraint on.</param>
    /// <returns>Z3 expression handle.</returns>
    private static Expr VisitUnary(Context context, Environment environment, UnaryExpression expression, ParameterExpression param, Func<Context, Expr, Expr> ctor)
    {
        return ctor(context, Visit(context, environment, expression.Operand, param));
    }
}