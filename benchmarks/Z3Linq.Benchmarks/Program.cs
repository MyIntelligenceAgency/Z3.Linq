using BenchmarkDotNet.Running;

if (args.Length == 0)
{
    BenchmarkSwitcher.FromAssembly(typeof(Z3Linq.Benchmarks.TheoremPipelineBenchmarks).Assembly).RunAll();
}
else
{
    BenchmarkSwitcher.FromAssembly(typeof(Z3Linq.Benchmarks.TheoremPipelineBenchmarks).Assembly).Run(args);
}
