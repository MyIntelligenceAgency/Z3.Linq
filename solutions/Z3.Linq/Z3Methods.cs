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
    /// Reduces a collection of arithmetic terms to their sum, mapped to Z3 <c>MkAdd</c>.
    /// </summary>
    /// <typeparam name="T">The term type (int/long/double/decimal).</typeparam>
    /// <param name="terms">The terms to add.</param>
    /// <returns>The sum.</returns>
    /// <remarks>
    /// This is the variadic counterpart to the binary <c>+</c> visitor case (which only folds
    /// pairwise <c>a + b</c>). Before B3, summing an arbitrary number of theorem-collection
    /// elements required manual per-element unrolling (e.g. Sudoku row/column distinctness, or
    /// a sum-of-indicators aggregation like <c>sum_i Sel[j][i] * kcal[i]</c>). With <c>Sum</c>,
    /// such aggregations express naturally over a materialized collection. Only used in LINQ
    /// expressions; same magic-method mechanism as <see cref="Distinct{T}"/>.
    /// </remarks>
    public static T Sum<T>(params T[] terms)
    {
        throw new NotSupportedException("This method should only be used in query expressions.");
    }
}