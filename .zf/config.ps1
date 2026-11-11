<#
This build process uses the 'ZeroFailed.Build.DotNet' extension to provide the features
needed when building .NET solutions.
#>

$zerofailedExtensions = @(
    @{
        # References the extension from its GitHub repository. If it is not already installed, the
        # referenced commit is downloaded.
        #
        # PINNED to 7fa91e302616accf3c65a2b95fff79f37fcc1ebb (2026-11-02, last commit before the
        # ZeroFailed.Build.Common drift of 2026-11-10, whose "ZF v2 versioning updates" require the
        # ZeroFailed v2 module while build.ps1 pins module 1.0.7 - see CoursIA #19237).
        #
        # The extension's own dependency, ZeroFailed.Build.Common, is declared without a GitRef in its
        # manifest and therefore resolves to 'main' with no override possible from this file (the 1.0.7
        # loader matches the installed folder name exactly and has no lockfile support). That dependency
        # is pinned by VENDORING the last compatible snapshots under .zf/extensions/.../main/ (the
        # folder name the nested resolution looks up), which short-circuits the download:
        #   .zf/extensions/ZeroFailed.Build.Common/main    = zerofailed/ZeroFailed.Build.Common@57ff8211cd75
        #   .zf/extensions/ZeroFailed.DevOps.Common/main   = zerofailed/ZeroFailed.DevOps.Common@c4e5d102de
        # Unpin (delete the vendored folders, restore GitRef = "main") only after moving to the
        # ZeroFailed v2 module, which supports .zf/extensions.lock.json.
        Name = "ZeroFailed.Build.DotNet"
        GitRepository = "https://github.com/zerofailed/ZeroFailed.Build.DotNet"
        GitRef = "7fa91e302616accf3c65a2b95fff79f37fcc1ebb"
    }
)

# Load the tasks and process
. ZeroFailed.tasks -ZfPath $here/.zf

#
# Build process control options
#
$SkipInit = $false
$SkipVersion = $false
$SkipBuild = $false
$CleanBuild = $Clean
# Z3.Linq.Tests runs on Microsoft.Testing.Platform via the MSTest SDK.
$SkipTest = $false
$SkipTestReport = $false
$SkipAnalysis = $false
$SkipPackage = $false

# Releases are ON HOLD. The build uses Microsoft.Z3 5.1.0 for its Linux and arm64 native
# binaries, but that version is not on nuget.org, so a published Z3.Linq would declare a
# dependency no consumer can restore (NU1102). Lift this once Microsoft.Z3 5.x is published -
# see https://github.com/endjin/Z3.Linq/issues/60 and https://github.com/Z3Prover/z3/issues/10711
$SkipPublish = $true

#
# Build process configuration
#
$SolutionToBuild = (Resolve-Path (Join-Path $here "./solutions/Z3.Linq.slnx")).Path
$ProjectsToPublish = @()
$NugetPublishSource = property ZF_NUGET_PUBLISH_SOURCE "$here/_local-nuget-feed"
$IncludeAssembliesInCodeCoverage = "Z3.Linq*"
$ExcludeAssembliesInCodeCoverage = "Z3.Linq*.Tests*"

task . FullBuild

#
# Build Process Extensibility Points - uncomment and implement as required
#

# task RunFirst {}
# task PreInit {}
# task PostInit {}
# task PreVersion {}
# task PostVersion {}
task PreBuild EnsureZ3Package
# task PostBuild {}
# task PreTest {}
# task PostTest {}
# task PreTestReport {}
# task PostTestReport {}
# task PreAnalysis {}
# task PostAnalysis {}
# task PrePackage {}
# task PostPackage {}
# task PrePublish {}
# task PostPublish {}
# task RunLast {}

#
# Microsoft.Z3 is not available on nuget.org (see scripts/Install-Z3Package.ps1), so the
# package has to be in the local folder feed before anything restores. PreBuild is the last
# extensibility point that runs ahead of the RestorePackages task.
#
task EnsureZ3Package {
    & (Join-Path $here "scripts/Install-Z3Package.ps1")
}
