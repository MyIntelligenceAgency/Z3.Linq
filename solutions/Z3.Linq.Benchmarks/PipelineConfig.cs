using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace Z3.Linq.Benchmarks;

/// <summary>
/// Configuration des bancs étendus : même protocole 5 warmup / 15 itérations et
/// invocation unique que <c>SolveBenchmarks</c>. La référence est InProcess. Quand
/// DOTNET_MULTI_RUNTIME=1, aucun job InProcess n'est ajouté : les monikers passés à
/// BDN définissent les jobs en processus enfants. Pour mesurer .NET 11 sans moniker
/// BDN, l'exécutable net8 peut aussi être lancé directement sous un DOTNET_ROOT
/// isolé avec DOTNET_ROLL_FORWARD=Major ; la ligne Runtime du journal fait foi.
/// </summary>
public class BenchConfig : ManualConfig
{
    public BenchConfig()
    {
        if (System.Environment.GetEnvironmentVariable("DOTNET_MULTI_RUNTIME") != "1")
        {
            AddJob(Job.ShortRun
                .WithToolchain(InProcessEmitToolchain.Instance)
                .WithInvocationCount(1)
                .WithUnrollFactor(1)
                .WithWarmupCount(5)
                .WithIterationCount(15)
                .WithId("InProcess"));
        }

        AddLogger(BenchmarkDotNet.Loggers.ConsoleLogger.Default);
        AddColumnProvider(BenchmarkDotNet.Columns.DefaultColumnProviders.Instance);
    }
}
