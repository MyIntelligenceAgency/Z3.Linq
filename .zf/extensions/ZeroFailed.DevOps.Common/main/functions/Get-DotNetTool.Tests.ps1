# <copyright file="Get-DotNetTool.Tests.ps1" company="Endjin Limited">
# Copyright (c) Endjin Limited. All rights reserved.
# </copyright>

BeforeAll {
    # sut
    . $PSCommandPath.Replace('.Tests.ps1','.ps1')
}

Describe 'Get-DotNetTool' {
    
    Context 'No global tools installed' {
        BeforeAll {
            Mock _runDotNetToolList { @'
Package Id      Version      Commands      Manifest
--------------------------------------------------- 
'@
}
        }

        It 'should execute successfully and return nothing' {
            $result = Get-DotNetTool -Name 'someglobal.tool' -Global

            Assert-MockCalled _runDotNetToolList -Exactly 1
            $result | Should -BeNullOrEmpty
        }
    }
    
}