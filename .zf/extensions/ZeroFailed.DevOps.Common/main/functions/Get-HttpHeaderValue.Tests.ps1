# <copyright file="Get-HttpHeaderValue.Tests.ps1" company="Endjin Limited">
# Copyright (c) Endjin Limited. All rights reserved.
# </copyright>

BeforeAll {
    # Dot source the function file
    . $PSScriptRoot/Get-HttpHeaderValue.ps1
    
    # Helper function to create mock headers collection
    function New-MockHeaders {
        param(
            [hashtable] $HeaderData = @{}
        )
        
        $headers = @()
        foreach ($entry in $HeaderData.GetEnumerator()) {
            $headerObj = New-Object PSObject -Property @{
                Key = $entry.Key
                Value = @($entry.Value)
            }
            $headers += $headerObj
        }
        return $headers
    }
    
    # Helper function to create mock headers with multiple values
    function New-MockHeadersWithMultipleValues {
        param(
            [string] $HeaderName,
            [string[]] $Values
        )
        
        $headerObj = New-Object PSObject -Property @{
            Key = $HeaderName
            Value = $Values
        }
        return @($headerObj)
    }
}

Describe "Get-HttpHeaderValue" {
    
    Context "When extracting integer headers" {
        It "should parse valid integer header successfully" {
            # Arrange
            $headers = New-MockHeaders @{ "Retry-After" = "30" }
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "Retry-After" -ExpectedType ([int]) -DefaultValue 0
            
            # Assert
            $result | Should -Be 30
            $result | Should -BeOfType ([int])
        }
        
        It "should return default value for non-integer header value" {
            # Arrange
            $headers = New-MockHeaders @{ "Retry-After" = "not-a-number" }
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "Retry-After" -ExpectedType ([int]) -DefaultValue -1
            
            # Assert
            $result | Should -Be -1
        }
        
        It "should parse zero as valid integer" {
            # Arrange
            $headers = New-MockHeaders @{ "X-RateLimit-Remaining" = "0" }
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "X-RateLimit-Remaining" -ExpectedType ([int]) -DefaultValue 10
            
            # Assert
            $result | Should -Be 0
            $result | Should -BeOfType ([int])
        }
        
        It "should parse negative integer values" {
            # Arrange
            $headers = New-MockHeaders @{ "Custom-Header" = "-15" }
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "Custom-Header" -ExpectedType ([int]) -DefaultValue 0
            
            # Assert
            $result | Should -Be -15
            $result | Should -BeOfType ([int])
        }
        
        It "should handle integer overflow gracefully" {
            # Arrange - Use a number larger than int32 max
            $headers = New-MockHeaders @{ "Large-Number" = "999999999999999999999" }
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "Large-Number" -ExpectedType ([int]) -DefaultValue 0
            
            # Assert
            $result | Should -Be 0  # Should return default due to overflow
        }
    }
    
    Context "When extracting long headers" {
        It "should parse valid long header successfully" {
            # Arrange
            $headers = New-MockHeaders @{ "X-RateLimit-Reset" = "132483916800000000" }
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "X-RateLimit-Reset" -ExpectedType ([long]) -DefaultValue 0
            
            # Assert
            $result | Should -Be 132483916800000000
            $result | Should -BeOfType ([long])
        }
        
        It "should return default value for invalid long value" {
            # Arrange
            $headers = New-MockHeaders @{ "X-RateLimit-Reset" = "invalid-filetime" }
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "X-RateLimit-Reset" -ExpectedType ([long]) -DefaultValue 0
            
            # Assert
            $result | Should -Be 0
        }
        
        It "should handle very large long values" {
            # Arrange
            $headers = New-MockHeaders @{ "Large-Long" = "9223372036854775807" }  # long.MaxValue
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "Large-Long" -ExpectedType ([long]) -DefaultValue 0
            
            # Assert
            $result | Should -Be 9223372036854775807
            $result | Should -BeOfType ([long])
        }
    }
    
    Context "When extracting double headers" {
        It "should parse valid double header successfully" {
            # Arrange
            $headers = New-MockHeaders @{ "Rate-Limit" = "1.5" }
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "Rate-Limit" -ExpectedType ([double]) -DefaultValue 0.0
            
            # Assert
            $result | Should -Be 1.5
            $result | Should -BeOfType ([double])
        }
        
        It "should parse integer as double" {
            # Arrange
            $headers = New-MockHeaders @{ "Rate-Limit" = "42" }
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "Rate-Limit" -ExpectedType ([double]) -DefaultValue 0.0
            
            # Assert
            $result | Should -Be 42.0
            $result | Should -BeOfType ([double])
        }
        
        It "should handle scientific notation" {
            # Arrange
            $headers = New-MockHeaders @{ "Scientific" = "1.23e-4" }
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "Scientific" -ExpectedType ([double]) -DefaultValue 0.0
            
            # Assert
            $result | Should -Be 0.000123
            $result | Should -BeOfType ([double])
        }
    }
    
    Context "When extracting string headers" {
        It "should return string header value as-is" {
            # Arrange
            $headers = New-MockHeaders @{ "Content-Type" = "application/json" }
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "Content-Type" -ExpectedType ([string]) -DefaultValue "text/plain"
            
            # Assert
            $result | Should -Be "application/json"
            $result | Should -BeOfType ([string])
        }
        
        It "should handle empty string header value" {
            # Arrange
            $headers = New-MockHeaders @{ "Empty-Header" = "" }
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "Empty-Header" -ExpectedType ([string]) -DefaultValue "default"
            
            # Assert
            $result | Should -Be "default"  # Empty string should return default
        }
        
        It "should handle whitespace-only string header value" {
            # Arrange
            $headers = New-MockHeaders @{ "Whitespace-Header" = "   " }
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "Whitespace-Header" -ExpectedType ([string]) -DefaultValue "default"
            
            # Assert
            $result | Should -Be "default"  # Whitespace-only should return default
        }
        
        It "should preserve leading and trailing spaces in valid string values" {
            # Arrange
            $headers = New-MockHeaders @{ "Spaced-Header" = "  valid content  " }
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "Spaced-Header" -ExpectedType ([string]) -DefaultValue "default"
            
            # Assert
            $result | Should -Be "  valid content  "
        }
    }
    
    Context "When extracting datetime headers" {
        It "should parse valid ISO datetime successfully" {
            # Arrange
            $headers = New-MockHeaders @{ "Last-Modified" = "2024-01-15T10:30:00Z" }
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "Last-Modified" -ExpectedType ([datetime]) -DefaultValue ([datetime]::MinValue)
            
            # Assert
            $result | Should -Be ([datetime]"2024-01-15T10:30:00Z")
            $result | Should -BeOfType ([datetime])
        }
        
        It "should return default for invalid datetime format" {
            # Arrange
            $headers = New-MockHeaders @{ "Last-Modified" = "not-a-date" }
            $defaultDate = [datetime]"2020-01-01T00:00:00Z"
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "Last-Modified" -ExpectedType ([datetime]) -DefaultValue $defaultDate
            
            # Assert
            $result | Should -Be $defaultDate
        }
        
        It "should parse different datetime formats" {
            # Arrange
            $testCases = @(
                "Mon, 15 Jan 2024 10:30:00 GMT",
                "2024-01-15 10:30:00",
                "1/15/2024 10:30:00 AM"
            )
            
            foreach ($dateString in $testCases) {
                $headers = New-MockHeaders @{ "Test-Date" = $dateString }
                $defaultDate = [datetime]::MinValue
                
                # Act
                $result = Get-HttpHeaderValue -Headers $headers -HeaderName "Test-Date" -ExpectedType ([datetime]) -DefaultValue $defaultDate
                
                # Assert - Should successfully parse or return default
                $result | Should -BeOfType ([datetime])
                # We don't assert exact value since datetime parsing is locale-dependent
            }
        }
    }
    
    Context "When handling case sensitivity" {
        It "should match header name case-insensitively" {
            # Arrange
            $headers = New-MockHeaders @{ "retry-after" = "30" }  # lowercase
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "Retry-After" -ExpectedType ([int]) -DefaultValue 0  # mixed case
            
            # Assert
            $result | Should -Be 30
        }
        
        It "should match uppercase header names" {
            # Arrange
            $headers = New-MockHeaders @{ "CONTENT-TYPE" = "application/json" }  # uppercase
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "content-type" -ExpectedType ([string]) -DefaultValue "text/plain"  # lowercase
            
            # Assert
            $result | Should -Be "application/json"
        }
        
        It "should match mixed case header names" {
            # Arrange
            $headers = New-MockHeaders @{ "X-RaTe-LiMiT-ReMaInInG" = "5" }  # mixed case
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "x-rate-limit-remaining" -ExpectedType ([int]) -DefaultValue 0  # lowercase
            
            # Assert
            $result | Should -Be 5
        }
    }
    
    Context "When handling multiple header values" {
        It "should use first value when multiple values exist" {
            # Arrange
            $headers = New-MockHeadersWithMultipleValues -HeaderName "Accept" -Values @("application/json", "text/html")
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "Accept" -ExpectedType ([string]) -DefaultValue "default"
            
            # Assert
            $result | Should -Be "application/json"  # First value
        }
        
        It "should parse first value for numeric types with multiple values" {
            # Arrange
            $headers = New-MockHeadersWithMultipleValues -HeaderName "Custom-Number" -Values @("10", "20", "30")
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "Custom-Number" -ExpectedType ([int]) -DefaultValue 0
            
            # Assert
            $result | Should -Be 10  # First value
        }
        
        It "should handle single value in array" {
            # Arrange
            $headers = New-MockHeadersWithMultipleValues -HeaderName "Single-Value" -Values @("42")
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "Single-Value" -ExpectedType ([int]) -DefaultValue 0
            
            # Assert
            $result | Should -Be 42
        }
    }
    
    Context "When handling missing or null headers" {
        It "should return default value when header is not found" {
            # Arrange
            $headers = New-MockHeaders @{ "Other-Header" = "value" }
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "Missing-Header" -ExpectedType ([string]) -DefaultValue "not-found"
            
            # Assert
            $result | Should -Be "not-found"
        }
        
        It "should return default value when headers collection is null" {
            # Arrange
            $headers = $null
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "Any-Header" -ExpectedType ([string]) -DefaultValue "default"
            
            # Assert
            $result | Should -Be "default"
        }
        
        It "should return default value when headers collection is empty" {
            # Arrange
            $headers = @()
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "Any-Header" -ExpectedType ([int]) -DefaultValue 42
            
            # Assert
            $result | Should -Be 42
        }
        
        It "should return default when header exists but has no values" {
            # Arrange
            $headerObj = New-Object PSObject -Property @{
                Key = "Empty-Header"
                Value = @()  # Empty value array
            }
            $headers = @($headerObj)
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "Empty-Header" -ExpectedType ([string]) -DefaultValue "default"
            
            # Assert
            $result | Should -Be "default"
        }
        
        It "should return default when header value is null" {
            # Arrange
            $headerObj = New-Object PSObject -Property @{
                Key = "Null-Header"
                Value = $null
            }
            $headers = @($headerObj)
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "Null-Header" -ExpectedType ([string]) -DefaultValue "default"
            
            # Assert
            $result | Should -Be "default"
        }
    }
    
    Context "When handling default values" {
        It "should return null as default value when specified" {
            # Arrange
            $headers = New-MockHeaders @{}
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "Missing" -ExpectedType ([string]) -DefaultValue $null
            
            # Assert
            $result | Should -BeNullOrEmpty
        }
        
        It "should return zero as default value for integers" {
            # Arrange
            $headers = New-MockHeaders @{ "Bad-Number" = "not-a-number" }
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "Bad-Number" -ExpectedType ([int]) -DefaultValue 0
            
            # Assert
            $result | Should -Be 0
        }
        
        It "should return custom default value" {
            # Arrange
            $headers = New-MockHeaders @{}
            $customDefault = "custom-default-value"
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "Missing" -ExpectedType ([string]) -DefaultValue $customDefault
            
            # Assert
            $result | Should -Be $customDefault
        }
        
        It "should handle complex default values" {
            # Arrange
            $headers = New-MockHeaders @{ "Bad-Date" = "invalid-date" }
            $defaultDate = [datetime]"2023-12-25T00:00:00Z"
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "Bad-Date" -ExpectedType ([datetime]) -DefaultValue $defaultDate
            
            # Assert
            $result | Should -Be $defaultDate
        }
    }
    
    Context "When handling edge cases and error conditions" {
        It "should handle headers with special characters in names" {
            # Arrange
            $headers = New-MockHeaders @{ "X-Custom_Header-2.0" = "special-value" }
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "X-Custom_Header-2.0" -ExpectedType ([string]) -DefaultValue "default"
            
            # Assert
            $result | Should -Be "special-value"
        }
        
        It "should handle very long header values" {
            # Arrange
            $longValue = "a" * 1000  # 1000 character string
            $headers = New-MockHeaders @{ "Long-Header" = $longValue }
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "Long-Header" -ExpectedType ([string]) -DefaultValue "default"
            
            # Assert
            $result | Should -Be $longValue
            $result.Length | Should -Be 1000
        }
        
        It "should handle headers with unicode characters" {
            # Arrange
            $unicodeValue = "Test-ÄÖÜ-日本語-🚀"
            $headers = New-MockHeaders @{ "Unicode-Header" = $unicodeValue }
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "Unicode-Header" -ExpectedType ([string]) -DefaultValue "default"
            
            # Assert
            $result | Should -Be $unicodeValue
        }
        
        It "should handle malformed headers objects gracefully" {
            # Arrange - Create header object without required properties
            $malformedHeader = New-Object PSObject -Property @{
                NotKey = "Wrong-Header"
                NotValue = @("wrong-value")
            }
            $headers = @($malformedHeader)
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "Any-Header" -ExpectedType ([string]) -DefaultValue "default"
            
            # Assert
            $result | Should -Be "default"
        }
        
        It "should handle mixed valid and invalid headers in collection" {
            # Arrange
            $validHeader = New-Object PSObject -Property @{
                Key = "Valid-Header"
                Value = @("valid-value")
            }
            $invalidHeader = New-Object PSObject -Property @{
                Key = $null
                Value = @("invalid")
            }
            $headers = @($validHeader, $invalidHeader)
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "Valid-Header" -ExpectedType ([string]) -DefaultValue "default"
            
            # Assert
            $result | Should -Be "valid-value"
        }
    }
    
    Context "When using verbose logging" {
        It "should log successful header parsing" {
            # Arrange
            $headers = New-MockHeaders @{ "Test-Header" = "123" }
            Mock Write-Verbose {}
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "Test-Header" -ExpectedType ([int]) -DefaultValue 0 -Verbose
            
            # Assert
            $result | Should -Be 123
            Should -Invoke Write-Verbose -Times 1 -ParameterFilter { $Message -like "*Successfully parsed header 'Test-Header' as int: 123*" }
        }
        
        It "should log parsing failure" {
            # Arrange
            $headers = New-MockHeaders @{ "Bad-Number" = "not-a-number" }
            Mock Write-Verbose {}
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "Bad-Number" -ExpectedType ([int]) -DefaultValue 0 -Verbose
            
            # Assert
            $result | Should -Be 0
            Should -Invoke Write-Verbose -Times 1 -ParameterFilter { $Message -like "*Could not parse Bad-Number header: not-a-number*" }
        }
        
        It "should log missing header" {
            # Arrange
            $headers = New-MockHeaders @{"Other-Header" = "value"}  # Non-empty headers collection
            Mock Write-Verbose {}
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "Missing-Header" -ExpectedType ([string]) -DefaultValue "default" -Verbose
            
            # Assert
            $result | Should -Be "default"
            Should -Invoke Write-Verbose -Times 1 -ParameterFilter { $Message -like "*Header 'Missing-Header' not found*" }
        }
        
        It "should log null headers collection" {
            # Arrange
            $headers = $null
            Mock Write-Verbose {}
            
            # Act
            $result = Get-HttpHeaderValue -Headers $headers -HeaderName "Any-Header" -ExpectedType ([string]) -DefaultValue "default" -Verbose
            
            # Assert
            $result | Should -Be "default"
            Should -Invoke Write-Verbose -Times 1 -ParameterFilter { $Message -like "*Headers collection is null or empty*" }
        }
    }
}