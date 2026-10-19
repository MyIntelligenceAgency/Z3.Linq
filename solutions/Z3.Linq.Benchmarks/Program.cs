// Benchmarks BenchmarkDotNet pour l'API publique Z3.Linq (EPIC .NET 11, CoursIA#18770).
// Baseline net9.0 : dotnet run -c Release -- --filter *
// Charges deterministes (Z3 monothread, parametres par defaut) : memes contraintes
// a chaque iteration, aucune I/O, aucun reseau.
using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
