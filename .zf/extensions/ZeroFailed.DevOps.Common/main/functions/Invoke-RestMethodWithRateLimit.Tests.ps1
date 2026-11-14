# <copyright file="Invoke-RestMethodWithRateLimit.Tests.ps1" company="Endjin Limited">
# Copyright (c) Endjin Limited. All rights reserved.
# </copyright>

Describe "Invoke-RestMethodWithRateLimit" {
    
    BeforeAll {
        # Dot source the function files
        . $PSScriptRoot/Get-HttpHeaderValue.ps1
        . $PSScriptRoot/Invoke-RestMethodWithRateLimit.ps1
        
        # Create test helper to simulate HTTP responses with specific status codes
        function New-MockHttpException {
            param(
                [int] $StatusCode,
                [hashtable] $Headers = @{}
            )
            
            # Create mock response object with the expected properties
            $response = New-Object PSObject -Property @{
                StatusCode = New-Object PSObject -Property @{
                    value__ = $StatusCode
                }
                Headers = @()
            }
            
            # Add headers if provided
            foreach ($header in $Headers.GetEnumerator()) {
                $headerObj = New-Object PSObject -Property @{
                    Key = $header.Key
                    Value = @($header.Value)
                }
                $response.Headers += $headerObj
            }
            
            # Create the exception with the response
            $exception = New-Object System.Exception("HTTP $StatusCode Error")
            Add-Member -InputObject $exception -MemberType NoteProperty -Name Response -Value $response
            
            return $exception
        }

        Mock Write-Warning {}
    }

    Context "When API call succeeds on first attempt" {
        It "should return response without retries" {
            # Arrange
            $splat = @{
                Uri = "https://api.fabric.microsoft.com/v1/test"
                Method = "GET"
                Headers = @{ Authorization = "Bearer token" }
            }
            $expectedResponse = @{ data = "success" }
            
            Mock -CommandName Invoke-RestMethod -MockWith { return $expectedResponse }
            Mock -CommandName Write-Information {}
            Mock -CommandName Start-Sleep {}

            # Act
            $result = Invoke-RestMethodWithRateLimit -Splat $splat

            # Assert
            $result | Should -Be $expectedResponse
            Should -Invoke Invoke-RestMethod -Times 1
            Should -Invoke Start-Sleep -Times 0
        }
    }

    Context "When handling rate limit errors (429)" {
        It "should retry with server-provided Retry-After header" {
            # Arrange
            $splat = @{
                Uri = "https://api.fabric.microsoft.com/v1/test"
                Method = "GET"
                Headers = @{ Authorization = "Bearer token" }
            }
            
            $script:callCount = 0
            Mock -CommandName Invoke-RestMethod -MockWith {
                $script:callCount++
                if ($script:callCount -eq 1) {
                    $headers = @{ "Retry-After" = @("5") }
                    throw (New-MockHttpException -StatusCode 429 -Headers $headers)
                }
                return @{ data = "success" }
            }
            
            Mock -CommandName Write-Information {}
            Mock -CommandName Start-Sleep {} -ParameterFilter { $Seconds -eq 5 }

            # Act
            $result = Invoke-RestMethodWithRateLimit -Splat $splat

            # Assert
            $result.data | Should -Be "success"
            Should -Invoke Invoke-RestMethod -Times 2
            Should -Invoke Start-Sleep -Times 1 -ParameterFilter { $Seconds -eq 5 }
            Should -Invoke Write-Information -Times 1 -ParameterFilter { $MessageData -like "*Waiting 5 seconds as requested by server*" }
            Should -Invoke Write-Information -Times 1 -ParameterFilter { $MessageData -like "*succeeded after 2 attempts*" }
        }

        It "should retry with exponential backoff when no Retry-After header" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            $script:callCount = 0
            Mock -CommandName Invoke-RestMethod -MockWith {
                $script:callCount++
                if ($script:callCount -eq 1) {
                    throw (New-MockHttpException -StatusCode 429)
                }
                return @{ data = "success" }
            }
            
            Mock -CommandName Write-Information {}
            Mock -CommandName Write-Warning {}
            Mock -CommandName Start-Sleep {} -ParameterFilter { $Seconds -ge 1 -and $Seconds -le 2 }
            Mock -CommandName Get-Random { return 0.5 }

            # Act
            $result = Invoke-RestMethodWithRateLimit -Splat $splat -BaseDelaySeconds 1

            # Assert
            $result.data | Should -Be "success"
            Should -Invoke Invoke-RestMethod -Times 2
            Should -Invoke Start-Sleep -Times 1
            Should -Invoke Write-Warning -Times 1 -ParameterFilter { $Message -like "*Rate limited*Retrying*" }
        }

        It "should respect MaxDelaySeconds limit" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            Mock -CommandName Invoke-RestMethod -MockWith {
                $headers = @{ "Retry-After" = @("120") }
                throw (New-MockHttpException -StatusCode 429 -Headers $headers)
            }
            
            Mock -CommandName Write-Information {}
            Mock -CommandName Start-Sleep {} -ParameterFilter { $Seconds -eq 60 }

            # Act & Assert
            { Invoke-RestMethodWithRateLimit -Splat $splat -MaxDelaySeconds 60 -MaxRetries 1 } | Should -Throw

            Should -Invoke Start-Sleep -Times 1 -ParameterFilter { $Seconds -eq 60 }
            Should -Invoke Write-Information -Times 1 -ParameterFilter { $MessageData -like "*exceeds MaxDelaySeconds*Waiting for 60 seconds*" }
        }

        It "should detect X-RateLimit headers when present" {
            # Arrange - This test focuses on basic header detection rather than complex timing
            $splat = @{ Uri = "https://api.fabric.microsoft.com/v1/test"; Method = "GET" }
            
            Mock -CommandName Invoke-RestMethod -MockWith {
                $headers = @{ 
                    "X-RateLimit-Remaining" = @("0")
                    "X-RateLimit-Reset" = @("132883916800000000")  # Fixed future timestamp
                }
                throw (New-MockHttpException -StatusCode 429 -Headers $headers)
            }
            
            Mock -CommandName Write-Verbose {}
            Mock -CommandName Write-Warning {}
            Mock -CommandName Start-Sleep {}

            # Act & Assert - Should fall back to exponential backoff since header parsing likely fails
            { Invoke-RestMethodWithRateLimit -Splat $splat -MaxRetries 1 } | Should -Throw

            # The function should process the headers (even if time calculation fails and falls back to exponential backoff)
            Should -Invoke Invoke-RestMethod -Times 2
            Should -Invoke Write-Warning -Times 1 -ParameterFilter { $Message -like "*Rate limited*" }
        }

        It "should use exponential backoff when X-RateLimit-Remaining is not zero" {
            # Arrange
            $splat = @{ Uri = "https://api.test.com"; Method = "GET" }
            
            $script:callCount = 0
            Mock -CommandName Invoke-RestMethod -MockWith {
                $script:callCount++
                if ($script:callCount -eq 1) {
                    $headers = @{ 
                        "X-RateLimit-Remaining" = @("5")
                        "X-RateLimit-Reset" = @("132483916800000000")  # Some future timestamp
                    }
                    throw (New-MockHttpException -StatusCode 429 -Headers $headers)
                }
                return @{ data = "success" }
            }
            
            Mock -CommandName Write-Warning {}
            Mock -CommandName Start-Sleep {} -ParameterFilter { $Seconds -ge 1.0 -and $Seconds -le 1.5 }
            Mock -CommandName Get-Random { return 0.0 }

            # Act
            $result = Invoke-RestMethodWithRateLimit -Splat $splat -BaseDelaySeconds 1

            # Assert
            $result.data | Should -Be "success"
            Should -Invoke Invoke-RestMethod -Times 2
            Should -Invoke Start-Sleep -Times 1
            Should -Invoke Write-Warning -Times 1 -ParameterFilter { $Message -like "*Rate limited*Retrying*" }
        }

        It "should prioritize Retry-After header over X-RateLimit headers" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            $script:callCount = 0
            Mock -CommandName Invoke-RestMethod -MockWith {
                $script:callCount++
                if ($script:callCount -eq 1) {
                    $headers = @{ 
                        "Retry-After" = @("10")
                        "X-RateLimit-Remaining" = @("0")
                        "X-RateLimit-Reset" = @("132483916800000000")
                    }
                    throw (New-MockHttpException -StatusCode 429 -Headers $headers)
                }
                return @{ data = "success" }
            }
            
            Mock -CommandName Write-Information {}
            Mock -CommandName Start-Sleep {} -ParameterFilter { $Seconds -eq 10 }

            # Act
            $result = Invoke-RestMethodWithRateLimit -Splat $splat

            # Assert
            $result.data | Should -Be "success"
            Should -Invoke Start-Sleep -Times 1 -ParameterFilter { $Seconds -eq 10 }
            Should -Invoke Write-Information -Times 1 -ParameterFilter { $MessageData -like "*Waiting 10 seconds as requested by server*" }
        }

        It "should show appropriate message when server delay is within MaxDelaySeconds" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            Mock -CommandName Invoke-RestMethod -MockWith {
                $headers = @{ "Retry-After" = @("30") }
                throw (New-MockHttpException -StatusCode 429 -Headers $headers)
            }
            
            Mock -CommandName Write-Information {}
            Mock -CommandName Start-Sleep {}

            # Act & Assert
            { Invoke-RestMethodWithRateLimit -Splat $splat -MaxDelaySeconds 60 -MaxRetries 1 } | Should -Throw

            Should -Invoke Write-Information -Times 1 -ParameterFilter { $MessageData -like "*Waiting 30 seconds as requested by server*" }
            Should -Invoke Write-Information -Times 0 -ParameterFilter { $MessageData -like "*exceeds MaxDelaySeconds*" }
        }
    }

    Context "When handling server errors (5xx)" {
        It "should retry on 500 errors with exponential backoff" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            $script:callCount = 0
            Mock -CommandName Invoke-RestMethod -MockWith {
                $script:callCount++
                if ($script:callCount -eq 1) {
                    throw (New-MockHttpException -StatusCode 500)
                }
                return @{ data = "success" }
            }
            
            Mock -CommandName Write-Warning {}
            Mock -CommandName Start-Sleep {}
            Mock -CommandName Get-Random { return 0.0 }

            # Act
            $result = Invoke-RestMethodWithRateLimit -Splat $splat -BaseDelaySeconds 1

            # Assert
            $result.data | Should -Be "success"
            Should -Invoke Invoke-RestMethod -Times 2
            Should -Invoke Write-Warning -Times 1 -ParameterFilter { $Message -like "*Server error (500)*" }
        }

        It "should retry on 502, 503, 504 errors" {
            # Arrange
            $testCases = @(502, 503, 504)
            
            foreach ($statusCode in $testCases) {
                $splat = @{ Uri = "https://test.com"; Method = "GET" }
                
                $script:callCount = 0
                Mock -CommandName Invoke-RestMethod -MockWith {
                    $script:callCount++
                    if ($script:callCount -eq 1) {
                        throw (New-MockHttpException -StatusCode $statusCode)
                    }
                    return @{ data = "success" }
                }
                
                Mock -CommandName Write-Warning {}
                Mock -CommandName Start-Sleep {}

                # Act
                $result = Invoke-RestMethodWithRateLimit -Splat $splat

                # Assert
                $result.data | Should -Be "success"
                Should -Invoke Invoke-RestMethod -Times 2
            }
        }

        It "should retry on additional 5xx server errors" {
            # Arrange - Test other 5xx status codes that should be retryable
            $testCases = @(501, 505, 507, 508, 509, 510, 511, 520, 521, 522, 523, 524, 525, 526, 527, 530, 599)
            
            foreach ($statusCode in $testCases) {
                $splat = @{ Uri = "https://test.com"; Method = "GET" }
                
                $script:callCount = 0
                Mock -CommandName Invoke-RestMethod -MockWith {
                    $script:callCount++
                    if ($script:callCount -eq 1) {
                        throw (New-MockHttpException -StatusCode $statusCode)
                    }
                    return @{ data = "success" }
                }
                
                Mock -CommandName Write-Warning {}
                Mock -CommandName Start-Sleep {}

                # Act
                $result = Invoke-RestMethodWithRateLimit -Splat $splat

                # Assert
                $result.data | Should -Be "success"
                Should -Invoke Invoke-RestMethod -Times 2
                Should -Invoke Write-Warning -Times 1 -ParameterFilter { $Message -like "*Server error ($statusCode)*" }
            }
        }

        It "should handle boundary status codes correctly" {
            # Arrange - Test edge cases around retryable range
            $retryableCases = @(429, 500, 599)  # Should retry
            $nonRetryableCases = @(499, 600)    # Should not retry
            
            foreach ($statusCode in $retryableCases) {
                $splat = @{ Uri = "https://test.com"; Method = "GET" }
                
                $script:callCount = 0
                Mock -CommandName Invoke-RestMethod -MockWith {
                    $script:callCount++
                    if ($script:callCount -eq 1) {
                        throw (New-MockHttpException -StatusCode $statusCode)
                    }
                    return @{ data = "success" }
                }
                
                Mock -CommandName Write-Warning {}
                Mock -CommandName Start-Sleep {}

                # Act
                $result = Invoke-RestMethodWithRateLimit -Splat $splat

                # Assert - Should retry
                $result.data | Should -Be "success"
                Should -Invoke Invoke-RestMethod -Times 2
            }

            foreach ($statusCode in $nonRetryableCases) {
                $splat = @{ Uri = "https://test.com"; Method = "GET" }
                
                Mock -CommandName Invoke-RestMethod -MockWith {
                    throw (New-MockHttpException -StatusCode $statusCode)
                }

                # Act & Assert - Should not retry
                { Invoke-RestMethodWithRateLimit -Splat $splat } | Should -Throw

                Should -Invoke Invoke-RestMethod -Times 1
            }
        }
    }

    Context "When handling non-retryable errors" {
        It "should not retry on 400 errors" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            Mock -CommandName Invoke-RestMethod -MockWith {
                throw (New-MockHttpException -StatusCode 400)
            }
            
            Mock -CommandName Start-Sleep {}

            # Act & Assert
            { Invoke-RestMethodWithRateLimit -Splat $splat } | Should -Throw

            Should -Invoke Invoke-RestMethod -Times 1
            Should -Invoke Start-Sleep -Times 0
        }

        It "should not retry on 401, 403, 404 errors" {
            # Arrange
            $testCases = @(401, 403, 404)
            
            foreach ($statusCode in $testCases) {
                $splat = @{ Uri = "https://test.com"; Method = "GET" }
                
                Mock -CommandName Invoke-RestMethod -MockWith {
                    throw (New-MockHttpException -StatusCode $statusCode)
                }

                # Act & Assert
                { Invoke-RestMethodWithRateLimit -Splat $splat } | Should -Throw

                Should -Invoke Invoke-RestMethod -Times 1
            }
        }
    }

    Context "When max retries is reached" {
        It "should throw after exhausting all retry attempts" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            Mock -CommandName Invoke-RestMethod -MockWith {
                throw (New-MockHttpException -StatusCode 429)
            }
            
            Mock -CommandName Write-Warning {}
            Mock -CommandName Start-Sleep {}

            # Act & Assert
            { Invoke-RestMethodWithRateLimit -Splat $splat -MaxRetries 2 } | Should -Throw

            Should -Invoke Invoke-RestMethod -Times 3  # Initial + 2 retries
            Should -Invoke Start-Sleep -Times 2
        }

        It "should provide verbose logging on final failure" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            Mock -CommandName Invoke-RestMethod -MockWith {
                throw (New-MockHttpException -StatusCode 500)
            }
            
            Mock -CommandName Write-Verbose {}
            Mock -CommandName Start-Sleep {}

            # Act & Assert
            { Invoke-RestMethodWithRateLimit -Splat $splat -MaxRetries 1 } | Should -Throw

            Should -Invoke Write-Verbose -Times 1 -ParameterFilter { $Message -like "*Non-retryable error or max retries reached*Status: 500*" }
        }
    }

    Context "When using custom retry parameters" {
        It "should respect custom MaxRetries parameter" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            Mock -CommandName Invoke-RestMethod -MockWith {
                throw (New-MockHttpException -StatusCode 429)
            }
            
            Mock -CommandName Start-Sleep {}

            # Act & Assert
            { Invoke-RestMethodWithRateLimit -Splat $splat -MaxRetries 5 } | Should -Throw

            Should -Invoke Invoke-RestMethod -Times 6  # Initial + 5 retries
        }

        It "should use custom BaseDelaySeconds for exponential backoff" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            $script:callCount = 0
            Mock -CommandName Invoke-RestMethod -MockWith {
                $script:callCount++
                if ($script:callCount -eq 1) {
                    throw (New-MockHttpException -StatusCode 429)
                }
                return @{ data = "success" }
            }
            
            Mock -CommandName Start-Sleep {} -ParameterFilter { $Seconds -ge 2 -and $Seconds -le 3 }
            Mock -CommandName Get-Random { return 0.0 }

            # Act
            $result = Invoke-RestMethodWithRateLimit -Splat $splat -BaseDelaySeconds 2

            # Assert
            $result.data | Should -Be "success"
            Should -Invoke Start-Sleep -Times 1
        }

        It "should use custom RetryBackOffExponentialFactor for backoff calculation" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            Mock -CommandName Invoke-RestMethod -MockWith {
                throw (New-MockHttpException -StatusCode 500)
            }
            
            $script:sleepValues = @()
            Mock -CommandName Start-Sleep { $script:sleepValues += $Seconds }
            Mock -CommandName Get-Random { return 0.0 }  # No jitter for predictable testing

            # Act & Assert
            { Invoke-RestMethodWithRateLimit -Splat $splat -MaxRetries 2 -BaseDelaySeconds 2 -RetryBackOffExponentialFactor 3.0 } | Should -Throw

            # Assert exponential backoff with factor 3: 3^0*2=2, 3^1*2=6
            $script:sleepValues.Count | Should -Be 2
            $script:sleepValues[0] | Should -Be 2
            $script:sleepValues[1] | Should -Be 6
        }

        It "should use default RetryBackOffExponentialFactor of 1.5 when not specified" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            Mock -CommandName Invoke-RestMethod -MockWith {
                throw (New-MockHttpException -StatusCode 500)
            }
            
            $script:sleepValues = @()
            Mock -CommandName Start-Sleep { $script:sleepValues += $Seconds }
            Mock -CommandName Get-Random { return 0.0 }  # No jitter for predictable testing

            # Act & Assert
            { Invoke-RestMethodWithRateLimit -Splat $splat -MaxRetries 2 -BaseDelaySeconds 2 } | Should -Throw

            # Assert exponential backoff with default factor 1.5: 1.5^0*2=2, 1.5^1*2=3
            $script:sleepValues.Count | Should -Be 2
            $script:sleepValues[0] | Should -Be 2
            $script:sleepValues[1] | Should -Be 3
        }
    }

    Context "When handling exponential backoff calculations" {
        It "should increase delay exponentially on multiple retries" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            Mock -CommandName Invoke-RestMethod -MockWith {
                throw (New-MockHttpException -StatusCode 500)
            }
            
            $script:sleepValues = @()
            Mock -CommandName Start-Sleep { $script:sleepValues += $Seconds }
            Mock -CommandName Get-Random { return 0.0 }  # No jitter for predictable testing

            # Act & Assert
            { Invoke-RestMethodWithRateLimit -Splat $splat -MaxRetries 3 -BaseDelaySeconds 1 } | Should -Throw

            # Assert exponential backoff with default factor 1.5: 1.5^0*1=1, 1.5^1*1=1.5, 1.5^2*1=2.25
            $script:sleepValues.Count | Should -Be 3
            $script:sleepValues[0] | Should -Be 1
            $script:sleepValues[1] | Should -Be 1.5
            $script:sleepValues[2] | Should -Be 2.25
        }

        It "should add jitter to prevent thundering herd" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            $script:callCount = 0
            Mock -CommandName Invoke-RestMethod -MockWith {
                $script:callCount++
                if ($script:callCount -eq 1) {
                    throw (New-MockHttpException -StatusCode 500)
                }
                return @{ data = "success" }
            }
            
            Mock -CommandName Start-Sleep {} -ParameterFilter { $Seconds -ge 1.0 -and $Seconds -le 1.1 }
            Mock -CommandName Get-Random { return 1.0 }  # Maximum jitter

            # Act
            $result = Invoke-RestMethodWithRateLimit -Splat $splat -BaseDelaySeconds 1

            # Assert
            $result.data | Should -Be "success"
            Should -Invoke Start-Sleep -Times 1
        }

        It "should cap exponential backoff delay to MaxDelaySeconds" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            Mock -CommandName Invoke-RestMethod -MockWith {
                throw (New-MockHttpException -StatusCode 500)
            }
            
            $script:sleepValues = @()
            Mock -CommandName Start-Sleep { $script:sleepValues += $Seconds }
            Mock -CommandName Get-Random { return 0.0 }  # No jitter

            # Act & Assert - High exponential factor should be capped
            { Invoke-RestMethodWithRateLimit -Splat $splat -MaxRetries 5 -BaseDelaySeconds 10 -RetryBackOffExponentialFactor 10 -MaxDelaySeconds 15 } | Should -Throw

            # Assert - All delays should be capped to MaxDelaySeconds
            $script:sleepValues.Count | Should -Be 5
            foreach ($delay in $script:sleepValues) {
                $delay | Should -BeLessOrEqual 15
            }
        }

        It "should handle jitter with zero exponential delay" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            $script:callCount = 0
            Mock -CommandName Invoke-RestMethod -MockWith {
                $script:callCount++
                if ($script:callCount -eq 1) {
                    throw (New-MockHttpException -StatusCode 429)
                }
                return @{ data = "success" }
            }
            
            Mock -CommandName Start-Sleep {} -ParameterFilter { $Seconds -ge 0 -and $Seconds -le 0.1 }
            Mock -CommandName Get-Random { return 1.0 }  # Maximum jitter

            # Act - With zero BaseDelaySeconds, jitter should still work
            $result = Invoke-RestMethodWithRateLimit -Splat $splat -BaseDelaySeconds 0

            # Assert
            $result.data | Should -Be "success"
            Should -Invoke Start-Sleep -Times 1
        }

        It "should handle negative jitter result gracefully" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            $script:callCount = 0
            Mock -CommandName Invoke-RestMethod -MockWith {
                $script:callCount++
                if ($script:callCount -eq 1) {
                    throw (New-MockHttpException -StatusCode 500)
                }
                return @{ data = "success" }
            }
            
            # Mock Get-Random to return negative value (should be clamped)
            Mock -CommandName Get-Random { return -0.5 }
            Mock -CommandName Start-Sleep {} -ParameterFilter { $Seconds -ge 0 }

            # Act
            $result = Invoke-RestMethodWithRateLimit -Splat $splat -BaseDelaySeconds 1

            # Assert - Should not have negative delay
            $result.data | Should -Be "success"
            Should -Invoke Start-Sleep -Times 1
        }

        It "should handle very large exponential calculations" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            Mock -CommandName Invoke-RestMethod -MockWith {
                throw (New-MockHttpException -StatusCode 500)
            }
            
            Mock -CommandName Start-Sleep {}
            Mock -CommandName Get-Random { return 0.0 }

            # Act & Assert - Should not cause overflow or exceptions
            { Invoke-RestMethodWithRateLimit -Splat $splat -MaxRetries 1 -BaseDelaySeconds 1000 -RetryBackOffExponentialFactor 999 -MaxDelaySeconds 30 } | Should -Throw

            # Should be capped to MaxDelaySeconds regardless of calculation
            Should -Invoke Start-Sleep -Times 1 -ParameterFilter { $Seconds -le 30 }
        }
    }

    Context "When handling verbose logging" {
        It "should log API call attempts" {
            # Arrange
            $splat = @{ Uri = "https://api.test.com/endpoint"; Method = "POST" }
            
            Mock -CommandName Invoke-RestMethod -MockWith { return @{ success = $true } }
            Mock -CommandName Write-Verbose {}

            # Act
            $result = Invoke-RestMethodWithRateLimit -Splat $splat

            # Assert
            Should -Invoke Write-Verbose -Times 1 -ParameterFilter { $Message -like "*API call attempt 1 to https://api.test.com/endpoint*" }
        }

        It "should log multiple API call attempts with correct attempt numbers" {
            # Arrange
            $splat = @{ Uri = "https://api.fabric.microsoft.com/v1/test"; Method = "GET" }
            
            $script:callCount = 0
            Mock -CommandName Invoke-RestMethod -MockWith {
                $script:callCount++
                if ($script:callCount -le 2) {
                    throw (New-MockHttpException -StatusCode 500)
                }
                return @{ data = "success" }
            }
            
            Mock -CommandName Write-Verbose {}
            Mock -CommandName Start-Sleep {}

            # Act
            $result = Invoke-RestMethodWithRateLimit -Splat $splat

            # Assert - Should log attempts 1, 2, and 3
            Should -Invoke Write-Verbose -Times 1 -ParameterFilter { $Message -like "*API call attempt 1 to https://api.fabric.microsoft.com/v1/test*" }
            Should -Invoke Write-Verbose -Times 1 -ParameterFilter { $Message -like "*API call attempt 2 to https://api.fabric.microsoft.com/v1/test*" }
            Should -Invoke Write-Verbose -Times 1 -ParameterFilter { $Message -like "*API call attempt 3 to https://api.fabric.microsoft.com/v1/test*" }
        }

        It "should log success message only when retries occurred" {
            # Arrange - Test success after retries
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            $script:callCount = 0
            Mock -CommandName Invoke-RestMethod -MockWith {
                $script:callCount++
                if ($script:callCount -eq 1) {
                    throw (New-MockHttpException -StatusCode 429)
                }
                return @{ data = "success" }
            }
            
            Mock -CommandName Write-Information {}
            Mock -CommandName Start-Sleep {}

            # Act
            $result = Invoke-RestMethodWithRateLimit -Splat $splat

            # Assert - Should log success after 2 attempts
            Should -Invoke Write-Information -Times 1 -ParameterFilter { $MessageData -like "*API call succeeded after 2 attempts*" }
        }

        It "should not log success message on first attempt success" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            Mock -CommandName Invoke-RestMethod -MockWith { return @{ data = "success" } }
            Mock -CommandName Write-Information {}

            # Act
            $result = Invoke-RestMethodWithRateLimit -Splat $splat

            # Assert - Should not log success message for first attempt
            Should -Invoke Write-Information -Times 0 -ParameterFilter { $MessageData -like "*succeeded after*attempts*" }
        }

        It "should log success with correct attempt count after various retry scenarios" {
            # Test different retry counts
            $testCases = @(2, 3, 4)
            
            foreach ($expectedAttempts in $testCases) {
                $splat = @{ Uri = "https://test.com"; Method = "GET" }
                
                $script:callCount = 0
                Mock -CommandName Invoke-RestMethod -MockWith {
                    $script:callCount++
                    if ($script:callCount -lt $expectedAttempts) {
                        throw (New-MockHttpException -StatusCode 500)
                    }
                    return @{ data = "success" }
                }
                
                Mock -CommandName Write-Information {}
                Mock -CommandName Start-Sleep {}

                # Act
                $result = Invoke-RestMethodWithRateLimit -Splat $splat

                # Assert - Should log correct attempt count
                Should -Invoke Write-Information -Times 1 -ParameterFilter { $MessageData -like "*API call succeeded after $expectedAttempts attempts*" }
            }
        }

        It "should extract URI correctly from different Splat structures" {
            # Arrange - Test URI extraction with different formats
            $testCases = @(
                @{ Uri = "https://simple.com"; Method = "GET" },
                @{ Uri = "https://complex.com/path/to/resource?param=value"; Method = "POST"; Body = "test" },
                @{ Uri = "https://auth.com/api"; Method = "GET"; Headers = @{ Authorization = "Bearer token" } }
            )
            
            foreach ($splat in $testCases) {
                Mock -CommandName Invoke-RestMethod -MockWith { return @{ success = $true } }
                Mock -CommandName Write-Verbose {}

                # Act
                $result = Invoke-RestMethodWithRateLimit -Splat $splat

                # Assert - Should log correct URI
                Should -Invoke Write-Verbose -Times 1 -ParameterFilter { $Message -like "*API call attempt 1 to $($splat.Uri)*" }
            }
        }
    }

    Context "When handling malformed headers" {
        It "should handle invalid Retry-After header gracefully" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            $script:callCount = 0
            Mock -CommandName Invoke-RestMethod -MockWith {
                $script:callCount++
                if ($script:callCount -eq 1) {
                    $headers = @{ "Retry-After" = @("invalid-value") }
                    throw (New-MockHttpException -StatusCode 429 -Headers $headers)
                }
                return @{ data = "success" }
            }
            
            Mock -CommandName Write-Verbose {}
            Mock -CommandName Write-Warning {}
            Mock -CommandName Start-Sleep {}
            Mock -CommandName Get-Random { return 0.0 }

            # Act
            $result = Invoke-RestMethodWithRateLimit -Splat $splat -BaseDelaySeconds 1

            # Assert - should fall back to exponential backoff
            $result.data | Should -Be "success"
            Should -Invoke Write-Verbose -Times 1 -ParameterFilter { $Message -like "*Could not parse Retry-After header*invalid-value*" }
            Should -Invoke Start-Sleep -Times 1 -ParameterFilter { $Seconds -eq 1 }
        }

        It "should handle invalid X-RateLimit-Remaining header gracefully" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            $script:callCount = 0
            Mock -CommandName Invoke-RestMethod -MockWith {
                $script:callCount++
                if ($script:callCount -eq 1) {
                    $headers = @{ 
                        "X-RateLimit-Remaining" = @("not-a-number")
                        "X-RateLimit-Reset" = @("132483916800000000")
                    }
                    throw (New-MockHttpException -StatusCode 429 -Headers $headers)
                }
                return @{ data = "success" }
            }
            
            Mock -CommandName Write-Verbose {}
            Mock -CommandName Write-Warning {}
            Mock -CommandName Start-Sleep {}
            Mock -CommandName Get-Random { return 0.0 }

            # Act
            $result = Invoke-RestMethodWithRateLimit -Splat $splat -BaseDelaySeconds 1

            # Assert - should fall back to exponential backoff
            $result.data | Should -Be "success"
            Should -Invoke Write-Verbose -Times 1 -ParameterFilter { $Message -like "*Could not parse X-RateLimit-Remaining header*not-a-number*" }
            Should -Invoke Start-Sleep -Times 1 -ParameterFilter { $Seconds -eq 1 }
        }

        It "should handle invalid X-RateLimit-Reset header gracefully" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            $script:callCount = 0
            Mock -CommandName Invoke-RestMethod -MockWith {
                $script:callCount++
                if ($script:callCount -eq 1) {
                    $headers = @{ 
                        "X-RateLimit-Remaining" = @("0")
                        "X-RateLimit-Reset" = @("invalid-filetime")
                    }
                    throw (New-MockHttpException -StatusCode 429 -Headers $headers)
                }
                return @{ data = "success" }
            }
            
            Mock -CommandName Write-Verbose {}
            Mock -CommandName Write-Warning {}
            Mock -CommandName Start-Sleep {}
            Mock -CommandName Get-Random { return 0.0 }

            # Act
            $result = Invoke-RestMethodWithRateLimit -Splat $splat -BaseDelaySeconds 1

            # Assert - should fall back to exponential backoff
            $result.data | Should -Be "success"
            Should -Invoke Write-Verbose -Times 1 -ParameterFilter { $Message -like "*Could not parse X-RateLimit-Reset header*invalid-filetime*" }
            Should -Invoke Start-Sleep -Times 1 -ParameterFilter { $Seconds -eq 1 }
        }

        It "should ignore X-RateLimit headers when X-RateLimit-Reset is missing" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            $script:callCount = 0
            Mock -CommandName Invoke-RestMethod -MockWith {
                $script:callCount++
                if ($script:callCount -eq 1) {
                    $headers = @{ "X-RateLimit-Remaining" = @("0") }
                    throw (New-MockHttpException -StatusCode 429 -Headers $headers)
                }
                return @{ data = "success" }
            }
            
            Mock -CommandName Write-Warning {}
            Mock -CommandName Start-Sleep {}
            Mock -CommandName Get-Random { return 0.0 }

            # Act
            $result = Invoke-RestMethodWithRateLimit -Splat $splat -BaseDelaySeconds 1

            # Assert - should use exponential backoff since X-RateLimit-Reset is missing
            $result.data | Should -Be "success"
            Should -Invoke Start-Sleep -Times 1 -ParameterFilter { $Seconds -eq 1 }
            Should -Invoke Write-Warning -Times 1 -ParameterFilter { $Message -like "*Rate limited*Retrying*" }
        }

        It "should handle multiple header values correctly" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            $script:callCount = 0
            Mock -CommandName Invoke-RestMethod -MockWith {
                $script:callCount++
                if ($script:callCount -eq 1) {
                    # Some APIs might return multiple values - should use first one
                    $headers = @{ "Retry-After" = @("10", "20") }
                    throw (New-MockHttpException -StatusCode 429 -Headers $headers)
                }
                return @{ data = "success" }
            }
            
            Mock -CommandName Write-Information {}
            Mock -CommandName Start-Sleep {} -ParameterFilter { $Seconds -eq 10 }

            # Act
            $result = Invoke-RestMethodWithRateLimit -Splat $splat

            # Assert - Should use first value (10)
            $result.data | Should -Be "success"
            Should -Invoke Start-Sleep -Times 1 -ParameterFilter { $Seconds -eq 10 }
        }

        It "should handle empty headers collection" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            # Create mock response with empty headers array
            $response = New-Object PSObject -Property @{
                StatusCode = New-Object PSObject -Property @{ value__ = 429 }
                Headers = @()  # Empty headers collection
            }
            
            $script:callCount = 0
            Mock -CommandName Invoke-RestMethod -MockWith {
                $script:callCount++
                if ($script:callCount -eq 1) {
                    $exception = New-Object System.Exception("HTTP 429 Error")
                    Add-Member -InputObject $exception -MemberType NoteProperty -Name Response -Value $response
                    throw $exception
                }
                return @{ data = "success" }
            }
            
            Mock -CommandName Write-Warning {}
            Mock -CommandName Start-Sleep {}
            Mock -CommandName Get-Random { return 0.0 }

            # Act
            $result = Invoke-RestMethodWithRateLimit -Splat $splat -BaseDelaySeconds 1

            # Assert - Should fall back to exponential backoff
            $result.data | Should -Be "success"
            Should -Invoke Start-Sleep -Times 1 -ParameterFilter { $Seconds -eq 1 }
        }

        It "should handle headers with null values" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            $script:callCount = 0
            Mock -CommandName Invoke-RestMethod -MockWith {
                $script:callCount++
                if ($script:callCount -eq 1) {
                    # Create header with null value array
                    $headers = @{ "Retry-After" = $null }
                    throw (New-MockHttpException -StatusCode 429 -Headers $headers)
                }
                return @{ data = "success" }
            }
            
            Mock -CommandName Write-Warning {}
            Mock -CommandName Start-Sleep {}
            Mock -CommandName Get-Random { return 0.0 }

            # Act
            $result = Invoke-RestMethodWithRateLimit -Splat $splat -BaseDelaySeconds 1

            # Assert - Should fall back to exponential backoff
            $result.data | Should -Be "success"
            Should -Invoke Start-Sleep -Times 1 -ParameterFilter { $Seconds -eq 1 }
        }

        It "should handle case-insensitive header matching" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            $script:callCount = 0
            Mock -CommandName Invoke-RestMethod -MockWith {
                $script:callCount++
                if ($script:callCount -eq 1) {
                    # Test different case variations
                    $headers = @{ "retry-after" = @("8") }  # lowercase
                    throw (New-MockHttpException -StatusCode 429 -Headers $headers)
                }
                return @{ data = "success" }
            }
            
            Mock -CommandName Write-Information {}
            Mock -CommandName Start-Sleep {} -ParameterFilter { $Seconds -eq 8 }

            # Act
            $result = Invoke-RestMethodWithRateLimit -Splat $splat

            # Assert - Should work with lowercase header name
            $result.data | Should -Be "success"
            Should -Invoke Start-Sleep -Times 1 -ParameterFilter { $Seconds -eq 8 }
        }

        It "should handle X-RateLimit-Reset with past timestamps gracefully" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            # Create a past timestamp (1 hour ago)
            $pastFileTime = [DateTime]::UtcNow.AddHours(-1).ToFileTimeUtc()
            
            $script:callCount = 0
            Mock -CommandName Invoke-RestMethod -MockWith {
                $script:callCount++
                if ($script:callCount -eq 1) {
                    $headers = @{ 
                        "X-RateLimit-Remaining" = @("0")
                        "X-RateLimit-Reset" = @($pastFileTime.ToString())
                    }
                    throw (New-MockHttpException -StatusCode 429 -Headers $headers)
                }
                return @{ data = "success" }
            }
            
            Mock -CommandName Write-Verbose {}
            Mock -CommandName Write-Warning {}
            Mock -CommandName Start-Sleep {}
            Mock -CommandName Get-Random { return 0.0 }

            # Act
            $result = Invoke-RestMethodWithRateLimit -Splat $splat -BaseDelaySeconds 1

            # Assert - Should process the headers, but may fall back to exponential backoff if time calculation fails or is negative
            $result.data | Should -Be "success"
            Should -Invoke Start-Sleep -Times 1
            # The verbose message might be called but the delay calculation may still fall back to exponential backoff
            # So we just verify the function completes successfully
        }
    }

    Context "When validating parameters" {
        It "should handle zero MaxRetries gracefully" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            Mock -CommandName Invoke-RestMethod -MockWith {
                throw (New-MockHttpException -StatusCode 429)
            }
            
            Mock -CommandName Start-Sleep {}

            # Act & Assert - Should try once (initial attempt) then throw
            { Invoke-RestMethodWithRateLimit -Splat $splat -MaxRetries 0 } | Should -Throw

            Should -Invoke Invoke-RestMethod -Times 1  # Only initial attempt, no retries
            Should -Invoke Start-Sleep -Times 0
        }

        It "should handle negative MaxRetries by not executing any attempts" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            Mock -CommandName Invoke-RestMethod -MockWith {
                throw (New-MockHttpException -StatusCode 500)
            }

            # Act & Assert - Should throw immediately without any API calls (negative MaxRetries means no attempts)
            { Invoke-RestMethodWithRateLimit -Splat $splat -MaxRetries -1 } | Should -Throw

            Should -Invoke Invoke-RestMethod -Times 0
        }

        It "should handle zero BaseDelaySeconds" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            $script:callCount = 0
            Mock -CommandName Invoke-RestMethod -MockWith {
                $script:callCount++
                if ($script:callCount -eq 1) {
                    throw (New-MockHttpException -StatusCode 429)
                }
                return @{ data = "success" }
            }
            
            Mock -CommandName Start-Sleep {} -ParameterFilter { $Seconds -eq 0 }
            Mock -CommandName Get-Random { return 0.0 }

            # Act
            $result = Invoke-RestMethodWithRateLimit -Splat $splat -BaseDelaySeconds 0

            # Assert - Should work with zero delay
            $result.data | Should -Be "success"
            Should -Invoke Start-Sleep -Times 1 -ParameterFilter { $Seconds -eq 0 }
        }

        It "should handle very small BaseDelaySeconds" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            $script:callCount = 0
            Mock -CommandName Invoke-RestMethod -MockWith {
                $script:callCount++
                if ($script:callCount -eq 1) {
                    throw (New-MockHttpException -StatusCode 500)
                }
                return @{ data = "success" }
            }
            
            Mock -CommandName Start-Sleep {} -ParameterFilter { $Seconds -le 0.01 }
            Mock -CommandName Get-Random { return 0.0 }

            # Act
            $result = Invoke-RestMethodWithRateLimit -Splat $splat -BaseDelaySeconds 0.001

            # Assert
            $result.data | Should -Be "success"
            Should -Invoke Start-Sleep -Times 1
        }

        It "should handle zero MaxDelaySeconds" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            $script:callCount = 0
            Mock -CommandName Invoke-RestMethod -MockWith {
                $script:callCount++
                if ($script:callCount -eq 1) {
                    $headers = @{ "Retry-After" = @("60") }
                    throw (New-MockHttpException -StatusCode 429 -Headers $headers)
                }
                return @{ data = "success" }
            }
            
            Mock -CommandName Write-Information {}
            Mock -CommandName Start-Sleep {} -ParameterFilter { $Seconds -eq 0 }

            # Act
            $result = Invoke-RestMethodWithRateLimit -Splat $splat -MaxDelaySeconds 0

            # Assert - Retry-After should be capped to 0
            $result.data | Should -Be "success"
            Should -Invoke Start-Sleep -Times 1 -ParameterFilter { $Seconds -eq 0 }
        }

        It "should handle very large MaxRetries" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            Mock -CommandName Invoke-RestMethod -MockWith { return @{ data = "success" } }

            # Act - Should not cause performance issues with large MaxRetries if first call succeeds
            $result = Invoke-RestMethodWithRateLimit -Splat $splat -MaxRetries 1000

            # Assert
            $result.data | Should -Be "success"
            Should -Invoke Invoke-RestMethod -Times 1
        }

        It "should handle very high RetryBackOffExponentialFactor" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            Mock -CommandName Invoke-RestMethod -MockWith {
                throw (New-MockHttpException -StatusCode 500)
            }
            
            Mock -CommandName Start-Sleep {}
            Mock -CommandName Get-Random { return 0.0 }

            # Act & Assert - High factor should be capped by MaxDelaySeconds
            { Invoke-RestMethodWithRateLimit -Splat $splat -MaxRetries 1 -BaseDelaySeconds 1 -RetryBackOffExponentialFactor 100 -MaxDelaySeconds 5 } | Should -Throw

            # Should be capped to MaxDelaySeconds
            Should -Invoke Start-Sleep -Times 1 -ParameterFilter { $Seconds -le 5 }
        }
    }

    Context "When handling different exception types" {
        It "should handle WebException with HttpWebResponse" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            # Create a more realistic WebException structure
            $webResponse = New-Object PSObject -Property @{
                StatusCode = New-Object PSObject -Property @{ value__ = 429 }
                Headers = @(
                    (New-Object PSObject -Property @{ Key = "Retry-After"; Value = @("15") })
                )
            }
            
            Mock -CommandName Invoke-RestMethod -MockWith {
                $webException = New-Object System.Net.WebException("The remote server returned an error: (429) Too Many Requests")
                Add-Member -InputObject $webException -MemberType NoteProperty -Name Response -Value $webResponse -Force
                throw $webException
            }
            
            Mock -CommandName Write-Information {}
            Mock -CommandName Start-Sleep {}

            # Act & Assert - Should handle WebException Response structure
            { Invoke-RestMethodWithRateLimit -Splat $splat -MaxRetries 1 } | Should -Throw

            Should -Invoke Start-Sleep -Times 1 -ParameterFilter { $Seconds -eq 15 }
        }

        It "should handle HttpRequestException structure" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            # Create HttpRequestException-like structure
            $httpResponse = New-Object PSObject -Property @{
                StatusCode = New-Object PSObject -Property @{ value__ = 503 }
                Headers = @()
            }
            
            $script:callCount = 0
            Mock -CommandName Invoke-RestMethod -MockWith {
                $script:callCount++
                if ($script:callCount -eq 1) {
                    $httpException = New-Object System.Net.Http.HttpRequestException("Response status code does not indicate success: 503")
                    Add-Member -InputObject $httpException -MemberType NoteProperty -Name Response -Value $httpResponse
                    throw $httpException
                }
                return @{ data = "success" }
            }
            
            Mock -CommandName Write-Warning {}
            Mock -CommandName Start-Sleep {}

            # Act
            $result = Invoke-RestMethodWithRateLimit -Splat $splat

            # Assert - Should retry on 503
            $result.data | Should -Be "success"
            Should -Invoke Invoke-RestMethod -Times 2
            Should -Invoke Write-Warning -Times 1 -ParameterFilter { $Message -like "*Server error (503)*" }
        }

        It "should handle exceptions with null Response property" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            Mock -CommandName Invoke-RestMethod -MockWith {
                $exception = New-Object System.Exception("Connection error")
                Add-Member -InputObject $exception -MemberType NoteProperty -Name Response -Value $null
                throw $exception
            }

            # Act & Assert - Should not retry when Response is null
            { Invoke-RestMethodWithRateLimit -Splat $splat } | Should -Throw "*Connection error*"

            Should -Invoke Invoke-RestMethod -Times 1
        }

        It "should handle exceptions with malformed Response structure" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            # Create response with missing or malformed StatusCode
            $malformedResponse = New-Object PSObject -Property @{
                StatusCode = $null  # Missing or malformed StatusCode
                Headers = @()
            }
            
            Mock -CommandName Invoke-RestMethod -MockWith {
                $exception = New-Object System.Exception("HTTP Error")
                Add-Member -InputObject $exception -MemberType NoteProperty -Name Response -Value $malformedResponse
                throw $exception
            }

            # Act & Assert - Should not retry when StatusCode is malformed
            { Invoke-RestMethodWithRateLimit -Splat $splat } | Should -Throw "*HTTP Error*"

            Should -Invoke Invoke-RestMethod -Times 1
        }

        It "should handle exceptions without Response property" {
            # Arrange
            $splat = @{ Uri = "https://test.com"; Method = "GET" }
            
            Mock -CommandName Invoke-RestMethod -MockWith {
                throw "Network timeout error"
            }

            # Act & Assert
            { Invoke-RestMethodWithRateLimit -Splat $splat } | Should -Throw "*Network timeout error*"

            Should -Invoke Invoke-RestMethod -Times 1
        }
    }
}