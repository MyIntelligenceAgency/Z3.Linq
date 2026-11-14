# <copyright file="Resolve-Value.Tests.ps1" company="Endjin Limited">
# Copyright (c) Endjin Limited. All rights reserved.
# </copyright>

BeforeAll {
    # sut
    . $PSCommandPath.Replace('.Tests.ps1','.ps1')
}

Describe 'Resolve-Value' {
    
    Context 'When passed a static value as a parameter' {
        
        It 'should return the original value' {
            Resolve-Value -Value 'StaticValue' | Should -Be 'StaticValue'
        }
    }

    Context 'When passed a static value via the pipeline' {
        
        It 'should return the original value' {
            'StaticValue' | Resolve-Value | Should -Be 'StaticValue'
        }
    }

    Context 'When passed a dynamic value as a parameter' {
        
        It 'should return the resolved value' {
            Resolve-Value -Value { 2 + 2 } | Should -Be 4
        }
    }

    Context 'When passed a dynamic value via the pipeline' {
        
        It 'should return the resolved value' {
            { 2 + 2 } | Resolve-Value | Should -Be 4
        }
    }

    Context 'When passed an empty string static value' {

        It 'should not error and return the original value' {
            { "" | Resolve-Value } | Should -Not -Throw
            "" | Resolve-Value | Should -BeNullOrEmpty
        }
    }

    Context 'When passed a null static value' {

        It 'should not error and return the original value' {
            { $null | Resolve-Value } | Should -Not -Throw
            $null | Resolve-Value | Should -BeNullOrEmpty
        }
    }

    Context 'When a dynamic value returns an empty string' {

        It 'should not error and return the resolved value' {
            { { "" } | Resolve-Value } | Should -Not -Throw
            { "" } | Resolve-Value | Should -BeNullOrEmpty
        }
    }

    Context 'When a dynamic value returns null' {

        It 'should not error and return the resolved value' {
            { { $null } | Resolve-Value } | Should -Not -Throw
            { $null } | Resolve-Value | Should -BeNullOrEmpty
        }
    }
    
}