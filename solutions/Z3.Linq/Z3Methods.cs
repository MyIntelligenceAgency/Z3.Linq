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
}