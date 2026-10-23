using BenchmarkDotNet.Attributes;
using Z3.Linq;

namespace Z3Linq.Benchmarks;

/// <summary>
/// Micro-bancs de <see cref="Rational"/> (numérateur/dénominateur longs, forme réduite) :
/// constante « numerics » de l'API publique. <see cref="Rational"/> n'expose aucune
/// arithmétique ration-ration — c'est un porteur de constante exacte pour l'arbre
/// d'expression — donc les bancs couvrent les quatre chemins réellement publics :
/// construction réduite, égalité structurelle, comparaison contre double (chemin des
/// prédicats <c>where</c>) et <see cref="object.ToString"/> (chemin d'émission des
/// littéraux vers Z3).
/// </summary>
[MemoryDiagnoser]
[Config(typeof(BenchConfig))]
public class RationalBenchmarks
{
    private static readonly Rational Third = Rational.Of(1, 3);
    private static readonly Rational TwoSixths = Rational.Of(2, 6);
    private static readonly Rational Seventh = Rational.Of(2, 7);

    /// <summary>Construction réduite depuis un numérateur/dénominateur non réduits (chemin GCD).</summary>
    [Benchmark(Description = "Rational.Of (reduction)")]
    public Rational ReduceConstruction() => Rational.Of(1_000_003 * 7, 1_000_003 * 3);

    /// <summary>
    /// Égalité structurelle sur paires réduites égales (entrées brutes différentes) et inégales —
    /// 32 itérations pour que le coût dépasse le bruit de l'horloge.
    /// </summary>
    [Benchmark(Description = "Rational Equals (reduced pairs)")]
    public bool EqualsReducedPairs()
    {
        bool acc = true;
        for (int i = 0; i < 32; i++)
        {
            acc &= Rational.Of(2 * (i + 1), 6 * (i + 1)).Equals(Rational.Of(i + 1, 3 * (i + 1)));
            acc &= Third.Equals(Seventh);
        }

        return acc;
    }

    /// <summary>Comparaisons contre double : le chemin qu'empruntent les prédicats Where réels.</summary>
    [Benchmark(Description = "Rational compare vs double")]
    public bool CompareVsDouble() => Third > 0.3333 && Third < 0.3334 && Seventh <= 0.2858 && Third >= 0.3333;

    /// <summary>Émission du littéral « num/den » : chemin historique des constantes vers MkReal.</summary>
    [Benchmark(Description = "Rational.ToString (literal emission)")]
    public string ToStringLiteral()
    {
        var a = Third.ToString();
        var b = TwoSixths.ToString();
        return a.Length + b.Length > 0 ? a : b;
    }
}
