namespace Z3.Linq;

using System.Collections;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

using MiaPlaza.ExpressionUtils;
using MiaPlaza.ExpressionUtils.Evaluating;

using Microsoft.Z3;

using ExprVisitor = System.Linq.Expressions.ExpressionVisitor;

/// <summary>
/// Translates the LINQ expression tree of one theorem constraint into a Z3 expression.
/// </summary>
/// <remarks>
/// One instance translates one constraint: the Z3 context, the environment its symbols are bound
/// in, and the lambda's parameter are fixed for the whole walk, so they are held as fields and set
/// once by <see cref="Translate"/> rather than threaded through every method. Driven by
/// <c>Theorem</c> during <c>Solve</c> and <c>Optimize</c>, which build the <see cref="Environment"/>
/// it translates against.
/// </remarks>
internal sealed class ExpressionVisitor
{
    /// <summary>The generic definition of <see cref="Z3Methods.Distinct{T}"/>, cached for the call-site match.</summary>
    private static readonly MethodInfo DistinctMethod = typeof(Z3Methods).GetMethod(nameof(Z3Methods.Distinct))!;

    /// <summary>The generic definition of <see cref="Z3Methods.Sum{T}"/>, cached for the call-site match.</summary>
    private static readonly MethodInfo SumMethod = typeof(Z3Methods).GetMethod(nameof(Z3Methods.Sum))!;

    /// <summary>The definition of <see cref="Z3Methods.ExactlyOne"/>, cached for the call-site match.</summary>
    private static readonly MethodInfo ExactlyOneMethod = typeof(Z3Methods).GetMethod(nameof(Z3Methods.ExactlyOne))!;

    /// <summary>The definition of <see cref="Z3Methods.AtMostOne"/>, cached for the call-site match.</summary>
    private static readonly MethodInfo AtMostOneMethod = typeof(Z3Methods).GetMethod(nameof(Z3Methods.AtMostOne))!;

    /// <summary>The definition of <see cref="Z3Methods.AtLeastOne"/>, cached for the call-site match.</summary>
    private static readonly MethodInfo AtLeastOneMethod = typeof(Z3Methods).GetMethod(nameof(Z3Methods.AtLeastOne))!;

    /// <summary>The definition of <see cref="Z3Methods.WeightedAtLeast"/>, cached for the call-site match.</summary>
    private static readonly MethodInfo WeightedAtLeastMethod = typeof(Z3Methods).GetMethod(nameof(Z3Methods.WeightedAtLeast))!;

    /// <summary>The definition of <see cref="Z3Methods.WeightedAtMost"/>, cached for the call-site match.</summary>
    private static readonly MethodInfo WeightedAtMostMethod = typeof(Z3Methods).GetMethod(nameof(Z3Methods.WeightedAtMost))!;

    /// <summary>The definition of <see cref="Z3Methods.WeightedExactly"/>, cached for the call-site match.</summary>
    private static readonly MethodInfo WeightedExactlyMethod = typeof(Z3Methods).GetMethod(nameof(Z3Methods.WeightedExactly))!;

    /// <summary>The generic definition of <see cref="Z3Methods.ForAll{T}"/>, cached for the call-site match.</summary>
    private static readonly MethodInfo ForAllMethod = typeof(Z3Methods).GetMethod(nameof(Z3Methods.ForAll))!;

    /// <summary>The generic definition of <see cref="Z3Methods.Exists{T}"/>, cached for the call-site match.</summary>
    private static readonly MethodInfo ExistsMethod = typeof(Z3Methods).GetMethod(nameof(Z3Methods.Exists))!;

    /// <summary>The generic definition of <see cref="Enumerable.ToArray{TSource}"/>, cached for the call-site match.</summary>
    private static readonly MethodInfo EnumerableToArrayMethod =
        typeof(Enumerable).GetMethods().First(m => m.Name == nameof(Enumerable.ToArray));

    /// <summary>The generic definition of the two-argument <see cref="Enumerable.Select{TSource, TResult}(IEnumerable{TSource}, Func{TSource, TResult})"/>, cached for the call-site match.</summary>
    private static readonly MethodInfo EnumerableSelectMethod =
        typeof(Enumerable).GetMethods().First(m => m.Name == nameof(Enumerable.Select) && m.GetParameters().Length == 2);

    private readonly Context context;
    private readonly Environment environment;
    private readonly ParameterExpression param;

    private ExpressionVisitor(Context context, Environment environment, ParameterExpression param)
    {
        this.context = context;
        this.environment = environment;
        this.param = param;
    }

    /// <summary>
    /// Translates a constraint's expression tree into a Z3 expression handle.
    /// </summary>
    /// <param name="context">Z3 context.</param>
    /// <param name="environment">Environment with bindings of theorem variables to Z3 handles.</param>
    /// <param name="expression">LINQ expression tree to be translated.</param>
    /// <param name="param">The constraint lambda's parameter, i.e. the theorem's environment variable.</param>
    /// <returns>Z3 expression handle.</returns>
    internal static Expr Translate(Context context, Environment environment, Expression expression, ParameterExpression param)
    {
        return new ExpressionVisitor(context, environment, param).Visit(expression);
    }

    /// <summary>
    /// Reports whether an expression mentions one of the <see cref="Z3Methods"/> translation markers.
    /// </summary>
    /// <param name="expression">The expression to scan.</param>
    /// <returns><see langword="true"/> when a marker call appears anywhere in the tree.</returns>
    /// <remarks>
    /// A marker's runtime body throws by design - it exists to be read by this visitor, never run -
    /// so anything that evaluates an expression ahead of translation has to leave such a tree
    /// alone. See <c>Theorem.TranslateConstraint</c>.
    /// </remarks>
    internal static bool MentionsTranslationMarker(Expression expression)
    {
        var finder = new MarkerFinder();
        finder.Visit(expression);

        return finder.Found;
    }

    private Expr Visit(Expression expression)
    {
        // Largely table-driven mechanism, providing constructor lambdas to Visit* methods,
        // classified by node type and arity. The lambdas take the context so they stay
        // non-capturing and are cached by the compiler rather than allocated per node.
        switch (expression.NodeType)
        {
            case ExpressionType.And:
            case ExpressionType.AndAlso:
                return VisitBitwise((BinaryExpression)expression, "&", static (ctx, a, b) => ctx.MkAnd(a, b), static (ctx, a, b) => ctx.MkBVAND(a, b));

            case ExpressionType.Or:
            case ExpressionType.OrElse:
                return VisitBitwise((BinaryExpression)expression, "|", static (ctx, a, b) => ctx.MkOr(a, b), static (ctx, a, b) => ctx.MkBVOR(a, b));

            case ExpressionType.ExclusiveOr:
                return VisitBitwise((BinaryExpression)expression, "^", static (ctx, a, b) => ctx.MkXor(a, b), static (ctx, a, b) => ctx.MkBVXOR(a, b));

            // C# spells both boolean '!' and bitwise '~' with the Not node (OnesComplement is the
            // alternative spelling some providers emit for '~'); the operand's sort decides.
            case ExpressionType.Not:
            case ExpressionType.OnesComplement:
                return VisitUnary((UnaryExpression)expression, static (ctx, a) => a switch
                {
                    BoolExpr boolExpr => ctx.MkNot(boolExpr),
                    BitVecExpr bvExpr => ctx.MkBVNot(bvExpr),
                    _ => throw new NotSupportedException("The '~' operator is supported only on bit-vector (uint/ulong) symbols; '!' only on Boolean operands."),
                });

            case ExpressionType.Negate:
            case ExpressionType.NegateChecked:
                return VisitUnary((UnaryExpression)expression, static (ctx, a) => ctx.MkUnaryMinus((ArithExpr)a));

            case ExpressionType.Add:
            case ExpressionType.AddChecked:
                return VisitArithmetic((BinaryExpression)expression, static (ctx, a, b) => ctx.MkAdd(a, b), static (ctx, a, b) => ctx.MkBVAdd(a, b));

            case ExpressionType.Subtract:
            case ExpressionType.SubtractChecked:
                return VisitArithmetic((BinaryExpression)expression, static (ctx, a, b) => ctx.MkSub(a, b), static (ctx, a, b) => ctx.MkBVSub(a, b));

            case ExpressionType.Multiply:
            case ExpressionType.MultiplyChecked:
                return VisitArithmetic((BinaryExpression)expression, static (ctx, a, b) => ctx.MkMul(a, b), static (ctx, a, b) => ctx.MkBVMul(a, b));

            case ExpressionType.Divide:
                return VisitArithmetic((BinaryExpression)expression, static (ctx, a, b) => ctx.MkDiv(a, b), static (ctx, a, b) => ctx.MkBVUDiv(a, b));

            case ExpressionType.Modulo:
                return VisitBinary((BinaryExpression)expression, static (ctx, a, b) => (a, b) switch
                {
                    (BitVecExpr ba, BitVecExpr bb) => ctx.MkBVURem(ba, bb),
                    (BitVecExpr ba, _) => ctx.MkBVURem(ba, ToBitVector(ctx, b, ba.SortSize)),
                    (_, BitVecExpr bb) => ctx.MkBVURem(ToBitVector(ctx, a, bb.SortSize), bb),
                    (IntExpr ia, IntExpr ib) => ctx.MkRem(ia, ib),
                    _ => throw new NotSupportedException("The modulo operator is supported only on integer or bit-vector operands; Z3 has no remainder on real-sorted values."),
                });

            case ExpressionType.LeftShift:
                return VisitShift((BinaryExpression)expression, "<<", static (ctx, a, b) => ctx.MkBVSHL(a, b));

            case ExpressionType.RightShift:
                return VisitShift((BinaryExpression)expression, ">>", static (ctx, a, b) => ctx.MkBVLSHR(a, b));

            case ExpressionType.LessThan:
                return VisitComparison((BinaryExpression)expression, static (ctx, a, b) => ctx.MkLt(a, b), static (ctx, a, b) => ctx.MkBVULT(a, b));

            case ExpressionType.LessThanOrEqual:
                return VisitComparison((BinaryExpression)expression, static (ctx, a, b) => ctx.MkLe(a, b), static (ctx, a, b) => ctx.MkBVULE(a, b));

            case ExpressionType.GreaterThan:
                return VisitComparison((BinaryExpression)expression, static (ctx, a, b) => ctx.MkGt(a, b), static (ctx, a, b) => ctx.MkBVUGT(a, b));

            case ExpressionType.GreaterThanOrEqual:
                return VisitComparison((BinaryExpression)expression, static (ctx, a, b) => ctx.MkGe(a, b), static (ctx, a, b) => ctx.MkBVUGE(a, b));

            case ExpressionType.Equal:
                return VisitEquality((BinaryExpression)expression, negate: false);

            case ExpressionType.NotEqual:
                return VisitEquality((BinaryExpression)expression, negate: true);

            case ExpressionType.MemberAccess:
                return VisitMember((MemberExpression)expression);

            case ExpressionType.Constant:
                return VisitConstantValue(((ConstantExpression)expression).Value!);

            case ExpressionType.Call:
                return VisitCall((MethodCallExpression)expression);

            case ExpressionType.Conditional:
                return VisitConditional((ConditionalExpression)expression);

            case ExpressionType.ArrayIndex:
                return this.VisitArrayIndex((BinaryExpression)expression);

            case ExpressionType.Index:
                return VisitIndex((IndexExpression)expression, static (ctx, a, b) => ctx.MkSelect((ArrayExpr)a, b));

            case ExpressionType.Convert:
                return VisitConvert((UnaryExpression)expression);

            case ExpressionType.Power:
                return VisitBinary((BinaryExpression)expression, static (ctx, a, b) => ctx.MkPower((ArithExpr)a, (ArithExpr)b));

            default:
                throw new NotSupportedException("Unsupported expression node type encountered: " + expression.NodeType);
        }
    }

    private Expr VisitConvert(UnaryExpression expression)
    {
        if (expression.Type == expression.Operand.Type)
        {
            return Visit(expression.Operand);
        }

        Expr inner = Visit(expression.Operand);

        // A numeric conversion the compiler inserted, or the caller wrote, means one of three
        // things in Z3, and which one depends on the sorts involved rather than on the CLR
        // types: nothing at all when both types map to the same sort (short to int, int to
        // long, float to double, a cast that only narrows); integer-to-real when the operand is
        // an integer and the target a real (int to double, int to decimal); real-to-integer the
        // other way round. The target sort comes from the same mapping the symbols are declared
        // with, so a conversion can never disagree with a symbol about what a type is.
        //
        // This used to be a switch on the target type alone, which assumed the operand sort from
        // it - int-to-real for every conversion to double, real-to-int for every conversion to
        // int - and had no arm at all for long, float or decimal. See #63 and #76.
        Sort? targetSort = expression.Type == typeof(char)
            ? this.context.IntSort
            : Theorem.TryGetSymbolSort(this.context, Type.GetTypeCode(expression.Type));

        if (targetSort is not null)
        {
            if (inner.Sort.Equals(targetSort))
            {
                return inner;
            }

            if (inner.IsInt && targetSort is RealSort)
            {
                return this.context.MkInt2Real((IntExpr)inner);
            }

            if (inner.IsReal && targetSort is IntSort)
            {
                return this.context.MkReal2Int((RealExpr)inner);
            }
        }

        throw new NotImplementedException($"Cast '{expression.Operand} ({expression.Operand.Type})' to {expression.Type}");
    }

    /// <summary>
    /// Translates a binary expression, combining the translated operands with <paramref name="ctor"/>.
    /// </summary>
    /// <param name="expression">Binary expression.</param>
    /// <param name="ctor">Combines the context and the two recursively translated operands.</param>
    /// <returns>Z3 expression handle.</returns>
    private Expr VisitBinary(BinaryExpression expression, Func<Context, Expr, Expr, Expr> ctor)
    {
        return ctor(this.context, Visit(expression.Left), Visit(expression.Right));
    }

    /// <summary>
    /// Translates <c>==</c> or <c>!=</c>, which Z3 expresses as equality and its negation.
    /// </summary>
    /// <param name="expression">The equality node.</param>
    /// <param name="negate">Whether to negate the equality, for <c>!=</c>.</param>
    /// <returns>Z3 expression handle.</returns>
    /// <remarks>
    /// The operands need not already share a sort: a declared-width bit-vector compared with an
    /// integer literal - <c>r.A == 15</c> - mixes the two, and the literal is read as a
    /// width-matched bit pattern. Z3's equality is sort-checked, so without this the comparison
    /// would be rejected rather than answered.
    /// </remarks>
    private Expr VisitEquality(BinaryExpression expression, bool negate)
    {
        Expr left = Visit(expression.Left);
        Expr right = Visit(expression.Right);

        if (left is BitVecExpr || right is BitVecExpr)
        {
            uint width = (left as BitVecExpr)?.SortSize ?? ((BitVecExpr)right).SortSize;
            left = ToBitVector(this.context, left, width);
            right = ToBitVector(this.context, right, width);
        }

        BoolExpr equality = this.context.MkEq(left, right);
        return negate ? this.context.MkNot(equality) : equality;
    }

    /// <summary>
    /// Translates <c>array[i]</c>, in whichever of the two <see cref="CollectionHandling"/> modes
    /// the array was built for.
    /// </summary>
    /// <param name="expression">The <see cref="ExpressionType.ArrayIndex"/> node.</param>
    /// <returns>Z3 expression handle for the selected element.</returns>
    /// <remarks>
    /// In <see cref="CollectionHandling.Array"/> mode the left operand translates to an array
    /// constant and Z3's Select picks the element. In <see cref="CollectionHandling.Constants"/>
    /// mode there is no array constant to select from: the operand's environment is a
    /// <see cref="MultipleEnvironment"/> holding one constant per element, created on first
    /// access, and the element's constant is returned directly. That is why this path cannot go
    /// through <see cref="Visit"/> for the left operand - Visit returns a single handle and a
    /// <see cref="MultipleEnvironment"/> has none.
    /// </remarks>
    private Expr VisitArrayIndex(BinaryExpression expression)
    {
        if (this.TryResolveMultipleEnvironment(expression.Left, out MultipleEnvironment? multiEnv))
        {
            Environment? elementEnv = this.ResolveElementEnvironment(multiEnv!, expression.Right)
                ?? throw new NotSupportedException(
                    $"{CollectionHandling.Constants} mode needs a compile-time constant index: '{expression}' has a symbolic one. "
                    + $"Build the theorem with {nameof(CollectionHandling)}.{nameof(CollectionHandling.Array)} to index with a symbol.");

            return elementEnv.Expr!;
        }

        if (this.TryFoldClosedRead(expression, out Expr? folded))
        {
            return folded!;
        }

        return this.context.MkSelect((ArrayExpr)Visit(expression.Left), Visit(expression.Right));
    }

    /// <summary>
    /// Resolves the left operand of an array index to the <see cref="MultipleEnvironment"/> of a
    /// <see cref="CollectionHandling.Constants"/> collection, if that is what it is.
    /// </summary>
    /// <param name="left">Left operand of an <see cref="ExpressionType.ArrayIndex"/> node.</param>
    /// <param name="multiEnv">The collection's per-element environment, when one was found.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> denotes a constants-mode collection.</returns>
    /// <remarks>
    /// Two shapes reach here. <c>Cells[i]</c> has a member access on the left, which the bindings
    /// table resolves directly. <c>Cells[i][j]</c> has a nested index, whose own element has to be
    /// materialised first - a row of <c>int[][]</c> is itself a <see cref="MultipleEnvironment"/> -
    /// and then looked into.
    /// </remarks>
    private bool TryResolveMultipleEnvironment(Expression left, out MultipleEnvironment? multiEnv)
    {
        multiEnv = null;

        if (left is MemberExpression member)
        {
            multiEnv = this.ResolveBoundEnvironment(member) as MultipleEnvironment;
            return multiEnv is not null;
        }

        if (left is BinaryExpression { NodeType: ExpressionType.ArrayIndex } nested
            && this.TryResolveMultipleEnvironment(nested.Left, out MultipleEnvironment? parent))
        {
            multiEnv = this.ResolveElementEnvironment(parent!, nested.Right) as MultipleEnvironment;
            return multiEnv is not null;
        }

        return false;
    }

    /// <summary>
    /// Returns the environment bound to a member access on the theorem's parameter, or
    /// <see langword="null"/> when the access is not one the bindings table can resolve.
    /// </summary>
    /// <param name="member">The member access.</param>
    /// <returns>The bound environment, or <see langword="null"/>.</returns>
    /// <remarks>
    /// Unlike <see cref="VisitMember"/> this never throws and never evaluates a captured constant:
    /// it answers one question - is this member access bound in the environment - and is used
    /// where a <see langword="null"/> simply means "not that shape".
    /// </remarks>
    private Environment? ResolveBoundEnvironment(MemberExpression member)
    {
        List<MemberExpression> hierarchy = [member];

        var mExp = member;
        while (mExp.Expression is MemberExpression parent)
        {
            mExp = parent;
            hierarchy.Add(parent);
        }

        if (hierarchy[^1].Expression != this.param)
        {
            return null;
        }

        Environment subEnv = this.environment;

        for (int i = hierarchy.Count - 1; i >= 0; i--)
        {
            MemberInfo key = hierarchy[i].Member;

            if (!subEnv.Properties.TryGetValue(key, out Environment? next))
            {
                return null;
            }

            subEnv = next;
        }

        return subEnv;
    }

    /// <summary>
    /// Gets the per-element environment of a <see cref="MultipleEnvironment"/> at a constant
    /// index, creating it on first access.
    /// </summary>
    /// <param name="multiEnv">The collection's per-element environment.</param>
    /// <param name="indexExpression">The index expression.</param>
    /// <returns>The element's environment, or <see langword="null"/> when the index is not a constant.</returns>
    private Environment? ResolveElementEnvironment(MultipleEnvironment multiEnv, Expression indexExpression)
    {
        // The index of a nested loop is a captured local, so it reaches us as a member access on
        // the closure rather than as a constant. Partial evaluation is what turns it into one.
        // The evaluator only reduces a lambda, so the index is wrapped in a parameterless one and
        // the body is taken back out - the same shape the rewriter uses for a captured constant.
        Expression indexExpr = PartialEvaluator.PartialEval(Expression.Lambda(indexExpression), ExpressionInterpreter.Instance).Body;

        if (indexExpr is not ConstantExpression constant)
        {
            return null;
        }

        object? index = constant.Value;

        if (index is null)
        {
            return null;
        }

        if (!multiEnv.SubEnvironments.TryGetValue(index, out Environment? elementEnv))
        {
            elementEnv = this.CreateElementEnvironment(multiEnv.ElementType, $"{multiEnv.Prefix}_{index}");
            multiEnv.SubEnvironments[index] = elementEnv;
        }

        return elementEnv;
    }

    /// <summary>
    /// Creates the environment of one element of a constants-mode collection.
    /// </summary>
    /// <param name="elementType">The collection's element type.</param>
    /// <param name="prefix">Symbol name for the element.</param>
    /// <returns>The element's environment.</returns>
    /// <remarks>
    /// A scalar element gets one constant of the sort its type maps to. A collection element -
    /// a row of <c>int[][]</c> - gets a further <see cref="MultipleEnvironment"/> instead, so that
    /// the next index in <c>Cells[i][j]</c> recurses one level down.
    /// </remarks>
    private Environment CreateElementEnvironment(Type elementType, string prefix)
    {
        if (elementType.IsArray || (elementType.IsGenericType && typeof(IEnumerable).IsAssignableFrom(elementType.GetGenericTypeDefinition())))
        {
            Type nested = elementType.IsArray ? elementType.GetElementType()! : elementType.GetGenericArguments()[0];
            return new MultipleEnvironment(prefix, nested);
        }

        Sort? sort = Theorem.TryGetSymbolSort(this.context, Type.GetTypeCode(elementType))
            ?? throw new NotSupportedException($"Unsupported element type {elementType.FullName}");

        return new Environment { Expr = this.context.MkConst(prefix, sort) };
    }

    /// <summary>
    /// Translates an arithmetic operator, choosing the integer/real form or the bit-vector form
    /// from the operands' sort.
    /// </summary>
    /// <param name="expression">Binary expression.</param>
    /// <param name="arith">Builds the term for integer- or real-sorted operands.</param>
    /// <param name="bv">Builds the term for bit-vector operands.</param>
    /// <returns>Z3 expression handle.</returns>
    /// <remarks>
    /// A bit-vector symbol (an unsigned CLR type, or a member declared with
    /// <see cref="BitVecWidthAttribute"/>) carries wrapping arithmetic; an <c>int</c>, <c>long</c>
    /// or real symbol carries mathematical arithmetic. Both operands usually share a sort, since C#
    /// would not compile a mixed expression without a conversion, which <see cref="VisitConvert"/>
    /// handles first - but a constant does not: <c>r.A + 1</c> on a 4-bit register mixes a
    /// bit-vector with an integer literal, and the literal is read as a width-matched bit pattern.
    /// See <see cref="ToBitVector"/>.
    /// </remarks>
    private Expr VisitArithmetic(BinaryExpression expression, Func<Context, ArithExpr, ArithExpr, ArithExpr> arith, Func<Context, BitVecExpr, BitVecExpr, BitVecExpr> bv)
    {
        Expr left = Visit(expression.Left);
        Expr right = Visit(expression.Right);

        return (left, right) switch
        {
            (BitVecExpr bvLeft, BitVecExpr bvRight) => bv(this.context, bvLeft, bvRight),
            (BitVecExpr bvLeft, _) => bv(this.context, bvLeft, ToBitVector(this.context, right, bvLeft.SortSize)),
            (_, BitVecExpr bvRight) => bv(this.context, ToBitVector(this.context, left, bvRight.SortSize), bvRight),
            _ => arith(this.context, (ArithExpr)left, (ArithExpr)right),
        };
    }

    /// <summary>
    /// Translates a relational operator, choosing the ordered-arithmetic form or the
    /// <em>unsigned</em> bit-vector form from the operands' sort.
    /// </summary>
    /// <param name="expression">Binary expression.</param>
    /// <param name="arith">Builds the comparison for integer- or real-sorted operands.</param>
    /// <param name="bv">Builds the unsigned comparison for bit-vector operands.</param>
    /// <returns>Z3 expression handle.</returns>
    /// <remarks>
    /// Bit-vectors map from the unsigned CLR types and from members declared with
    /// <see cref="BitVecWidthAttribute"/>, so the comparison is unsigned - <c>uint</c> order, not
    /// two's-complement signed order. A literal on one side is read as a width-matched bit pattern,
    /// as in <see cref="VisitArithmetic"/>.
    /// </remarks>
    private Expr VisitComparison(BinaryExpression expression, Func<Context, ArithExpr, ArithExpr, BoolExpr> arith, Func<Context, BitVecExpr, BitVecExpr, BoolExpr> bv)
    {
        Expr left = Visit(expression.Left);
        Expr right = Visit(expression.Right);

        return (left, right) switch
        {
            (BitVecExpr bvLeft, BitVecExpr bvRight) => bv(this.context, bvLeft, bvRight),
            (BitVecExpr bvLeft, _) => bv(this.context, bvLeft, ToBitVector(this.context, right, bvLeft.SortSize)),
            (_, BitVecExpr bvRight) => bv(this.context, ToBitVector(this.context, left, bvRight.SortSize), bvRight),
            _ => arith(this.context, (ArithExpr)left, (ArithExpr)right),
        };
    }

    /// <summary>
    /// Reads an operand as a bit-vector of the given width, so that it can be combined with one.
    /// </summary>
    /// <param name="context">The context the term is built in.</param>
    /// <param name="expr">The operand, already translated.</param>
    /// <param name="width">The width, in bits, of the bit-vector it is combined with.</param>
    /// <returns>The operand as a bit-vector of <paramref name="width"/> bits.</returns>
    /// <remarks>
    /// A constant is the only mixed operand the DSL can reach: two symbols of different sorts need a
    /// conversion to be written in C# at all, and <see cref="VisitConvert"/> has already applied it.
    /// An integer literal becomes a bit-vector of the same width, which is what makes
    /// <c>r.A + r.B &lt; r.A</c> and <c>r.B &gt;= 1</c> expressible against a declared-width member.
    /// A value the width cannot hold wraps, exactly as the register arithmetic it joins does.
    /// </remarks>
    private static BitVecExpr ToBitVector(Context context, Expr expr, uint width)
    {
        return expr switch
        {
            BitVecExpr bitVector => bitVector,
            IntNum literal => context.MkBV(literal.Int64, width),
            IntExpr integer => context.MkInt2BV(width, integer),
            _ => throw new NotSupportedException(
                $"Cannot combine a {width}-bit bit-vector with an operand of sort {expr.Sort}: only integers convert to a bit-vector."),
        };
    }

    /// <summary>
    /// Translates <c>&amp;</c>, <c>|</c> and <c>^</c>, which C# uses for Boolean logic, integer
    /// bitwise arithmetic, and bit-vector bitwise arithmetic alike.
    /// </summary>
    /// <param name="expression">Binary expression.</param>
    /// <param name="op">The C# operator, for the diagnostic when the operands fit neither form.</param>
    /// <param name="boolOp">Builds the logical term for Boolean operands.</param>
    /// <param name="bvOp">Builds the bitwise term for bit-vector operands.</param>
    /// <returns>Z3 expression handle.</returns>
    /// <remarks>
    /// The operator is chosen from the operands' Z3 sort, not the expression node, which is the
    /// same for <c>bool &amp; bool</c>, <c>int &amp; int</c> and <c>uint &amp; uint</c>. Boolean
    /// operands give the logical operator and bit-vector operands the bitwise one. A plain
    /// integer symbol has neither - Z3's integer sort has no bitwise operations - so it is
    /// rejected with a message pointing at the unsigned types, rather than an
    /// <see cref="InvalidCastException"/> from inside the cast.
    /// </remarks>
    private Expr VisitBitwise(BinaryExpression expression, string op, Func<Context, BoolExpr, BoolExpr, BoolExpr> boolOp, Func<Context, BitVecExpr, BitVecExpr, BitVecExpr> bvOp)
    {
        Expr left = Visit(expression.Left);
        Expr right = Visit(expression.Right);

        return (left, right) switch
        {
            (BoolExpr boolLeft, BoolExpr boolRight) => boolOp(this.context, boolLeft, boolRight),
            (BitVecExpr bvLeft, BitVecExpr bvRight) => bvOp(this.context, bvLeft, bvRight),
            (BitVecExpr bvLeft, _) => bvOp(this.context, bvLeft, ToBitVector(this.context, right, bvLeft.SortSize)),
            (_, BitVecExpr bvRight) => bvOp(this.context, ToBitVector(this.context, left, bvRight.SortSize), bvRight),
            _ => throw new NotSupportedException(
                $"The '{op}' operator is supported on Boolean operands and on bit-vector (uint/ulong) symbols. It is not supported on plain integer symbols, whose Z3 sort has no bitwise operations."),
        };
    }

    /// <summary>
    /// Translates a shift, <c>&lt;&lt;</c> or <c>&gt;&gt;</c>, on a bit-vector.
    /// </summary>
    /// <param name="expression">Binary expression.</param>
    /// <param name="op">The C# operator, for the diagnostic.</param>
    /// <param name="bvOp">Builds the shift term from the value and the shift amount.</param>
    /// <returns>Z3 expression handle.</returns>
    /// <remarks>
    /// C# types the shift amount as <c>int</c>, so the right operand translates to an integer and
    /// is converted to a bit-vector of the value's width before the shift. Only a bit-vector value
    /// can be shifted; Z3's integer sort has no shift.
    /// </remarks>
    private Expr VisitShift(BinaryExpression expression, string op, Func<Context, BitVecExpr, BitVecExpr, BitVecExpr> bvOp)
    {
        Expr value = Visit(expression.Left);
        Expr amount = Visit(expression.Right);

        if (value is not BitVecExpr bvValue)
        {
            throw new NotSupportedException($"The '{op}' shift operator is supported only on bit-vector (uint/ulong) symbols.");
        }

        BitVecExpr bvAmount = amount as BitVecExpr ?? this.context.MkInt2BV(bvValue.SortSize, (IntExpr)amount);

        return bvOp(this.context, bvValue, bvAmount);
    }

    /// <summary>
    /// Translates a conditional (ternary <c>?:</c>) expression.
    /// </summary>
    /// <param name="expression">Conditional expression.</param>
    /// <returns>Z3 expression handle.</returns>
    /// <remarks>
    /// Maps onto Z3's if-then-else term. The two branches must share a sort, which they do for
    /// any ternary the C# compiler accepts, since both arms are converted to a common type.
    /// </remarks>
    private Expr VisitConditional(ConditionalExpression expression)
    {
        Expr test = Visit(expression.Test);
        Expr ifTrue = Visit(expression.IfTrue);
        Expr ifFalse = Visit(expression.IfFalse);

        return this.context.MkITE((BoolExpr)test, ifTrue, ifFalse);
    }

    /// <summary>
    /// Translates a method call expression.
    /// </summary>
    /// <param name="call">Method call expression.</param>
    /// <returns>Z3 expression handle.</returns>
    private Expr VisitCall(MethodCallExpression call)
    {
        var method = call.Method;

        // Does the method have a rewriter attribute applied? IsDefined is checked first so a call
        // to an ordinary method - the common case, hit once per call node in every constraint -
        // does not allocate an attribute array; the attribute is read only when one is present.
        if (method.IsDefined(typeof(TheoremPredicateRewriterAttribute), false))
        {
            var rewriterAttr = method.GetCustomAttributes<TheoremPredicateRewriterAttribute>(false).Single();

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
            return Visit(result);
        }

        // The variadic "magic methods" - Distinct, Sum, and the pseudo-Boolean trio - all take a
        // params array, so they share the argument extraction and differ only in the term built
        // from the materialised arguments.
        bool isDistinct = method.IsGenericMethod && method.GetGenericMethodDefinition() == DistinctMethod;
        bool isSum = method.IsGenericMethod && method.GetGenericMethodDefinition() == SumMethod;

        if (isDistinct || isSum)
        {
            IEnumerable<Expression> distinctExps = MaterializeVariadicArguments(call.Arguments[0], method);

            IEnumerable<Expr> args = from Expression arg in distinctExps
                                     select Visit(arg);

            if (isDistinct)
            {
                return this.context.MkDistinct(args.ToArray());
            }

            // Sum -> one addition over the materialised terms. MkAdd is variadic, so the sum of any
            // number of terms is a single node rather than a fold of pairwise additions.
            return this.context.MkAdd(args.Cast<ArithExpr>().ToArray());
        }

        // The pseudo-Boolean trio. Each takes a params bool[] because PB indicators have to be
        // Boolean, and each maps onto native PB: sum(indicators) >= 1 for at-least-one,
        // sum(not indicators) >= n - 1 for at-most-one, and both for exactly-one. Native PB keeps
        // Z3's own propagation, where an Ite-and-add expansion does not.
        if (method == ExactlyOneMethod || method == AtMostOneMethod || method == AtLeastOneMethod)
        {
            // An empty params array is legal at the call site; MaterializeVariadicArguments reads it
            // off the NewArrayExpression like any other literal.
            List<BoolExpr> indicators = [];

            foreach (Expression argument in MaterializeVariadicArguments(call.Arguments[0], method))
            {
                indicators.Add(Visit(argument) as BoolExpr ?? throw new InvalidOperationException(
                    $"Z3Methods.{method.Name} indicators must be Boolean, got {argument.NodeType}."));
            }

            int count = indicators.Count;

            // The degenerate cases are decided, not translated: nothing can make one of zero
            // indicators true, and at most one of zero is vacuously true.
            if (count == 0)
            {
                return method == AtMostOneMethod ? this.context.MkTrue() : this.context.MkFalse();
            }

            int[] weights = new int[count];
            Array.Fill(weights, 1);

            if (method == AtLeastOneMethod)
            {
                return this.context.MkPBGe(weights, indicators.ToArray(), 1);
            }

            BoolExpr[] negated = new BoolExpr[count];
            for (int i = 0; i < count; i++)
            {
                negated[i] = this.context.MkNot(indicators[i]);
            }

            if (method == AtMostOneMethod)
            {
                return this.context.MkPBGe(weights, negated, count - 1);
            }

            return this.context.MkAnd(
                this.context.MkPBGe(weights, indicators.ToArray(), 1),
                this.context.MkPBGe(weights, negated, count - 1));
        }

        // The weighted band forms. Same indicators, unequal weights and an explicit bound, all of
        // which are host values here: the weights describe the group, they are not themselves
        // unknowns, so they are read off the tree rather than translated into terms.
        if (method == WeightedAtLeastMethod || method == WeightedAtMostMethod || method == WeightedExactlyMethod)
        {
            int bound = Convert.ToInt32(EvaluateToHostValue(call.Arguments[0]));

            int[] weights = EvaluateToHostValue(call.Arguments[1]) as int[] ?? throw new InvalidOperationException(
                $"Z3Methods.{method.Name} weights must be an int[] known at translation time, got {call.Arguments[1].NodeType}.");

            List<BoolExpr> indicators = [];

            foreach (Expression argument in MaterializeVariadicArguments(call.Arguments[2], method))
            {
                indicators.Add(Visit(argument) as BoolExpr ?? throw new InvalidOperationException(
                    $"Z3Methods.{method.Name} indicators must be Boolean, got {argument.NodeType}."));
            }

            if (weights.Length != indicators.Count)
            {
                throw new InvalidOperationException(
                    $"Z3Methods.{method.Name} has {weights.Length} weights for {indicators.Count} indicators.");
            }

            // The weighted sum of an empty group is 0, so the bound alone decides.
            if (indicators.Count == 0)
            {
                bool holds = method == WeightedAtLeastMethod ? bound <= 0
                    : method == WeightedAtMostMethod ? bound >= 0
                    : bound == 0;

                return holds ? this.context.MkTrue() : this.context.MkFalse();
            }

            if (method == WeightedAtLeastMethod)
            {
                return this.context.MkPBGe(weights, indicators.ToArray(), bound);
            }

            if (method == WeightedAtMostMethod)
            {
                return this.context.MkPBLe(weights, indicators.ToArray(), bound);
            }

            return this.context.MkPBEq(weights, indicators.ToArray(), bound);
        }

        // The bounded quantifiers. The domain is a host value and the predicate is unrolled over
        // it, so what looks like a quantifier in the query is a finite conjunction or disjunction
        // in the term - a different constraint from an SMT quantifier over an unbounded sort.
        if (method.IsGenericMethod && method.GetGenericMethodDefinition() == ForAllMethod)
        {
            return this.VisitBoundedQuantifier(call, isUniversal: true);
        }

        if (method.IsGenericMethod && method.GetGenericMethodDefinition() == ExistsMethod)
        {
            return this.VisitBoundedQuantifier(call, isUniversal: false);
        }

        if (method.Name.StartsWith("get_"))
        {
            // Assuming it's an indexed property
            string prop = method.Name[4..];
            var propinfo = method.DeclaringType?.GetProperty(prop);
            var target = call.Object;

            if (target != null)
            {
                // A List<T>/IList<T> indexer reaches us as a get_Item call, not as an ArrayIndex
                // node like an int[] one, so the Constants-mode per-element path has to be reached
                // from here as well: the indexer otherwise falls through to MkSelect, which assumes
                // an ArrayExpr that Constants mode never builds. In Array mode the member's
                // environment is an ArrayExpr and not a MultipleEnvironment, so this guard fails
                // and the Select path below runs as before. A nested collection element has no
                // Expr of its own yet, so it falls through too and the Select picks the next level.
                if (call.Arguments.Count == 1
                    && target is MemberExpression collectionMember
                    && this.ResolveBoundEnvironment(collectionMember) is MultipleEnvironment multiEnv
                    && this.ResolveElementEnvironment(multiEnv, call.Arguments[0]) is { Expr: not null } elementEnv)
                {
                    return elementEnv.Expr!;
                }

                var args = call.Arguments;
                var indexer = Expression.MakeIndex(target, propinfo, args);

                return Visit(indexer);
            }
        }

        // A call that mentions no theorem parameter is not a term at all: it produces the value it
        // produces, and only its value belongs in the translation. Rational.Of(1, 3) is the case
        // that matters - a static factory is the only way to write an exact fraction in a lambda, so
        // without this every exact-rational constraint would be refused as an unknown method call.
        // Tested after the magic methods and the indexers, which are calls the DSL does translate
        // and which this would otherwise evaluate away on the host.
        if (!ParameterFinder.Contains(call))
        {
            return VisitConstantValue(EvaluateToHostValue(call)!);
        }

        throw new NotSupportedException("Unknown method call:" + method.ToString());
    }

    /// <summary>
    /// Finds whether an expression mentions any parameter.
    /// </summary>
    /// <remarks>
    /// Used to tell a host value from a term: an expression with no parameter in it can be
    /// evaluated, one that mentions a parameter has to be translated.
    /// </remarks>
    private sealed class ParameterFinder : ExprVisitor
    {
        private bool found;

        /// <summary>
        /// Determines whether the given expression mentions a parameter.
        /// </summary>
        /// <param name="expression">The expression to search.</param>
        /// <returns>Whether a parameter occurs in it.</returns>
        public static bool Contains(Expression expression)
        {
            var finder = new ParameterFinder();
            finder.Visit(expression);

            return finder.found;
        }

        /// <inheritdoc/>
        protected override Expression VisitParameter(ParameterExpression node)
        {
            this.found = true;

            return node;
        }
    }

    /// <summary>
    /// Translates a bounded quantifier by unrolling it over its domain.
    /// </summary>
    /// <param name="call">The call to <see cref="Z3Methods.ForAll{T}"/> or <see cref="Z3Methods.Exists{T}"/>.</param>
    /// <param name="isUniversal">Whether the quantifier is universal.</param>
    /// <returns>The unrolled term.</returns>
    /// <remarks>
    /// The domain is read as a host value and the predicate is applied to each element in turn, so
    /// the result is a finite conjunction (universal) or disjunction (existential). The empty domain
    /// falls out of that reading rather than being special-cased away: an empty conjunction holds,
    /// an empty disjunction does not.
    /// </remarks>
    /// <exception cref="NotSupportedException">The domain is not enumerable, or the predicate is not a lambda.</exception>
    private Expr VisitBoundedQuantifier(MethodCallExpression call, bool isUniversal)
    {
        string name = isUniversal ? nameof(Z3Methods.ForAll) : nameof(Z3Methods.Exists);

        if (EvaluateToHostValue(call.Arguments[0]) is not IEnumerable domain)
        {
            throw new NotSupportedException($"The domain of {name} must be a finite, host-evaluable sequence.");
        }

        // A Func argument arrives quoted by the compiler; the lambda inside is what gets applied.
        Expression predicateArgument = call.Arguments[1] is UnaryExpression { NodeType: ExpressionType.Quote } quoted
            ? quoted.Operand
            : call.Arguments[1];

        if (predicateArgument is not LambdaExpression predicate)
        {
            throw new NotSupportedException($"The predicate of {name} must be a lambda expression.");
        }

        var terms = new List<BoolExpr>();

        foreach (object element in domain)
        {
            Expression substituted = ParameterSubstituter.SubstituteParameter(
                predicate,
                Expression.Constant(element, predicate.Parameters[0].Type));

            // Same reason as in MaterializeVariadicArguments: PartialEval wants a LambdaExpression,
            // so the substituted body is wrapped and the folded body taken back off.
            Expression folded = PartialEvaluator.PartialEval(Expression.Lambda(substituted), ExpressionInterpreter.Instance).Body;

            terms.Add(Visit(folded) as BoolExpr ?? throw new InvalidOperationException(
                $"The predicate of {name} must be Boolean, got {folded.NodeType}."));
        }

        if (terms.Count == 0)
        {
            return isUniversal ? this.context.MkTrue() : this.context.MkFalse();
        }

        return isUniversal ? this.context.MkAnd(terms.ToArray()) : this.context.MkOr(terms.ToArray());
    }

    /// <summary>
    /// Evaluates an expression that must be a host value, not a theorem term.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <returns>Its value.</returns>
    /// <remarks>
    /// Used for the parts of a magic-method call that describe the constraint rather than
    /// participate in it - a bound, a weight array. The expression is folded first so that a literal,
    /// a <see cref="NewArrayExpression"/> of literals and a closure-captured member all reduce to the
    /// same constant. An expression that still depends on the theorem's parameter has no value and
    /// surfaces as an interpret failure, which is the wanted outcome: a symbolic weight is a
    /// different constraint, not this one.
    /// </remarks>
    private static object? EvaluateToHostValue(Expression expression)
    {
        if (expression is ConstantExpression constant)
        {
            return constant.Value;
        }

        // Same MiaPlaza 1.3 constraint as in MaterializeVariadicArguments: PartialEval only takes a
        // LambdaExpression, so the expression is wrapped and the folded body taken back off.
        Expression folded = PartialEvaluator.PartialEval(Expression.Lambda(expression), ExpressionInterpreter.Instance).Body;

        return folded is ConstantExpression foldedConstant
            ? foldedConstant.Value
            : ExpressionInterpreter.Instance.Interpret(folded);
    }

    /// <summary>
    /// Materialises the arguments of a variadic magic method into the expressions they stand for.
    /// </summary>
    /// <param name="itemsExpression">The <c>params</c> argument of the call.</param>
    /// <param name="method">The method being called, for the diagnostics.</param>
    /// <returns>One expression per argument.</returns>
    /// <remarks>
    /// A <c>params</c> argument arrives in one of two shapes: a <see cref="NewArrayExpression"/>,
    /// when the arguments are written out at the call site, or <c>Select(...).ToArray()</c> over a
    /// collection, when they are computed. The second is expanded here, one substituted-and-folded
    /// body per element, so the caller sees a flat list either way.
    /// </remarks>
    /// <exception cref="NotSupportedException">The argument has neither shape.</exception>
    private static IEnumerable<Expression> MaterializeVariadicArguments(Expression itemsExpression, MethodInfo method)
    {
        if (itemsExpression is NewArrayExpression arrExp)
        {
            return arrExp.Expressions;
        }

        if (itemsExpression is MethodCallExpression mExp
            && mExp.Method.IsGenericMethod
            && mExp.Method.GetGenericMethodDefinition() == EnumerableToArrayMethod
            && mExp.Arguments[0] is MethodCallExpression callerToArrayMethodExp
            && callerToArrayMethodExp.Method.IsGenericMethod
            && callerToArrayMethodExp.Method.GetGenericMethodDefinition() == EnumerableSelectMethod)
        {
            var selector = callerToArrayMethodExp.Arguments[1] as LambdaExpression
                ?? throw new NotSupportedException("Unsupported method call: " + method + " with a non-lambda selector.");

            // The source is any IEnumerable, not only an ICollection: Enumerable.Range, a Where
            // clause and a Select over either are all lazy, and none of them implements ICollection.
            // Concretising it once keeps the count and the walk consistent, and each element then
            // becomes a Constant the selector's parameter can be substituted with.
            object? source = ExpressionInterpreter.Instance.Interpret(callerToArrayMethodExp.Arguments[0]);

            if (source is not IEnumerable items)
            {
                throw new NotSupportedException("Unsupported method call: " + method + " over a source that is not enumerable.");
            }

            var caller = items.Cast<object>().ToList();
            var subExps = new List<Expression>(caller.Count);

            foreach (var item in caller)
            {
                var substitutedExpression = ParameterSubstituter.SubstituteParameter(selector, Expression.Constant(item));

                // SubstituteParameter yields the selector's body, which is not a lambda, but from
                // 1.3.0 PartialEval only accepts a LambdaExpression. Wrapping the body in a lambda
                // and taking the partially evaluated Body back off is the supported equivalent of
                // the overload that used to take a bare Expression. The body still references the
                // theorem's own parameter, which stays free in the wrapper - the evaluator leaves
                // parameter-dependent subtrees alone and folds only the closed ones, exactly as
                // before.
                var wrappedExpression = Expression.Lambda(substitutedExpression);
                var newlyFlattened = PartialEvaluator.PartialEval(wrappedExpression, ExpressionInterpreter.Instance).Body;
                subExps.Add(newlyFlattened);
            }

            return subExps;
        }

        throw new NotSupportedException("Unsupported method call: " + method + " with sub expression " + itemsExpression);
    }

    /// <summary>
    /// Translates a constant value into a Z3 term.
    /// </summary>
    /// <param name="val">The constant value.</param>
    /// <returns>Z3 expression handle.</returns>
    private Expr VisitConstantValue(object val)
    {
        // Exact rational constants are emitted as the fraction num/den, which Z3 parses as a
        // rational. This is the path a CLR double cannot offer for a non-terminating fraction:
        // 1/3 stringified from a double is a sixteen-digit truncation, and 3 * (that) is not 1.
        // Checked before the TypeCode switch because a Rational is a struct, so TypeCode.Object.
        if (val is Rational rational)
        {
            return this.context.MkReal(rational.ToString());
        }

        TypeCode typeCode = Type.GetTypeCode(val.GetType());

        return typeCode switch
        {
            TypeCode.SByte or TypeCode.Byte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.Int64
                => this.context.MkInt(Convert.ToInt64(val)),

            // uint and ulong are bit-vectors of their width. See #99's bit-vector follow-up.
            TypeCode.UInt32 or TypeCode.UInt64
                => this.context.MkBV(Convert.ToUInt64(val), Theorem.BitVectorWidth(typeCode)!.Value),

            TypeCode.Boolean => this.context.MkBool((bool)val),

            // Invariant, not current: Z3's parser accepts only '.' as the decimal separator,
            // and about half of all cultures render 1.5 as something else. See #52.
            TypeCode.Single or TypeCode.Double or TypeCode.Decimal
                => this.context.MkReal(((IFormattable)val).ToString(null, CultureInfo.InvariantCulture)),

            // A DateTime is encoded as its ticks - 100ns intervals from 0001-01-01 - on the UTC
            // timeline, which covers the whole DateTime range. A Windows file time counted from
            // 1601 instead, so nothing earlier could be written or read. See #83.
            //
            // A Kind of Local is converted to UTC first; Unspecified is taken to be UTC already,
            // which is the convention ToFileTimeUtc had and the read path inverts. See #56.
            TypeCode.DateTime => this.context.MkInt(ToUtcTicks((DateTime)val)),

            TypeCode.String => this.context.MkString((string)val),

            _ => throw new NotSupportedException($"Unsupported constant {val}"),
        };
    }

    private Expr VisitIndex(IndexExpression expression, Func<Context, Expr, Expr[], Expr> ctor)
    {
        if (this.TryFoldClosedRead(expression, out Expr? folded))
        {
            return folded!;
        }

        var args = expression.Arguments.Select(Visit).ToArray();
        return ctor(this.context, Visit(expression.Object!), args);
    }

    /// <summary>
    /// Evaluates an element read that mentions nothing of the theorem, so the read denotes a constant.
    /// </summary>
    /// <param name="expression">A node reading one element out of a captured collection.</param>
    /// <param name="folded">The Z3 term the element's value translates to, when it was folded.</param>
    /// <returns><see langword="true"/> when the node was folded.</returns>
    /// <remarks>
    /// <c>kcalArr[i]</c> over a captured array arrives as an ArrayIndex whose object is a single
    /// ConstantExpression holding the whole array, and <c>list[i]</c> as a get_Item call routed
    /// here through an IndexExpression. The visitor translates scalars, so the array itself used
    /// to reach <see cref="VisitConstantValue"/> and fail there with "Unsupported constant
    /// System.Int32[]". Reading one element of a closed collection is a constant, so it is
    /// evaluated here and translated as such. Two conditions keep this from swallowing anything
    /// else: the node may mention neither the theorem's parameter nor a Z3Methods marker (both are
    /// translated, never evaluated), and an evaluation that fails leaves the node to the paths
    /// that handled it before.
    /// </remarks>
    private bool TryFoldClosedRead(Expression expression, out Expr? folded)
    {
        folded = null;

        if (ParameterFinder.Contains(expression) || MentionsTranslationMarker(expression))
        {
            return false;
        }

        object? value;

        try
        {
            value = Expression.Lambda(expression).Compile(preferInterpretation: true).DynamicInvoke();
        }
        catch (Exception)
        {
            return false;
        }

        if (value is null)
        {
            return false;
        }

        try
        {
            folded = this.VisitConstantValue(value);
        }
        catch (NotSupportedException)
        {
            folded = null;

            return false;
        }

        return true;
    }

    /// <summary>
    /// Translates a member expression - a symbol access on the environment, or a captured constant.
    /// </summary>
    /// <param name="member">Member expression.</param>
    /// <returns>Z3 expression handle.</returns>
    private Expr VisitMember(MemberExpression member)
    {
        // E.g. Symbols l = ...;
        //      theorem.Where(s => l.X1)
        //                         ^^
        List<MemberExpression> hierarchy = [];
        var mExp = member;
        hierarchy.Add(mExp);

        while (mExp.Expression is MemberExpression parent)
        {
            mExp = parent;
            hierarchy.Add(parent);
        }

        hierarchy.Reverse();

        var topMember = hierarchy.First();

        if (topMember.Expression != this.param)
        {
            // Two roots reduce the same way: a captured instance, which is a ConstantExpression,
            // and a static member, whose root expression is null because there is no instance -
            // DateTime.MaxValue is a static property, and a bound such as x.D > DateTime.MaxValue
            // is written against it.
            if (topMember.Expression is null || topMember.Expression is ConstantExpression)
            {
                // We only ever get here if SimplifyLambda is set to false, otherwise partial evaluation does it earlier
                var target = (topMember.Expression as ConstantExpression)?.Value;
                var hierarchyIdx = 0;
                object? val = target;

                while (hierarchyIdx < hierarchy.Count)
                {
                    var currentMember = hierarchy[hierarchyIdx].Member;

                    // Each level is read off the previous level's value, not off the constant: a
                    // captured local can be nested more than one closure deep. Roslyn gives a loop
                    // body its own display class inside the method's, so a lambda that captures
                    // both a method parameter and a loop variable reaches its outer value through
                    // two fields - and reading every level off the constant fails on the second
                    // with "field ... is not a field on the target object". A property chain on a
                    // captured instance, obj.Limit.Value, has the same shape.
                    val = currentMember switch
                    {
                        PropertyInfo property => property.GetValue(val),
                        FieldInfo field => field.GetValue(val),
                        _ => throw new NotSupportedException($"Unsupported constant {topMember.Expression} ."),
                    };

                    hierarchyIdx++;
                }

                return VisitConstantValue(val ?? throw new NotSupportedException($"Could not reduce expression {topMember.Expression}"));
            }
        }

        // Only members we allow currently are direct accesses to the theorem's variables
        // in the environment type. So we just try to find the mapping from the environment
        // bindings table.
        Environment subEnv = this.environment;

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

        return subEnv.Expr!;
    }

    /// <summary>
    /// Translates a unary expression, transforming the translated operand with <paramref name="ctor"/>.
    /// </summary>
    /// <param name="expression">Unary expression.</param>
    /// <param name="ctor">Combines the context and the recursively translated operand.</param>
    /// <returns>Z3 expression handle.</returns>
    private Expr VisitUnary(UnaryExpression expression, Func<Context, Expr, Expr> ctor)
    {
        return ctor(this.context, Visit(expression.Operand));
    }

    /// <summary>
    /// The ticks of <paramref name="value"/> on the UTC timeline, which is how a
    /// <see cref="DateTime"/> is encoded for Z3.
    /// </summary>
    /// <remarks>
    /// A <see cref="DateTimeKind.Local"/> value is converted to UTC; an
    /// <see cref="DateTimeKind.Unspecified"/> one is taken to be UTC already, as
    /// <see cref="DateTime.ToFileTimeUtc"/> - the previous encoding - did. The read path in
    /// <c>Theorem</c> produces a <see cref="DateTimeKind.Utc"/> value from the same ticks.
    /// See #56 and #83.
    /// </remarks>
    internal static long ToUtcTicks(DateTime value)
    {
        return value.Kind == DateTimeKind.Local ? value.ToUniversalTime().Ticks : value.Ticks;
    }

    /// <summary>Reports whether a Z3Methods marker call appears anywhere in an expression tree.</summary>
    private sealed class MarkerFinder : ExprVisitor
    {
        internal bool Found { get; private set; }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            this.Found = this.Found || node.Method.DeclaringType == typeof(Z3Methods);

            return this.Found ? node : base.VisitMethodCall(node);
        }
    }
}
