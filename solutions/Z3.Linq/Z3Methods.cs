namespace Z3.Linq;

using System;
using System.Collections.Generic;

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

    /// <summary>
    /// Bounded universal quantifier: the predicate must hold for every element of the
    /// (finite, host-evaluable) domain. The visitor unrolls it into a conjunction
    /// <c>MkAnd(predicate(d0), predicate(d1), ...)</c> — backlog item B8 (#4616).
    /// An empty domain yields <c>true</c>.
    /// </summary>
    /// <typeparam name="T">Element type of the bound variable (e.g. <c>int</c> index).</typeparam>
    /// <param name="domain">A finite domain known at translation time (e.g. <c>Enumerable.Range(0, n)</c>).</param>
    /// <param name="predicate">Predicate over a domain element; it must reference the theorem parameter.</param>
    /// <returns>Predicate return value.</returns>
    /// <remarks>
    /// This method should only be used within LINQ expressions. The <paramref name="domain"/> is
    /// evaluated in the host; the <paramref name="predicate"/> is expanded over each element. This is
    /// a bounded (finite-domain) quantifier, not a true SMT <c>forall</c> over an unbounded sort.
    /// </remarks>
    public static bool ForAll<T>(IEnumerable<T> domain, Func<T, bool> predicate)
    {
        throw new NotSupportedException("This method should only be used in query expressions.");
    }

    /// <summary>
    /// Bounded existential quantifier: the predicate must hold for at least one element of the
    /// (finite, host-evaluable) domain. The visitor unrolls it into a disjunction
    /// <c>MkOr(predicate(d0), predicate(d1), ...)</c> — backlog item B8 (#4616).
    /// An empty domain yields <c>false</c>.
    /// </summary>
    /// <typeparam name="T">Element type of the bound variable (e.g. <c>int</c> index).</typeparam>
    /// <param name="domain">A finite domain known at translation time (e.g. <c>Enumerable.Range(0, n)</c>).</param>
    /// <param name="predicate">Predicate over a domain element; it must reference the theorem parameter.</param>
    /// <returns>Predicate return value.</returns>
    /// <remarks>
    /// This method should only be used within LINQ expressions. The <paramref name="domain"/> is
    /// evaluated in the host; the <paramref name="predicate"/> is expanded over each element. This is
    /// a bounded (finite-domain) quantifier, not a true SMT <c>exists</c> over an unbounded sort.
    /// </remarks>
    public static bool Exists<T>(IEnumerable<T> domain, Func<T, bool> predicate)
    {
        throw new NotSupportedException("This method should only be used in query expressions.");
    }

    /// <summary>
    /// Exactly-one: at most one and at least one of the given Boolean indicators is true.
    /// Maps to native Z3 pseudo-Boolean (<c>ctx.MkPBGe</c> with weights {1..1} on the
    /// indicators and on their negations). This is the canonical "one-hot" constraint,
    /// used for example by notebook 09 (meal planner) to select exactly one recipe per slot.
    /// </summary>
    /// <param name="indicators">The Boolean indicators (typically theorem parameters).</param>
    /// <returns>Predicate return value.</returns>
    /// <remarks>
    /// This method should only be used within LINQ expressions. Measurement on the fork
    /// (pb-bench, c.8247): native <c>MkPBGe</c> builds ~half the AST nodes and runs 3-6x
    /// faster on the planner's 7x5 instance than the equivalent <c>MkIte + MkAdd</c>
    /// expansion. We expose the unweighted case only (see #10605 / #4616 backlog).
    /// Backlog item B-pseudo-bool (the last open item of #4616).
    /// </remarks>
    public static bool ExactlyOne(params bool[] indicators)
    {
        throw new NotSupportedException("This method should only be used in query expressions.");
    }

    /// <summary>
    /// At-most-one: zero or one of the given Boolean indicators is true.
    /// Half of the exactly-one constraint; useful when at-least-one is enforced separately
    /// (e.g. by a domain predicate).
    /// </summary>
    /// <param name="indicators">The Boolean indicators.</param>
    /// <returns>Predicate return value.</returns>
    public static bool AtMostOne(params bool[] indicators)
    {
        throw new NotSupportedException("This method should only be used in query expressions.");
    }

    /// <summary>
    /// At-least-one: one or more of the given Boolean indicators is true.
    /// The other half of exactly-one; useful when at-most-one is enforced separately.
    /// </summary>
    /// <param name="indicators">The Boolean indicators.</param>
    /// <returns>Predicate return value.</returns>
    public static bool AtLeastOne(params bool[] indicators)
    {
        throw new NotSupportedException("This method should only be used in query expressions.");
    }

    /// <summary>
    /// Weighted at-least: <c>sum(weights[i] * indicators[i]) &gt;= bound</c>, mapped to native
    /// Z3 pseudo-Boolean (<c>ctx.MkPBGe</c>). This is the band form used by notebook 09
    /// (meal planner nutrition bounds): unlike the unweighted trio above, the weights are
    /// host constants (e.g. kcal per recipe) and the bound is the nutritional floor.
    /// </summary>
    /// <param name="bound">The lower bound on the weighted sum (host constant).</param>
    /// <param name="weights">Weight of each indicator, evaluated in the host.</param>
    /// <param name="indicators">The Boolean indicators (typically theorem parameters).</param>
    /// <returns>Predicate return value.</returns>
    /// <remarks>
    /// This method should only be used within LINQ expressions. <paramref name="weights"/> must
    /// have exactly as many elements as <paramref name="indicators"/>. Design note (issue #10605):
    /// a dedicated magic method is preferred over a weighted <c>Sum</c> overload because it maps
    /// 1:1 onto native <c>MkPBGe</c>, keeping Z3's pseudo-Boolean theory solver in charge of
    /// propagation -- pb-bench (c.8252) measures the gap against the <c>MkIte + MkAdd</c>
    /// expansion on the weighted band shape.
    /// </remarks>
    public static bool WeightedAtLeast(int bound, int[] weights, params bool[] indicators)
    {
        throw new NotSupportedException("This method should only be used in query expressions.");
    }

    /// <summary>
    /// Weighted at-most: <c>sum(weights[i] * indicators[i]) &lt;= bound</c>, mapped to native
    /// Z3 pseudo-Boolean (<c>ctx.MkPBLe</c>). Upper band form (e.g. sugar/sodium ceilings).
    /// </summary>
    /// <param name="bound">The upper bound on the weighted sum (host constant).</param>
    /// <param name="weights">Weight of each indicator, evaluated in the host.</param>
    /// <param name="indicators">The Boolean indicators (typically theorem parameters).</param>
    /// <returns>Predicate return value.</returns>
    /// <remarks>
    /// This method should only be used within LINQ expressions. <paramref name="weights"/> must
    /// have exactly as many elements as <paramref name="indicators"/>.
    /// </remarks>
    public static bool WeightedAtMost(int bound, int[] weights, params bool[] indicators)
    {
        throw new NotSupportedException("This method should only be used in query expressions.");
    }

    /// <summary>
    /// Weighted exactly: <c>sum(weights[i] * indicators[i]) == bound</c>, mapped to native
    /// Z3 pseudo-Boolean (<c>ctx.MkPBEq</c>). Equality band form (e.g. exact macro split).
    /// </summary>
    /// <param name="bound">The required value of the weighted sum (host constant).</param>
    /// <param name="weights">Weight of each indicator, evaluated in the host.</param>
    /// <param name="indicators">The Boolean indicators (typically theorem parameters).</param>
    /// <returns>Predicate return value.</returns>
    /// <remarks>
    /// This method should only be used within LINQ expressions. <paramref name="weights"/> must
    /// have exactly as many elements as <paramref name="indicators"/>.
    /// </remarks>
    public static bool WeightedExactly(int bound, int[] weights, params bool[] indicators)
    {
        throw new NotSupportedException("This method should only be used in query expressions.");
    }
}