# <copyright file="build.tasks.ps1" company="Endjin Limited">
# Copyright (c) Endjin Limited. All rights reserved.
# </copyright>

. $PSScriptRoot/build.properties.ps1

# Synopsis: Ensures the packages output directory exists
task EnsurePackagesDir -After InitCore {
    # Ensure we resolve a relative path for $PackagesDir now, rather than letting it be resolved by downstream tooling
    # For example, 'dotnet pack' will resolve it relative to a given project which is not what we want.
    $script:PackagesDir = [IO.Path]::IsPathRooted($PackagesDir) ? $PackagesDir : (Join-Path $here $PackagesDir)

    if (!(Test-Path $PackagesDir)) {
        New-Item -Path $PackagesDir -ItemType Directory | Out-Null
    }
}
