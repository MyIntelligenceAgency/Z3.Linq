# <copyright file="Register-OnEnterAction.ps1" company="Endjin Limited">
# Copyright (c) Endjin Limited. All rights reserved.
# </copyright>

function Register-OnEnterAction
{
    <#
        .SYNOPSIS
            A helper function for consumers to register a PowerShell script block run as part of the InvokeBuild `Enter-Build` extensibility point.

        .DESCRIPTION
            A helper function for consumers to register a PowerShell script block run as part of the InvokeBuild `Enter-Build` extensibility point.

        .PARAMETER Action
            A script block containing the functionality that should run as part of the InvokeBuild `Enter-Build` extensibility point.

        .OUTPUTS
            System.Void

        .EXAMPLE
            ```powershell
            Register-OnEnterAction -Action { Write-Build White 'Doing something' }
            ```

        .EXAMPLE
            ```powershell
            Register-OnEnterAction -Action { Invoke-Build -File my.tasks.ps1 -Task MyStartupTask }
            ```
    #>
    [CmdletBinding()]
    [OutputType([System.Void])]
    param (
        [Parameter(Mandatory)]
        [scriptblock] $Action
    )

    # Do not register the action if we're already running in the context of one of these actions
    if (!(Test-Path variable:/__RunningInEnterAction) -or !$__RunningInEnterAction)
    {
        $script:OnEnterActions.Add($Action)
    }
}
