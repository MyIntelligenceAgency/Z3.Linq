# <copyright file="Install-DotNetTool.Tests.ps1" company="Endjin Limited">
# Copyright (c) Endjin Limited. All rights reserved.
# </copyright>

BeforeAll {
    # sut
    . $PSCommandPath.Replace('.Tests.ps1','.ps1')

    # Make external functions available for mocking
    function Get-DotNetTool {}
}

Describe 'Install-DotNetTool' {

    Context 'Already installed' {
        BeforeAll {
            Mock Get-DotNetTool {
                return [pscustomobject]@{
                    name = 'someglobal.tool'
                    version = '1.0.0'
                    commands = 'dotnet-someglobal.tool'
                }
            }
            Mock _runDotNetToolInstall {}
            Mock _runDotNetToolUninstall {}
        }

        It 'should not make any changes' {
            $result = Install-DotNetTool -Name 'someglobal.tool' -Global -Version '1.0.0'
            $result | Should -Invoke _runDotNetToolUninstall -Exactly 0
            $result | Should -Invoke _runDotNetToolInstall -Exactly 0
            $result | Should -Be $null
        }
    }

    Context 'Out-dated version' {
        BeforeAll {
            Mock Get-DotNetTool {
                return [pscustomobject]@{
                    name = 'someglobal.tool'
                    version = '0.0.9'
                    commands = 'dotnet-someglobal.tool'
                }
            }
            Mock _runDotNetToolInstall {}
            Mock _runDotNetToolUninstall {}
            Mock Write-Host {}
            $LASTEXITCODE = 0
        }

        It 'should update to the specified version' {
            $result = Install-DotNetTool -Name 'someglobal.tool' -Global -Version '1.0.0'
            $result | Should -Invoke _runDotNetToolUninstall -Exactly 1
            $result | Should -Invoke _runDotNetToolInstall -Exactly 1
            $result | Should -Be $null
        }
    }

    Context 'Not installed' {
        BeforeAll {
            Mock Get-DotNetTool {}
            Mock _runDotNetToolInstall {}
            Mock _runDotNetToolUninstall {}
            $LASTEXITCODE = 0
        }

        It 'should install the specified version' {
            $result = Install-DotNetTool -Name 'someglobal.tool' -Global -Version '1.0.0'
            $result | Should -Invoke _runDotNetToolUninstall -Exactly 0
            $result | Should -Invoke _runDotNetToolInstall -Exactly 1
            $result | Should -Be $null
        }
    }
}