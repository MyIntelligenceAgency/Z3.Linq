# <copyright file="Register-OnEnterAction.Tests.ps1" company="Endjin Limited">
# Copyright (c) Endjin Limited. All rights reserved.
# </copyright>

BeforeAll {
    # sut
    . $PSCommandPath.Replace('.Tests.ps1','.ps1')
}

Describe 'Register-OnEnterAction' {

    BeforeEach {
        $script:OnEnterActions = [System.Collections.Generic.List[scriptblock]]::new()
        Remove-Variable -Name __RunningInEnterAction -Scope Script -ErrorAction SilentlyContinue
    }

    Context 'When the guard variable does not exist' {

        It 'should add the action to the list' {
            $action = { Write-Host 'action' }
            Register-OnEnterAction -Action $action
            $script:OnEnterActions.Count | Should -Be 1
            $script:OnEnterActions[0] | Should -Be $action
        }

        It 'can register multiple actions' {
            Register-OnEnterAction -Action { Write-Host 'action 1' }
            Register-OnEnterAction -Action { Write-Host 'action 2' }
            $script:OnEnterActions.Count | Should -Be 2
        }
    }

    Context 'When the guard variable is false' {

        BeforeEach {
            $script:__RunningInEnterAction = $false
        }

        It 'should add the action to the list' {
            $action = { Write-Host 'action' }
            Register-OnEnterAction -Action $action
            $script:OnEnterActions.Count | Should -Be 1
            $script:OnEnterActions[0] | Should -Be $action
        }
    }

    Context 'When running inside an Enter-Build action' {

        BeforeEach {
            $script:__RunningInEnterAction = $true
        }

        It 'should not add the action to the list' {
            Register-OnEnterAction -Action { Write-Host 'action' }
            $script:OnEnterActions.Count | Should -Be 0
        }
    }
}
