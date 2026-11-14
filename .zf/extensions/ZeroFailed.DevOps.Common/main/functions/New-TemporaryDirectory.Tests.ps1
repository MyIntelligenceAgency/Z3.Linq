# <copyright file="New-TemporaryDirectory.Tests.ps1" company="Endjin Limited">
# Copyright (c) Endjin Limited. All rights reserved.
# </copyright>

BeforeAll {
    # sut
    . $PSCommandPath.Replace('.Tests.ps1','.ps1')
}

Describe 'New-TemporaryDirectory' {
    
    BeforeAll {
        $result = New-TemporaryDirectory
    }

    AfterAll {
        Remove-Item -Path $result.FullName -Recurse -Force
    }

    It 'should execute successfully and return the path to the temporary directory' {
        $result | Should -Not -BeNullOrEmpty
        $result | Should -BeOfType [System.IO.DirectoryInfo]
        $result | Should -Exist
    }
}