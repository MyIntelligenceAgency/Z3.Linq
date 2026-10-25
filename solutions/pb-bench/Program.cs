// pb-bench: c.8247 measurement harness — compare MkPBGe (raw Z3 native PB)
// vs MkIte+MkAdd expansion on the same Pseudo-Boolean constraint.
//
// Reference: docs/secrets-and-coord-detail.md §2 -- "mesurer, ne pas supposer"
// (issue #10605 acceptance criterion #1).
//
// We compare on three sizes (n=10, 50, 100 boolean indicators, bound = 1) --
// the canonical Exactly-One pattern used by notebook 09 (meal planner). For
// each size, we record:
//   - Number of AST nodes produced (rough proxy for solver propagation surface)
//   - Solver wall-clock (ms) for satisfiability check
//   - SMT-LIB string length (proxy for the constraint chunk Z3 has to manage)

namespace PbBench;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Microsoft.Z3;

internal static class Program
{
    private static int Main()
    {
        int[] sizes = { 10, 50, 100 };
        Console.WriteLine("size\tvariant\tnodes\tsmt2len\tz3ms\tstatus");
        foreach (var n in sizes)
        {
            // SAT case: exactly-one of b_0..b_{n-1} (constraint alone is satisfiable).
            MeasureOnce(n, "MkPBGe", useNativePb: true, forceUnsat: false);
            MeasureOnce(n, "MkIte+Add", useNativePb: false, forceUnsat: false);

            // UNSAT case: exactly-one AND b_0 is forced true AND b_1 is forced true
            // (forces two indicators on, violates the exactly-one).
            // This is the case where propagation matters most.
            MeasureOnce(n, "MkPBGe", useNativePb: true, forceUnsat: true);
            MeasureOnce(n, "MkIte+Add", useNativePb: false, forceUnsat: true);
        }

        // Weighted band shape (c.8252, issue #10605): weights != 1 (kcal-like values),
        // bound around half the total weight -- the nutrition-band form of notebook 09.
        // SAT case: band alone is satisfiable. UNSAT case: forced indicators push the
        // weighted sum past the ceiling while the band demands less.
        Console.WriteLine();
        Console.WriteLine("-- weighted bands (weights != 1) --");
        foreach (var n in sizes)
        {
            MeasureWeightedOnce(n, "MkPBGe", useNativePb: true, forceUnsat: false);
            MeasureWeightedOnce(n, "MkIte+Add", useNativePb: false, forceUnsat: false);
            MeasureWeightedOnce(n, "MkPBGe", useNativePb: true, forceUnsat: true);
            MeasureWeightedOnce(n, "MkIte+Add", useNativePb: false, forceUnsat: true);
        }
        return 0;
    }

    private static void MeasureWeightedOnce(int n, string variant, bool useNativePb, bool forceUnsat)
    {
        using var ctx = new Context(new Dictionary<string, string>
        {
            { "MODEL", "true" },
            { "TIMEOUT", "5000" },
        });

        var bs = new BoolExpr[n];
        for (int i = 0; i < n; i++) bs[i] = ctx.MkBoolConst($"b{i}");

        // Deterministic kcal-like weights and a band around half the total.
        var weights = new int[n];
        int total = 0;
        for (int i = 0; i < n; i++)
        {
            weights[i] = 100 + (i * 137) % 400;
            total += weights[i];
        }
        int floor = total / 2 - 50;   // band: [half-50, half+50]
        int ceiling = total / 2 + 50;

        var sw = Stopwatch.StartNew();
        BoolExpr lo, hi;

        if (useNativePb)
        {
            lo = ctx.MkPBGe(weights, bs, floor);
            hi = ctx.MkPBLe(weights, bs, ceiling);
        }
        else
        {
            // MkIte+MkAdd expansion: sum_i (ite b_i w_i 0) then compare via MkGe/MkLe.
            var terms = new ArithExpr[n];
            for (int i = 0; i < n; i++)
            {
                terms[i] = (ArithExpr)ctx.MkITE(bs[i], ctx.MkInt(weights[i]), ctx.MkInt(0));
            }
            var sum = ctx.MkAdd(terms);
            lo = ctx.MkGe(sum, ctx.MkInt(floor));
            hi = ctx.MkLe(sum, ctx.MkInt(ceiling));
        }

        var solver = ctx.MkSolver();
        solver.Assert(lo);
        solver.Assert(hi);
        if (forceUnsat)
        {
            // Force enough heavy indicators on to blow past the ceiling.
            int acc = 0;
            for (int i = 0; i < n && acc <= ceiling; i++)
            {
                solver.Assert(ctx.MkEq(bs[i], ctx.MkTrue()));
                acc += weights[i];
            }
        }
        var status = solver.Check();

        sw.Stop();

        var constraint = ctx.MkAnd(lo, hi);
        var nodes = CountNodes(constraint);
        var smt2Len = Encoding.UTF8.GetByteCount(constraint.ToString());

        Console.WriteLine($"{n}\t{variant}\t{nodes}\t{smt2Len}\t{sw.ElapsedMilliseconds}\t{status}");
    }

    private static void MeasureOnce(int n, string variant, bool useNativePb, bool forceUnsat)
    {
        using var ctx = new Context(new Dictionary<string, string>
        {
            { "MODEL", "true" },
            { "TIMEOUT", "5000" },
        });

        // Boolean indicators b_0, ..., b_{n-1}.
        var bs = new BoolExpr[n];
        for (int i = 0; i < n; i++) bs[i] = ctx.MkBoolConst($"b{i}");

        var sw = Stopwatch.StartNew();
        BoolExpr constraint;

        if (useNativePb)
        {
            // Native Z3 PB: sum of weights * indicators >= bound.
            // Exactly-one: sum(b_i) == 1  ==  sum(b_i) >= 1  AND  sum((1-b_i)) >= n-1.
            var weights = new int[n];
            for (int i = 0; i < n; i++) weights[i] = 1;
            var indicators = new BoolExpr[n];
            for (int i = 0; i < n; i++) indicators[i] = bs[i];

            var lhsLower = ctx.MkPBGe(weights, indicators, 1);
            var notBs = new BoolExpr[n];
            for (int i = 0; i < n; i++) notBs[i] = ctx.MkNot(bs[i]);
            var lhsUpper = ctx.MkPBGe(weights, notBs, n - 1);

            constraint = ctx.MkAnd(lhsLower, lhsUpper);
        }
        else
        {
            // MkIte+MkAdd expansion -- each b_i becomes (ite b_i 1 0),
            // we build (MkAdd iteb_i ...) and compare to 1 via MkEq.
            // Upper bound: same, with (ite (not b_i) 1 0).
            var lowers = new ArithExpr[n];
            for (int i = 0; i < n; i++)
            {
                lowers[i] = (ArithExpr)ctx.MkITE(bs[i], ctx.MkInt(1), ctx.MkInt(0));
            }
            var lowerSum = ctx.MkAdd(lowers);
            var lowerConj = ctx.MkEq(lowerSum, ctx.MkInt(1));

            var uppers = new ArithExpr[n];
            for (int i = 0; i < n; i++)
            {
                uppers[i] = (ArithExpr)ctx.MkITE(ctx.MkNot(bs[i]), ctx.MkInt(1), ctx.MkInt(0));
            }
            var upperSum = ctx.MkAdd(uppers);
            var upperConj = ctx.MkEq(upperSum, ctx.MkInt(n - 1));

            constraint = ctx.MkAnd(lowerConj, upperConj);
        }

        var solver = ctx.MkSolver();
        solver.Assert(constraint);
        if (forceUnsat)
        {
            // Force exactly-two to make the exactly-one unsat. This is the
            // case where PB propagation should help Z3 detect UNSAT fast.
            solver.Assert(ctx.MkEq(bs[0], ctx.MkTrue()));
            solver.Assert(ctx.MkEq(bs[1], ctx.MkTrue()));
        }
        var status = solver.Check();

        sw.Stop();

        // Rough proxy for AST surface: count of assertions.
        var nodes = CountNodes(constraint);
        var smt2Len = Encoding.UTF8.GetByteCount(constraint.ToString());

        Console.WriteLine($"{n}\t{variant}\t{nodes}\t{smt2Len}\t{sw.ElapsedMilliseconds}\t{status}");
    }

    private static int CountNodes(Expr e)
    {
        // Recursive node count: cover the AST cheaply. We use a BFS over
        // distinct subexpressions via id() avoiding double-counts.
        var seen = new System.Collections.Generic.HashSet<uint>();
        var queue = new System.Collections.Generic.Queue<Expr>();
        queue.Enqueue(e);
        int count = 0;
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            uint id = cur.Id;
            if (!seen.Add(id)) continue;
            count++;
            for (uint i = 0; i < cur.NumArgs; i++)
            {
                queue.Enqueue(cur.Arg(i));
            }
        }
        return count;
    }
}
