# <copyright file="Invoke-CommandWithRetry.Tests.ps1" company="Endjin Limited">
# Copyright (c) Endjin Limited. All rights reserved.
# </copyright>

BeforeAll {
    # sut
    . $PSCommandPath.Replace('.Tests.ps1','.ps1')

    # Ensure this internal function is available for mocking
    function _logRetry {}
}

Describe "Invoke-CommandWithRetry" {

    BeforeAll {
        Mock _logRetry {}
        Mock Write-Warning {}
        Mock Write-Host {}
    }
    
    Context "When the command does not error" {
        BeforeAll {
            $result = Invoke-CommandWithRetry { return $true }
        }

        It "should return the output" {
            $result | Should -Be $true
        }

        It "should not log a success after retry" {
            Should -Invoke _logRetry -Times 0 -Exactly -Scope Context
            Should -Invoke Write-Host -Times 0 -Exactly -Scope Context
        }
    }

    Context "When the command does error" {
        It "should bubble up the exception" {
            { Invoke-CommandWithRetry { throw "force retry" } -RetryDelay 0 } | Should -Throw "force retry"
            Should -Invoke Write-Warning -Times 1 -Exactly -Scope Context
        }

        It "should retry 5 times by default" {
            Should -Invoke _logRetry -Times 5 -Exactly -Scope Context
        }
    }

    Context "When the retry count is overridden" {
        It "should bubble up the exception" {
            { Invoke-CommandWithRetry { throw "force retry" } -RetryDelay 0 -RetryCount 10 } | Should -Throw "force retry"
            Should -Invoke Write-Warning -Times 1 -Exactly -Scope Context
        }

        It "should retry the specified amount of times" {
            Should -Invoke _logRetry -Times 10 -Exactly -Scope Context
        }
    }

    Context "When the command eventually passes" {
        BeforeAll {
            $global:failureCount = 0;
    
            $scriptBlock = {
                $global:failureCount = $global:failureCount + 1
                if ($global:failureCount -eq 3) {
                    return $true
                }
                else {
                    throw "force retry"
                }
            }
    
            $result = { Invoke-CommandWithRetry $scriptBlock -RetryDelay 0 -RetryCount 10 }
        }

        It "should not bubble the exception" {
            $result | Should -Not -Throw
            Should -Invoke Write-Warning -Times 0 -Exactly -Scope Context
        }

        It "should log attempting the retries" {
            Should -Invoke _logRetry -Times 2 -Exactly -Scope Context
        }

        It "should return the output" {
            $result | Should -Be $true
        }

        It "should log a success after retry" {
            Should -Invoke Write-Host -Times 1 -Exactly -Scope Context
        }
    }

    Context "When the command accesses outside variables" {

        It "should be able to reference it" {
            $outsideVariable = "was outside"
            $result = Invoke-CommandWithRetry { return $outsideVariable }

            $result | Should -Be "was outside"
        }
    }
}
