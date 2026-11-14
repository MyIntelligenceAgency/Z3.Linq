# <copyright file="versioning.tasks.ps1" company="Endjin Limited">
# Copyright (c) Endjin Limited. All rights reserved.
# </copyright>

. $PSScriptRoot/versioning.properties.ps1

# Synopsis: Run GitVersion tool
task GitVersion -If {$UseGitVersion} -After VersionCore {
    
    if ($GitVersion.Keys.Count -gt 0) {
        Write-Build Cyan "Version details overridden by environment variable:`n$($GitVersion | ConvertTo-Json)"
    }
    else {
        if (!(Test-Path -PathType Container .git)) {
            Write-Warning "No git repository found, versioning will be skipped"
        }

        Install-DotNetTool -Name "GitVersion.Tool" -Version $GitVersionToolVersion
        Write-Build Cyan "GitVersion Config: $GitVersionConfigPath"

        $gitVersionOutputJson = ''
        exec { dotnet-gitversion /output json /nofetch /config $GitVersionConfigPath } |
            Tee-Object -Variable gitVersionOutputJson |
            ForEach-Object { Write-Build White $_ }
    
        $env:GitVersionOutput = $gitVersionOutputJson
        $script:GitVersion = $gitVersionOutputJson | ConvertFrom-Json -AsHashtable
    
        # Set the native GitVersion output as environment variables and build server variables
        foreach ($var in $script:GitVersion.Keys) {
            Set-Item -Path "env:GITVERSION_$var" -Value $GitVersion[$var]
            Set-BuildServerVariable -Name "GitVersion.$var" -Value $GitVersion[$var]
        }
    }
}
