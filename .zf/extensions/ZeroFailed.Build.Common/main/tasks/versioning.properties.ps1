# <copyright file="versioning.properties.ps1" company="Endjin Limited">
# Copyright (c) Endjin Limited. All rights reserved.
# </copyright>

# Synopsis: When true, the versioning tasks will be skipped.
$SkipVersion = [Convert]::ToBoolean((property ZF_BUILD_SKIP_VERSIONING $false))

# Synopsis: When true, the GitVersion tool will be used to determine the version number.
$UseGitVersion = [Convert]::ToBoolean((property ZF_BUILD_USE_GITVERSION $true))

# Synopsis: Path to the GitVersion configuration file. Default is a "GitVersion.yml" file alongside the running script.
$GitVersionConfigPath = property ZF_BUILD_GITVERSION_CONFIG_PATH ((Test-Path variable:/here) ? (Join-Path $here "GitVersion.yml") : "./GitVersion.yml")

# Synopsis: The version of the GitVersion tool to use.
$GitVersionToolVersion = property ZF_BUILD_GITVERSION_TOOL_VERSION "5.8.0"

# Synopsis: When defined, the value will used as an override of the GitVersion output. This can be useful when you want to force a particular version tag unrelated to the current branch etc. Not set by default.
$GitVersion = property ZF_BUILD_GITVERSION_OVERRIDE @{}