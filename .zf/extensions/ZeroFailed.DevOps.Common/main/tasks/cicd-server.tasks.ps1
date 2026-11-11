# <copyright file="cicd-server.tasks.ps1" company="Endjin Limited">
# Copyright (c) Endjin Limited. All rights reserved.
# </copyright>

. $PSScriptRoot/cicd-server.properties.ps1

# Synopsis: Identifies which, if any, CI/CD platform is running the current process
task DetectCICDServer -If { !$SkipDetectCICDServer } -After InitCore {
    if ($env:TF_BUILD) {
        $script:IsRunningOnCICDServer = $true
        $script:IsAzureDevOps = $true
        Write-Build White "Azure Pipelines detected"
    }
    elseif ($env:RELEASE_RELEASENAME) {
        $script:IsRunningOnCICDServer = $true
        $script:IsAzureDevOps = $true
        $script:IsAzureDevOpsRelease = $true
        Write-Build White "Azure Release Pipeline detected"
    }
    elseif ($env:GITHUB_ACTIONS) {
        $script:IsRunningOnCICDServer = $true
        $script:IsGitHubActions = $true
        Write-Build White "GitHub Actions detected"
    }
}
