# <copyright file="New-TemporaryDirectory.ps1" company="Endjin Limited">
# Copyright (c) Endjin Limited. All rights reserved.
# </copyright>

function New-TemporaryDirectory {
    <#
        .SYNOPSIS
            Creates a new temporary directory with a unique name.

        .DESCRIPTION
            This function creates a new directory in the system's temporary folder with a randomly generated GUID as its name.
            This ensures that the directory name is unique and avoids potential naming conflicts. The function returns the DirectoryInfo object for the newly created directory.

        .OUTPUTS
            System.IO.DirectoryInfo

        .EXAMPLE
            ```powershell
            $tempDir = New-TemporaryDirectory
            PS> $tempDir.FullName
            C:\Users\username\AppData\Local\Temp\a1b2c3d4-e5f6-7890-1234-567890abcdef
            ```

        .EXAMPLE
            ```powershell
            $tempDir = New-TemporaryDirectory
            try {
                # Use temporary directory for operations
                # ...
            }
            finally {
                # Clean up when done
                Remove-Item -Path $tempDir -Recurse -Force
            }
            ```
    #>
    [CmdletBinding()]
    [OutputType([System.IO.DirectoryInfo])]
    param ()

    $parent = [System.IO.Path]::GetTempPath()
    [string] $name = [System.Guid]::NewGuid()
    New-Item -ItemType Directory -Path (Join-Path $parent $name)
}
