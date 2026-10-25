namespace Z3.Linq;

using System;

/// <summary>
/// Z3 predicate methods.
/// </summary>
public static class Z3Methods
{
    /// <summary>
    /// Creates a predicate constraining the given symbols as distinct values.
    /// </summary>
    /// <typeparam name="T">Type of the parameters.</typeparam>
    /// <param name="symbols">Symbols that are required to be distinct.</param>
    /// <returns>Predicate return value.</returns>
    /// <remarks>This method should only be used within LINQ expressions.</remarks>
    public static bool Distinct<T>(params T[] symbols /* type? */)
    {
        throw new NotSupportedException("This method should only be used in query expressions.");
    }

    /// <summary>
    /// Creates a term that is the sum of the given terms.
    /// </summary>
    /// <typeparam name="T">Type of the terms.</typeparam>
    /// <param name="terms">Terms to add.</param>
    /// <returns>The sum.</returns>
    /// <remarks>
    /// <para>
    /// This method should only be used within LINQ expressions. It maps to a single Z3 addition,
    /// which is variadic, so a sum of any number of terms is one node rather than a left fold of
    /// pairwise additions.
    /// </para>
    /// <para>
    /// Its point is a sum whose length is not known when the constraint is written: an aggregation
    /// over a collection, such as <c>Z3Methods.Sum(Enumerable.Range(0, n).Select(i =&gt; g.Cells[i]).ToArray())</c>.
    /// Writing <c>a + b + c</c> cannot express that.
    /// </para>
    /// </remarks>
    public static T Sum<T>(params T[] terms)
    {
        throw new NotSupportedException("This method should only be used in query expressions.");
    }

    /// <summary>
    /// Creates a predicate stating that exactly one of the given indicators holds.
    /// </summary>
    /// <param name="indicators">Boolean indicators, exactly one of which must be true.</param>
    /// <returns>Predicate return value.</returns>
    /// <remarks>
    /// <para>
    /// This method should only be used within LINQ expressions. It maps to a single pseudo-Boolean
    /// constraint rather than to the quadratic pairwise encoding.
    /// </para>
    /// <para>
    /// Its point is a cardinality constraint whose length is not known when the constraint is
    /// written, such as one indicator per cell of a variable-size group.
    /// </para>
    /// </remarks>
    public static bool ExactlyOne(params bool[] indicators)
    {
        throw new NotSupportedException("This method should only be used in query expressions.");
    }

    /// <summary>
    /// Creates a predicate stating that at most one of the given indicators holds.
    /// </summary>
    /// <param name="indicators">Boolean indicators, at most one of which may be true.</param>
    /// <returns>Predicate return value.</returns>
    /// <remarks>
    /// <para>
    /// This method should only be used within LINQ expressions. It maps to a single pseudo-Boolean
    /// constraint, encoded as "at least n-1 of the negated indicators".
    /// </para>
    /// <para>
    /// Its point is the same as <see cref="ExactlyOne"/>'s: a cardinality constraint over a group
    /// whose size is a runtime value. This is the constraint that rules out two values sharing a
    /// group, the usual shape of a Latin-square or Sudoku column.
    /// </para>
    /// </remarks>
    public static bool AtMostOne(params bool[] indicators)
    {
        throw new NotSupportedException("This method should only be used in query expressions.");
    }

    /// <summary>
    /// Creates a predicate stating that at least one of the given indicators holds.
    /// </summary>
    /// <param name="indicators">Boolean indicators, at least one of which must be true.</param>
    /// <returns>Predicate return value.</returns>
    /// <remarks>
    /// <para>
    /// This method should only be used within LINQ expressions. It maps to a single pseudo-Boolean
    /// constraint, which stays linear where a disjunction over the same indicators would be one
    /// <c>or</c> node of n children.
    /// </para>
    /// <para>
    /// Its point is the same as <see cref="ExactlyOne"/>'s: a cardinality constraint over a group
    /// whose size is a runtime value.
    /// </para>
    /// </remarks>
    public static bool AtLeastOne(params bool[] indicators)
    {
        throw new NotSupportedException("This method should only be used in query expressions.");
    }

    /// <summary>
    /// Creates a predicate stating that the weighted sum of the given indicators reaches a bound.
    /// </summary>
    /// <param name="bound">The lower bound on the weighted sum.</param>
    /// <param name="weights">One weight per indicator, known at translation time.</param>
    /// <param name="indicators">Boolean indicators whose weights are summed.</param>
    /// <returns>Predicate return value.</returns>
    /// <remarks>
    /// <para>
    /// This method should only be used within LINQ expressions. It maps to a native
    /// <c>MkPBGe</c>, keeping Z3's own pseudo-Boolean propagation.
    /// </para>
    /// <para>
    /// Its point is a band over a group of options carrying unequal costs or contributions, such as
    /// a nutrition bound over the recipes selected for a meal.
    /// </para>
    /// </remarks>
    public static bool WeightedAtLeast(int bound, int[] weights, params bool[] indicators)
    {
        throw new NotSupportedException("This method should only be used in query expressions.");
    }

    /// <summary>
    /// Creates a predicate stating that the weighted sum of the given indicators stays under a bound.
    /// </summary>
    /// <param name="bound">The upper bound on the weighted sum.</param>
    /// <param name="weights">One weight per indicator, known at translation time.</param>
    /// <param name="indicators">Boolean indicators whose weights are summed.</param>
    /// <returns>Predicate return value.</returns>
    /// <remarks>This method should only be used within LINQ expressions. It maps to a native <c>MkPBLe</c>.</remarks>
    public static bool WeightedAtMost(int bound, int[] weights, params bool[] indicators)
    {
        throw new NotSupportedException("This method should only be used in query expressions.");
    }

    /// <summary>
    /// Creates a predicate stating that the weighted sum of the given indicators equals a bound.
    /// </summary>
    /// <param name="bound">The value the weighted sum must take.</param>
    /// <param name="weights">One weight per indicator, known at translation time.</param>
    /// <param name="indicators">Boolean indicators whose weights are summed.</param>
    /// <returns>Predicate return value.</returns>
    /// <remarks>This method should only be used within LINQ expressions. It maps to a native <c>MkPBEq</c>.</remarks>
    public static bool WeightedExactly(int bound, int[] weights, params bool[] indicators)
    {
        throw new NotSupportedException("This method should only be used in query expressions.");
    }

    /// <summary>
    /// Creates a predicate stating that the given predicate holds for every element of a domain.
    /// </summary>
    /// <typeparam name="T">Type of the domain's elements.</typeparam>
    /// <param name="domain">A finite domain known at translation time, such as <c>Enumerable.Range(0, n)</c>.</param>
    /// <param name="predicate">Predicate over one domain element.</param>
    /// <returns>Predicate return value.</returns>
    /// <remarks>
    /// <para>
    /// This method should only be used within LINQ expressions. The domain is evaluated on the host
    /// and the predicate is unrolled into a conjunction over its elements, so an empty domain holds
    /// vacuously.
    /// </para>
    /// <para>
    /// It is a bounded quantifier over a finite, enumerable domain - what a C# loop emitting one
    /// constraint per index would otherwise express - and <em>not</em> an SMT quantifier over an
    /// unbounded sort.
    /// </para>
    /// </remarks>
    public static bool ForAll<T>(IEnumerable<T> domain, Func<T, bool> predicate)
    {
        throw new NotSupportedException("This method should only be used in query expressions.");
    }

    /// <summary>
    /// Creates a predicate stating that the given predicate holds for at least one element of a domain.
    /// </summary>
    /// <typeparam name="T">Type of the domain's elements.</typeparam>
    /// <param name="domain">A finite domain known at translation time, such as <c>Enumerable.Range(0, n)</c>.</param>
    /// <param name="predicate">Predicate over one domain element.</param>
    /// <returns>Predicate return value.</returns>
    /// <remarks>
    /// <para>
    /// This method should only be used within LINQ expressions. The domain is evaluated on the host
    /// and the predicate is unrolled into a disjunction over its elements, so an empty domain is
    /// unsatisfiable.
    /// </para>
    /// <para>
    /// It is a bounded quantifier over a finite, enumerable domain, and <em>not</em> an SMT
    /// quantifier over an unbounded sort.
    /// </para>
    /// </remarks>
    public static bool Exists<T>(IEnumerable<T> domain, Func<T, bool> predicate)
    {
        throw new NotSupportedException("This method should only be used in query expressions.");
    }
}