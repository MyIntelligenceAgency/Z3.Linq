# <copyright file="cicd-server.properties.ps1" company="Endjin Limited">
# Copyright (c) Endjin Limited. All rights reserved.
# </copyright>


# Synopsis: Will be set to True when a supported CI/CD server platform is detected. ***NOTE**: Considered a read only property.*
$IsRunningOnCICDServer = $false

# Synopsis: Will be set to True when running in Azure DevOps (YAML pipeline or Classic release pipeline). ***NOTE**: Considered a read only property.*
$IsAzureDevOps = $false

# Synopsis: Will be set to True when running in an Azure DevOps Classic release pipeline. ***NOTE**: Considered a read only property.*
$IsAzureDevOpsRelease = $false

# Synopsis: Will be set to True when running in a GitHub Actions workflow. ***NOTE**: Considered a read only property.*
$IsGitHubActions = $false

# Synopsis: When true, no DevOps agent detection will be attempted. Default is false.
$SkipDetectCICDServer = [Convert]::ToBoolean((property ZF_SKIP_DETECT_CICD_SERVER $false))
