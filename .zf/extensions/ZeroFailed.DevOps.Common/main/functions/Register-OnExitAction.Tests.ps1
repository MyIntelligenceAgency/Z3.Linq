# <copyright file="Register-OnExitAction.Tests.ps1" company="Endjin Limited">
# Copyright (c) Endjin Limited. All rights reserved.
# </copyright>

BeforeAll {
    # sut
    . $PSCommandPath.Replace('.Tests.ps1','.ps1')
}

Describe 'Register-OnExitAction' {

    BeforeEach {
        $script:OnExitActions = [System.Collections.Generic.List[scriptblock]]::new()
        Remove-Variable -Name __RunningInExitAction -Scope Script -ErrorAction SilentlyContinue
    }

    Context 'When the guard variable does not exist' {

        It 'should add the action to the list' {
            $action = { Write-Host 'action' }
            Register-OnExitAction -Action $action
            $script:OnExitActions.Count | Should -Be 1
            $script:OnExitActions[0] | Should -Be $action
        }

        It 'can register multiple actions' {
            Register-OnExitAction -Action { Write-Host 'action 1' }
            Register-OnExitAction -Action { Write-Host 'action 2' }
            $script:OnExitActions.Count | Should -Be 2
        }
    }

    Context 'When the guard variable is false' {

        BeforeEach {
            $script:__RunningInExitAction = $false
        }

        It 'should add the action to the list' {
            $action = { Write-Host 'action' }
            Register-OnExitAction -Action $action
            $script:OnExitActions.Count | Should -Be 1
            $script:OnExitActions[0] | Should -Be $action
        }
    }

    Context 'When running inside an Exit-Build action' {

        BeforeEach {
            $script:__RunningInExitAction = $true
        }

        It 'should not add the action to the list' {
            Register-OnExitAction -Action { Write-Host 'action' }
            $script:OnExitActions.Count | Should -Be 0
        }
    }
}
