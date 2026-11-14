# <copyright file="Set-BuildServerVariable.Tests.ps1" company="Endjin Limited">
# Copyright (c) Endjin Limited. All rights reserved.
# </copyright>

BeforeAll {
    # sut
    . $PSCommandPath.Replace('.Tests.ps1','.ps1')
}

Describe 'Set-BuildServerVariable' {
    
    Context "Running in Azure Pipelines" {
        BeforeAll {
            $env:TF_BUILD = "true"
            Mock Write-Host {}
        }
        AfterAll {
            Remove-Item -Path Env:TF_BUILD
        }

        It 'should emit the required Azure Pipelines command message' {
            $result = Set-BuildServerVariable -Name "foo" -Value "bar"
            $result | Should -Invoke Write-Host -ParameterFilter { $Object -eq "##vso[task.setvariable variable=foo]bar" }
        }
    }
    
    Context "Running GitHub Actions" {
        BeforeAll {
            # Avoid conflicts when running these tests in GHA
            $isRunningInGHA = ![string]::IsNullOrEmpty($env:GITHUB_ACTIONS)
            if (!$isRunningInGHA) {
                $env:GITHUB_ACTIONS = "true"
            }
            else {
                $savedGitHubOutput = $env:GITHUB_OUTPUT
            }
            $env:GITHUB_OUTPUT = 'TestDrive:/github_output.txt'
            Mock Write-Output {}
        }
        AfterAll {
            if (!$isRunningInGHA) {
                Remove-Item -Path Env:GITHUB_ACTIONS
            }
            else {
                $env:GITHUB_OUTPUT = $savedGitHubOutput
            }
        }

        It 'should emit the required GitHub Actions logging command' {
            $result = Set-BuildServerVariable -Name "foo" -Value "bar"
            $result | Should -Invoke Write-Output -ParameterFilter { $InputObject -eq "foo=bar" }
        }
    }

    Context "Not running on a CI/CD agent" {

        It 'should not do anything' {
            $result = Set-BuildServerVariable -Name "foo" -Value "bar"
            $result | Should -BeNullOrEmpty
        }
    }
}