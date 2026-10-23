using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace Z3Linq.Benchmarks;

/// <summary>
/// Configuration commune des bancs Z3.Linq : InProcessEmitToolchain (pas de processus
/// enfant par banc — le solveur Z3 est une dépendance native déjà chargée) et Job.ShortRun
/// (5 warmup / 15 itérations), alignée sur la configuration des baselines de la famille
/// #18770 (MetaGeneticSharp, Automata, SemanticFleet) pour la comparabilité des mesures
/// .NET 10/11 à venir.
/// </summary>
public class BenchConfig : ManualConfig
{
    public BenchConfig()
    {
        AddJob(Job.ShortRun
            .WithToolchain(InProcessEmitToolchain.Instance)
            .WithId("InProcess"));

        AddLogger(BenchmarkDotNet.Loggers.ConsoleLogger.Default);
        AddColumnProvider(BenchmarkDotNet.Columns.DefaultColumnProviders.Instance);
    }
}
