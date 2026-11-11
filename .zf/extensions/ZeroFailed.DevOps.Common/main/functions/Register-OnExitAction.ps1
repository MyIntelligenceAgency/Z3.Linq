# <copyright file="Register-OnExitAction.ps1" company="Endjin Limited">
# Copyright (c) Endjin Limited. All rights reserved.
# </copyright>

function Register-OnExitAction
{
    <#
        .SYNOPSIS
            A helper function for consumers to register a PowerShell script block run as part of the InvokeBuild `Exit-Build` extensibility point.

        .DESCRIPTION
            A helper function for consumers to register a PowerShell script block run as part of the InvokeBuild `Exit-Build` extensibility point.

        .PARAMETER Action
            A script block containing the functionality that should run as part of the InvokeBuild `Exit-Build` extensibility point.

        .OUTPUTS
            System.Void

        .EXAMPLE
            ```powershell
            Register-OnExitAction -Action { Write-Build White 'Doing some clean-up' }
            ```

        .EXAMPLE
            ```powershell
            Register-OnExitAction -Action { Invoke-Build -File my.tasks.ps1 -Task MyCleanupTask }
            ```
    #>
    [CmdletBinding()]
    [OutputType([System.Void])]
    param (
        [Parameter(Mandatory)]
        [scriptblock] $Action
    )

    # Do not register the action if we're already running in the context of one of these actions
    if (!(Test-Path variable:/__RunningInExitAction) -or !$__RunningInExitAction)
    {
        $script:OnExitActions.Add($Action)
    }
}
