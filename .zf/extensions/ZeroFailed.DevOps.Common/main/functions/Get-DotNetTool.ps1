# <copyright file="Get-DotNetTool.ps1" company="Endjin Limited">
# Copyright (c) Endjin Limited. All rights reserved.
# </copyright>

function Get-DotNetTool
{
    <#
        .SYNOPSIS
            Simple wrapper to check whether a given .NET tool is already installed.

        .DESCRIPTION
            Simple wrapper to check whether a given .NET tool is already installed.

        .PARAMETER Name
            The name of the .NET tool to check

        .PARAMETER Version
            The version of the .NET tool to check for. When unspecified any version found will cause this function to return true.

        .PARAMETER Global
            When specified, the tool's installation status is checked in the global scope (i.e. for the current user).

        .PARAMETER Local
            When specified, the tool's installation status is checked in the local scope (i.e. for the current project's .NET tool manifest).

        .PARAMETER ToolPath
            When specified, the tool's installation status is checked in the specified directory.

        .OUTPUTS
            System.Collections.Hashtable

        .EXAMPLE
            ```powershell
            PS> Get-DotNetTool -Global -Name gitversion.tool

            name            version commands
            ----            ------- --------
            gitversion.tool 5.8.0   dotnet-gitversion
            ```

        .EXAMPLE
            ```powershell
            PS:> Get-DotNetTool -ToolPath ./tools -Name gitversion.tool -Version 5.6.6
            ```
    #>
    [CmdletBinding(DefaultParameterSetName="global")]
    [OutputType([Hashtable])]
    param (
        [Parameter(Mandatory=$true)]
        [string] $Name,

        [Parameter()]
        [string] $Version,
        
        [Parameter(ParameterSetName="global")]
        [switch] $Global,

        [Parameter(ParameterSetName="local")]
        [switch] $Local,

        [Parameter(ParameterSetName="toolpath")]
        [string] $ToolPath
    )

    switch ($PSCmdlet.ParameterSetName) {
        "global" { $scopeArg = @("--global") }
        "local" { $scopeArg = @("--local") }
        "toolpath" { $scopeArg = @("--tool-path", $ToolPath) }
    }

    # Parse the output from the dotnet cli to check whether the tool is already installed:
    #  - Skip first 2 'header' lines of output
    #  - The output is not tab delimited, so convert the whitespace between columns to a comma
    #  - Convert the now CSV-formatted output into an object
    #  - Match the specified tool name
    $existingInstall = _runDotNetToolList $scopeArg |
                            Select-Object -Skip 2 |
                            ForEach-Object { $_ -ireplace "\s+","," } |
                            ConvertFrom-Csv -Header name,version,commands |
                            Where-Object { $_.name -eq $Name }

    return $existingInstall
}

# Internal wrapper function to enable mocking in tests
function _runDotNetToolList {
    [CmdletBinding()]
    param (
        $scopeArg
    )

    & dotnet tool list @scopeArg
}
