# <copyright file="cicd-server.tasks.ps1" company="Endjin Limited">
# Copyright (c) Endjin Limited. All rights reserved.
# </copyright>

. $PSScriptRoot/cicd-server.properties.ps1

# Synopsis: Inform the DevOps agent of the current version number. NOTE: AzureDevOps only.
task SetCICDServerBuildNumber -If {$IsRunningOnCICDServer -and !$SkipSetCICDServerBuildNumber} -After Version {
    if ($IsAzureDevOps) {
        Write-Host "Setting Azure Pipelines build number: $($GitVersion[$GitVersionComponentForBuildNumber])"
        Write-Host "##vso[build.updatebuildnumber]$($GitVersion[$GitVersionComponentForBuildNumber])"
    }
    elseif ($IsGitHubActions) {
        Write-Warning "Setting GitHub Actions workflow run number is not supported."
    }
}

# Synopsis: Sends a message to the build server to indicate that the build phase has completed successfully. NOTE: GitHub Actions only.
task sendCompileOkMessageToCICDServer -If {$SendPhaseCompletionMessagesToCICDServer -and $IsRunningOnCICDServer} -After Build DetectCICDServer,{
    if ($IsGitHubActions) {
        Write-Host ("::notice title=Compile OK::Compile phase completed successfully")
    }
    elseif ($IsAzureDevOps) {
        # TODO: Determine whether any of the available options make the message suitably visible in the build summary
    }
}

# Synopsis: Sends a message to the build server to indicate that the analysis phase has completed successfully. NOTE: GitHub Actions only.
task sendAnalysisOkMessageToCICDServer -If {$SendPhaseCompletionMessagesToCICDServer -and $IsRunningOnCICDServer} -After Analysis DetectCICDServer,{
    if ($IsGitHubActions) {
        Write-Host ("::notice title=Analysis OK::Analysis phase completed successfully")
    }
    elseif ($IsAzureDevOps) {
        # TODO: Determine whether any of the available options make the message suitably visible in the build summary
    }
}

# Synopsis: Sends a message to the build server to indicate that the test phase has completed successfully. NOTE: GitHub Actions only.
task sendTestOkMessageToCICDServer -If {$SendPhaseCompletionMessagesToCICDServer -and $IsRunningOnCICDServer} -After Test DetectCICDServer,{
    if ($IsGitHubActions) {
        Write-Host ("::notice title=Test OK::Test phase completed successfully")
    }
    elseif ($IsAzureDevOps) {
        # TODO: Determine whether any of the available options make the message suitably visible in the build summary
    }
}

# Synopsis: Sends a message to the build server to indicate that the package phase has completed successfully. NOTE: GitHub Actions only.
task sendPackageOkMessageToCICDServer -If {$SendPhaseCompletionMessagesToCICDServer -and $IsRunningOnCICDServer} -After Package DetectCICDServer,{
    if ($IsGitHubActions) {
        Write-Host ("::notice title=Package OK::Package phase completed successfully")
    }
    elseif ($IsAzureDevOps) {
        # TODO: Determine whether any of the available options make the message suitably visible in the build summary
    }
}

# Synopsis: Sends a message to the build server to indicate that the publish phase has completed successfully. NOTE: GitHub Actions only.
task sendPublishOkMessageToCICDServer -If {$SendPhaseCompletionMessagesToCICDServer -and $IsRunningOnCICDServer} -After Publish DetectCICDServer,{
    if ($IsGitHubActions) {
        Write-Host ("::notice title=Publish OK::Publish phase completed successfully")
    }
    elseif ($IsAzureDevOps) {
        # TODO: Determine whether any of the available options make the message suitably visible in the build summary
    }
}
