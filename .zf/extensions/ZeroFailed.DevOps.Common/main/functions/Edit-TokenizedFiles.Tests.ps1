# <copyright file="Edit-TokenizedFiles.Tests.ps1" company="Endjin Limited">
# Copyright (c) Endjin Limited. All rights reserved.
# </copyright>

BeforeAll {
    $here = Split-Path -Parent $PSCommandPath
    $sut = (Split-Path -Leaf $PSCommandPath) -replace ".Tests"
    
    . "$here\$sut"
    
    Mock Write-Host {}
    Mock Write-Verbose {}

    $defaultTokenRegexPattern = "\#\{.*\}\#"
}

Describe "Edit-TokenizedFiles Tests" {

    Context "Single File: Using the default TokenRegexPattern" {

        BeforeAll {
            $testJsonConfig = @"
{
    "settingA": "#{SETTING_A}#",
    "settingB": "B",
    "settingC": "#{SETTING_A}#__#{SETTING_B}#__#{SETTING_C}#"
}    
"@
            $testJsonFile = "TestDrive:/test-config.json"
            Set-Content -Path $testJsonFile -Value $testJsonConfig
    
            $tokenValues = @{
                SETTING_A = "foo"
                SETTING_B = "bar"
                SETTING_C = "too_far"
            }
        }
        
        BeforeEach {
            Edit-TokenizedFiles -FilesToProcess @($testJsonFile) `
                                -TokenValuePairs $tokenValues
        }

        It "should parse the tokens in the target file correctly" {
            # Uses logging calls to verify execution behaviour

            # Messages for the 3 token replacements and the file save operation
            Should -Invoke Write-Host -Exactly 4

            # verify processing the correct number of tokens
            Should -Invoke Write-Verbose -ParameterFilter { $Message -eq "Checking for SETTING_A" } -Exactly 1
            Should -Invoke Write-Verbose -ParameterFilter { $Message -eq "Checking for SETTING_B" } -Exactly 1
            Should -Invoke Write-Verbose -ParameterFilter { $Message -eq "Checking for SETTING_C" } -Exactly 1
            
            # verify expected tokens were processed
            Should -Invoke Write-Host -ParameterFilter { $Object.StartsWith("Patching 'SETTING_A'") } -Exactly 1
            Should -Invoke Write-Host -ParameterFilter { $Object.StartsWith("Patching 'SETTING_B'") } -Exactly 1
            Should -Invoke Write-Host -ParameterFilter { $Object.StartsWith("Patching 'SETTING_C'") } -Exactly 1

            # verify all tokens were found
            Should -Invoke Write-Verbose -ParameterFilter { $Message -eq "Token not found" } -Exactly 0
        }

        It "should replace the required token" {
            (Get-Content -Raw -Path $testJsonFile) -match $defaultTokenRegexPattern | Should -Be $false
        }

        It "should replace the required token with the correct value" {
            (Get-Content -Raw -Path $testJsonFile | ConvertFrom-Json).settingA | Should -Be "foo"
        }

        It "should handle multiple tokens referenced on the same line correctly" {
            (Get-Content -Raw -Path $testJsonFile | ConvertFrom-Json).settingC | Should -Be "foo__bar__too_far"
        }
    }

    Context "Multiple Files: Using the default TokenRegexPattern" {

        BeforeAll {

            $testJsonConfig = @"
{
    "settingA": "#{SETTING_A}#",
    "settingB": "B",
    "settingC": "#{SETTING_A}#__#{SETTING_B}#"
}    
"@
            $testJsonFile = "TestDrive:/test-config.json"
            Set-Content -Path $testJsonFile -Value $testJsonConfig

            $testPsd1Config = @"
@{
    settingA = '#{SETTING_A}#'
    settingB = 'B'
    settingC = '#{SETTING_A}#__#{SETTING_B}#'
}
"@
            $testPsd1File = "TestDrive:/test-config.psd1"
            Set-Content -Path $testPsd1File -Value $testPsd1Config
    
            $tokenValues = @{
                SETTING_A = "foo"
                SETTING_B = "bar"
            }
        }
        
        BeforeEach {
            Edit-TokenizedFiles -FilesToProcess @($testJsonFile,$testPsd1File) `
                                -TokenValuePairs $tokenValues
        }

        It "should parse the tokens in the target file correctly" {
            # Uses logging calls to verify execution behaviour

            # Messages for the 2 token replacements and the file save operation
            Should -Invoke Write-Host -Exactly 6

            # verify processing the correct number of tokens
            Should -Invoke Write-Verbose -ParameterFilter { $Message -eq "Checking for SETTING_A" } -Exactly 1
            Should -Invoke Write-Verbose -ParameterFilter { $Message -eq "Checking for SETTING_B" } -Exactly 1
            
            # verify expected tokens were processed
            Should -Invoke Write-Host -ParameterFilter { $Object.StartsWith("Patching 'SETTING_A' in 'TestDrive:/test-config.json'") } -Exactly 1
            Should -Invoke Write-Host -ParameterFilter { $Object.StartsWith("Patching 'SETTING_B' in 'TestDrive:/test-config.json'") } -Exactly 1
            Should -Invoke Write-Host -ParameterFilter { $Object.StartsWith("Patching 'SETTING_A' in 'TestDrive:/test-config.psd1'") } -Exactly 1
            Should -Invoke Write-Host -ParameterFilter { $Object.StartsWith("Patching 'SETTING_B' in 'TestDrive:/test-config.psd1'") } -Exactly 1

            # verify all tokens were found
            Should -Invoke Write-Verbose -ParameterFilter { $Message -eq "Token not found" } -Exactly 0
        }

        It "should replace the required token" {
            (Get-Content -Raw -Path $testJsonFile) -match $defaultTokenRegexPattern | Should -Be $false
            (Get-Content -Raw -Path $testPsd1File) -match $defaultTokenRegexPattern | Should -Be $false
        }

        It "should replace the required token with the correct value" {
            (Get-Content -Raw -Path $testJsonFile | ConvertFrom-Json).settingA | Should -Be "foo"
            (Import-PowerShellDataFile -Path $testPsd1File).settingA | Should -Be "foo"
        }

        It "should handle multiple tokens referenced on the same line correctly" {
            (Get-Content -Raw -Path $testJsonFile | ConvertFrom-Json).settingC | Should -Be "foo__bar"
            (Import-PowerShellDataFile -Path $testPsd1File).settingC | Should -Be "foo__bar"
        }
    }

    Context 'Escape character edge-case: .NET Regex substitution sequences' {

        BeforeAll {
            $testJsonConfig = @"
{
    "settingA": "#{SETTING_A}#",
    "settingB": "#{SETTING_B}#",
    "settingC": "#{SETTING_A}#__#{SETTING_B}#__#{SETTING_C}#"
}    
"@
            $testJsonFile = "TestDrive:/test-config.json"
            Set-Content -Path $testJsonFile -Value $testJsonConfig
    
            $tokenValues = @{
                SETTING_A = "foo"
                SETTING_B = 'pa$&ss$1word'
                SETTING_C = "too_far"
            }
        }

        BeforeEach {
            Edit-TokenizedFiles -FilesToProcess @($testJsonFile) `
                                -TokenValuePairs $tokenValues
        }

        It 'should render replacement strings containing .NET Regex substitution sequences correctly' {
            (Get-Content -Raw -Path $testJsonFile | ConvertFrom-Json).settingB | Should -Be 'pa$&ss$1word'
        }

         It "should handle multiple tokens referenced on the same line correctly" {
            (Get-Content -Raw -Path $testJsonFile | ConvertFrom-Json).settingC | Should -Be 'foo__pa$&ss$1word__too_far'
        }
    }
}