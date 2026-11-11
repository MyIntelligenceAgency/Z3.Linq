# <copyright file="common.tasks.ps1" company="Endjin Limited">
# Copyright (c) Endjin Limited. All rights reserved.
# </copyright>

. $PSScriptRoot/common.properties.ps1

# Synopsis: Checks that the GitHub CLI is installed and installs it if not.
task EnsureGitHubCli -If { !$SkipEnsureGitHubCli } -After InitCore {
    if (!(Get-Command gh -CommandType Application -ErrorAction SilentlyContinue)) {
        throw "The GitHub CLI is required - please install as per: https://github.com/cli/cli#installation"
    }

    # Test whether GitHub CLI is logged-in
    & gh auth status | Out-Null
    if ($LASTEXITCODE -eq 1) {
        throw "You must be logged-in to GitHub CLI to run this process. Please run 'gh auth login' to login and then re-run the build."
    }
}

# Synopsis: Installs and imports the specified PowerShell modules, retrying on failure. ***NOTE**: PowerShell repositories will be trusted by default.*
task setupModules -If { $null -ne $RequiredPowerShellModules -and $RequiredPowerShellModules -ne @{} } -After InitCore {
    # Retry to mitigate transient PowerShell Gallery failures. Modules that are already installed
    # are skipped, so a retry only installs those that are still missing.
    Invoke-CommandWithRetry -RetryCount $PowerShellModuleInstallRetryCount -RetryDelay $PowerShellModuleInstallRetryDelay -Command {
        Install-PSResource -RequiredResource $RequiredPowerShellModules -Scope CurrentUser -TrustRepository -ErrorAction Stop | Out-Null
    }
    $RequiredPowerShellModules.Keys | ForEach-Object { Import-Module $_ }
}


# Support the special InvokeBuild entry & exit actions in a way that allows
# extensions to register their own actions and have them run using this
# built-in InvokeBuild functionality.  This is primarily intended to support
# compensation scenarios in the event of terminating errors or the equivalent
# of a 'finally' block.

# These actions can be scriptblocks that perform some specific processing or they can
# run a nested build, thus allowing one or more Tasks to be run in this way.
# However, to avoid recursion errors we must provide a mechanism for ZF to know when
# it is already running in this context. When executing the actions we set a
# well-known variable that the 'Register-On*Action' helper functions look for
# when deciding whether to do the actual registration.
Enter-Build {
    if ($OnEnterActions.Count -gt 0) {
        Write-Build Green "Found $($OnEnterActions.Count) registered 'Enter-Build' action(s)"
        for ($i=0; $i -lt $OnEnterActions.Count; $i++) {
            Write-Build White "Running action $i"
            & {
                # The invoked scriptblock will inherit this value, so this registration process
                # and the action itself can be aware of when it is running in this context.
                $__RunningInEnterAction = $true
                # Run action, no need to override the default 'ErrorAction' handling for the OnEnter actions
                Invoke-Command -ScriptBlock $OnEnterActions[$i]
            }
        }
    }
}
Exit-Build {
    if ($OnExitActions.Count -gt 0) {
        Write-Build Green "Found $($OnExitActions.Count) registered 'Exit-Build' action(s)"
        for ($i=0; $i -lt $OnExitActions.Count; $i++) {
            Write-Build White "Running action $i"
            & {
                # The invoked scriptblock will inherit this value, so this registration process
                # and the action itself can be aware of when it is running in this context.
                $__RunningInExitAction = $true
                # Run the action, but ensure that any remaining actions are run even if the current one fails
                Invoke-Command -ScriptBlock $OnExitActions[$i] -ErrorAction Continue
            }
        }
    }
}
