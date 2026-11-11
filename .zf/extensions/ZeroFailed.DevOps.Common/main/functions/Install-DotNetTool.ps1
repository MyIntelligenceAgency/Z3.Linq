# <copyright file="Install-DotNetTool.ps1" company="Endjin Limited">
# Copyright (c) Endjin Limited. All rights reserved.
# </copyright>

function Install-DotNetTool
{
    <#
        .SYNOPSIS
            Simple wrapper to install a .NET global tool if it is not already installed.

        .DESCRIPTION
            Simple wrapper to install a .NET Global Tool if it is not already installed.  Any existing installed version will be uninstalled before installing the required version.

        .PARAMETER Name
            The name of the .NET global tool to install.

        .PARAMETER Version
            The version of the global tool to install. When unspecified the latest version will attempted to used, however, this does not work for all scenarios (e.g. pre-release, when not using a fully-featured NuGet feed as a source).

        .PARAMETER AdditionalArgs
            An array of arbitrary command-line arguments supported by 'dotnet tool install' command.

        .PARAMETER Global
            When specified, the tool is installed in the global scope (i.e. for the current user).

        .PARAMETER Local
            When specified, the tool is installed in the local scope (i.e. for the current project's .NET tool manifest).

        .PARAMETER ToolPath
            When specified, the tool is installed in the specified directory.

        .OUTPUTS
            System.String

        .EXAMPLE
            ```powershell
            Install-DotNetTool -Global -Name dotnet-gitversion
            ```

        .EXAMPLE
            ```powershell
            Install-DotNetTool -ToolPath ./tools -Name dotnet-gitversion -Version 5.6.6
            ```
    #>
    [CmdletBinding(DefaultParameterSetName="global")]
    [OutputType([String])]
    param (
        [Parameter(Mandatory=$true, Position=0)]
        [string] $Name,

        [Parameter()]
        [string] $Version,

        [Parameter()]
        [string[]] $AdditionalArgs = @(),
        
        [Parameter(ParameterSetName="global")]
        [switch] $Global,

        [Parameter(ParameterSetName="local")]
        [switch] $Local,

        [Parameter(ParameterSetName="toolpath")]
        [string] $ToolPath
    )

    # Remove AdditionArgs from the bound parameters so we can splat the Test-DotNetTool call
    $PSBoundParameters.Remove("AdditionalArgs") | Out-Null
    $alreadyInstalled = Get-DotNetTool @PSBoundParameters

    # Translate the current parameter set to the relevant 'dotnet tool' cli argument
    switch ($PSCmdlet.ParameterSetName) {
        "global" { $scopeArg = @("--global") }
        "local" { $scopeArg = @("--local") }
        "toolpath" { $scopeArg = @("--tool-path", $ToolPath) }
    }

    # Uninstall an existing version of the tool if it's the wrong version
    if ($alreadyInstalled -and $Version -and $alreadyInstalled.version -ne $Version) {
        Write-Host "Uninstalling existing version: $($alreadyInstalled.version)"
        _runDotNetToolUninstall @scopeArg $Name
        if ($LASTEXITCODE -ne 0) {
            throw "'dotnet tool uninstall' returned a non-zero exit code ($LASTEXITCODE) - check previous output"
        }
        # Ensure the required version is installed below
        $alreadyInstalled = $false
    }

    # Install the tool, if necessary
    if (!$alreadyInstalled) {
        if ($Version) {
            $AdditionalArgs += @(
                "--version"
                $Version
            )
        }

        # Remove any 'scope' references that may have been unnecessarily supplied via AdditionalArgs
        $AdditionalArgs = $AdditionalArgs | Where-Object { $_ -notin @("-g","--global","-l","--local","-t","--tool-path") }

        Write-Verbose "cmdline: & dotnet tool install $($scopeArg -join " ") $Name $AdditionalArgs"
        _runDotNetToolInstall @scopeArg $Name @AdditionalArgs
        if ($LASTEXITCODE -ne 0) {
            throw "'dotnet tool install' returned a non-zero exit code ($LASTEXITCODE) - check previous output"
        }
    }

    # Ensure .NET global tools are available via the PATH environment variable, if only for the current process
    if ($PSCmdlet.ParameterSetName -eq "global") {
        $toolsPath = Join-Path $HOME ".dotnet/tools"
        
        if ($toolsPath -notin ($env:PATH -split [IO.Path]::PathSeparator)) {
            $env:PATH = "{0}{1}{2}" -f $env:PATH, [IO.Path]::PathSeparator, $toolsPath
        }
    }
}

# Internal wrapper functions to enable mocking in tests
function _runDotNetToolInstall {
    [CmdletBinding()]
    param (
        [Parameter(ValueFromRemainingArguments)]
        $cmdArgs
    )

    & dotnet tool install @cmdArgs
}
function _runDotNetToolUninstall {
    [CmdletBinding()]
    param (
        [Parameter(ValueFromRemainingArguments)]
        $cmdArgs
    )

    & dotnet tool uninstall @cmdArgs
}
