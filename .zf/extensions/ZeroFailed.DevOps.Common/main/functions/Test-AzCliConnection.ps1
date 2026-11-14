# <copyright file="Test-AzCliConnection.ps1" company="Endjin Limited">
# Copyright (c) Endjin Limited. All rights reserved.
# </copyright>

function Test-AzCliConnection
{
    <#
        .SYNOPSIS
            Checks whether the current process is logged-in to the azure-cli with a valid access token.

        .DESCRIPTION
            Returns true when a valid azure-cli access token is found, otherwise returns false.

        .OUTPUTS
            System.Boolean

        .EXAMPLE
            ```powershell
            if (Test-AzCliConnection) { & az storage list } else { Write-Error "Please run 'az login'" }
            ```
    #>
    [CmdletBinding()]
    [OutputType([Boolean])]
    param (
    )

    if ($null -eq $(Get-Command az -ErrorAction Ignore)) {
        throw "The azure-cli is not installed."
    }

    $currentTokenExpiry = $(az account get-access-token --query "expiresOn" -o tsv 2>$null)
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($currentTokenExpiry) -or ([DateTime]$currentTokenExpiry) -le [DateTime]::Now)
    {
        return $false
    }

    return $true
}
