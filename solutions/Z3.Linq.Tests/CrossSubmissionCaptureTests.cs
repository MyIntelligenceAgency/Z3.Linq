namespace Z3.Linq.Tests;

using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Xunit;

/// <summary>
/// Reproduces the cross-submission capture crash in <see cref="Theorem.AssertConstraints"/>
/// (fix PR #43): a constraint lambda captures a local declared in a *different* compiled
/// submission, so the expression tree roots the captured field at a closure object whose
/// runtime type is not the field's declaring type. <c>ExpressionVisitor.VisitMember</c>
/// then resolves the field reflectively and <c>FieldInfo.GetValue</c> rejects the mismatch
/// ("Field 'b' defined on type 'Submission#0' is not a field on the target object of type
/// 'Submission#1'"). Single-assembly tests cannot produce this shape by construction —
/// the compiler only emits it when the captured local and the lambda live in different
/// dynamic assemblies — so the test compiles two chained C# scripts, the same mechanism
/// .NET Interactive / Polyglot Notebooks use for their cells.
/// </summary>
public class CrossSubmissionCaptureTests
{
    [Fact]
    public async Task Solve_ConstraintCapturingLocalFromPreviousSubmission_ReturnsModel()
    {
        var options = ScriptOptions.Default
            .AddReferences(typeof(Z3Context).Assembly)
            .AddReferences(typeof(Microsoft.Z3.Context).Assembly)
            .AddReferences(typeof(Expression).Assembly)
            .AddImports("Z3.Linq");

        // Submission #0: declares the local that submission #1 will capture. Top-level
        // script locals become fields on the generated Submission#0 type.
        var state0 = await CSharpScript.RunAsync("int b = 5;", options);

        // Submission #1: the constraint lambda references b from submission #0, so the
        // tree's MemberExpression carries Submission#0's field while its constant root is
        // the Submission#1 closure. Solving walks that member pre-fix and crashes.
        var state1 = await state0.ContinueWithAsync<int>(@"
class Bag { public int I { get; set; } }
var sol = new Z3Context().NewTheorem<Bag>().Where(x => x.I == b).Solve();
return sol!.I;
", options);

        Assert.Equal(5, state1.ReturnValue);
    }
}
