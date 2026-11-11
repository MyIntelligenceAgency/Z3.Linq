# <copyright file="build.properties.ps1" company="Endjin Limited">
# Copyright (c) Endjin Limited. All rights reserved.
# </copyright>

# Synopsis: When true, the build process will clean intermediate & output directories before starting the build.
$CleanBuild = [Convert]::ToBoolean((property ZF_BUILD_CLEAN $false))

# Synopsis: The directory path where packages produced by the build will be placed.
$PackagesDir ??= property ZF_BUILD_PACKAGES_DIR '_packages'

# Synopsis: The directory path where code coverage files produced by the build will be placed.
$CoverageDir ??= property ZF_BUILD_COVERAGE_DIR '_codeCoverage'
$CoverageDir = [IO.Path]::IsPathRooted($CoverageDir) ? $CoverageDir : (Join-Path $here $CoverageDir)

# Synopsis: When true (and running on a CI/CD server), the build process will send phase completion messages to the agent.
$SendPhaseCompletionMessagesToCICDServer = [Convert]::ToBoolean((property ZF_BUILD_PHASE_COMPLETION_MESSAGES $true))
