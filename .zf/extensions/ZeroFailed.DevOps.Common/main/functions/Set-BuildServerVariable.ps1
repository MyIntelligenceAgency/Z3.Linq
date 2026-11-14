# <copyright file="Set-BuildServerVariable.ps1" company="Endjin Limited">
# Copyright (c) Endjin Limited. All rights reserved.
# </copyright>

function Set-BuildServerVariable
{
    <#
        .SYNOPSIS
            Abstracts sending formatted log messages to build servers to set build variables.

        .DESCRIPTION
            Identifies the current build server using well-known environment variables and outputs correctly formatted log messages that will set variables within the build server context. Currently supports Azure Pipelines and GitHub Actions.

        .PARAMETER Name
            The name of the variable to set on the build server.

        .PARAMETER Value
            The value of the variable to set on the build server.

        .OUTPUTS
            System.Void

        .EXAMPLE
            ```powershell
            Set-BuildServerVariable -Name "MyVar" -Value "foo"
            ```
    #>
    [CmdletBinding()]
    [OutputType([System.Void])]
    param (
        [Parameter(Mandatory=$true)]
        [string] $Name,

        [Parameter(Mandatory=$true)]
        [AllowNull()]
        $Value
    )

    if ($env:TF_BUILD) {
        Write-Host "##vso[task.setvariable variable=$Name]$Value" -InformationAction Continue
    }
    elseif ($env:GITHUB_ACTIONS) {
        Write-Output "$Name=$Value" | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding utf8 -Append
    }
}
