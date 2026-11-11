# <copyright file="Invoke-RestMethodWithRateLimit.ps1" company="Endjin Limited">
# Copyright (c) Endjin Limited. All rights reserved.
# </copyright>

function Invoke-RestMethodWithRateLimit {
    <#
        .SYNOPSIS
            Invokes REST API calls with automatic rate limit handling and exponential backoff retry logic.

        .DESCRIPTION
            This wrapper function handles Fabric API and Microsoft Graph API rate limiting by:

            - Detecting HTTP 429 responses
            - Extracting Retry-After headers
            - Implementing exponential backoff with jitter
            - Providing configurable retry attempts
            - Maintaining detailed logging

        .PARAMETER Splat
            Hashtable containing all parameters for Invoke-RestMethod (Uri, Method, Headers, Body, etc.)

        .PARAMETER MaxRetries
            Maximum number of retry attempts (default: 3).

        .PARAMETER BaseDelaySeconds
            Base delay in seconds for exponential backoff (default: 1).

        .PARAMETER MaxDelaySeconds
            Maximum delay in seconds between retries (default: 60).

        .PARAMETER RetryBackOffExponentialFactor
            The factor used when calculating how quickly the exponential backoff should increase. Defaults to 1.5.

        .OUTPUTS
            System.Object

        .EXAMPLE
            ```powershell
            $splat = @{
                Uri = "https://api.fabric.microsoft.com/v1/connections"
                Method = "GET"
                Headers = @{ Authorization = "Bearer $token" }
            }
            $result = Invoke-RestMethodWithRateLimit -Splat $splat
            ```

        .EXAMPLE
            ```powershell
            $result = Invoke-RestMethodWithRateLimit -Splat $splat -MaxRetries 5 -BaseDelaySeconds 2 -MaxDelaySeconds 120
            ```
    #>
    [CmdletBinding()]
    [OutputType([Object])]
    param (
        [Parameter(Mandatory=$true)]
        [hashtable] $Splat,
        
        [Parameter()]
        [int] $MaxRetries = 3,
        
        [Parameter()]
        [double] $BaseDelaySeconds = 1.0,
        
        [Parameter()]
        [int] $MaxDelaySeconds = 60,

        [Parameter()]
        [double] $RetryBackOffExponentialFactor = 1.5
    )
    
    $attempt = 0
    $lastException = $null
    
    while ($attempt -le $MaxRetries) {
        try {
            Write-Verbose "API call attempt $($attempt + 1) to $($Splat.Uri)"
            
            $response = Invoke-RestMethod @Splat
            
            if ($attempt -gt 0) {
                Write-Information "API call succeeded after $($attempt + 1) attempts"
            }
            
            return $response
            
        } catch {
            $lastException = $_
            $statusCode = $null
            $retryAfter = $null
            
            # Extract status code and retry-after header if available
            if ($_.Exception.Response) {
                $statusCode = $_.Exception.Response.StatusCode.value__
                
                # Look for rate limiting headers using the extracted validation function
                if ($_.Exception.Response.Headers) {
                    # Treat 'Retry-After' header as taking precedence as it is the only such mechanism in the current specification
                    # Ref: https://datatracker.ietf.org/doc/html/rfc7231#section-7.1.3
                    $retryAfterValue = Get-HttpHeaderValue -Headers $_.Exception.Response.Headers -HeaderName 'Retry-After' -ExpectedType ([int]) -DefaultValue $null
                    
                    if ($retryAfterValue) {
                        $retryAfter = $retryAfterValue
                    }
                    else {
                        # Whilst no longer part of a current specification, the 'X-RateLimit-*' headers are used in the real world (e.g. GitHub)
                        $rateLimitRemaining = Get-HttpHeaderValue -Headers $_.Exception.Response.Headers -HeaderName 'X-RateLimit-Remaining' -ExpectedType ([int]) -DefaultValue $null
                        $rateLimitResetFileTime = Get-HttpHeaderValue -Headers $_.Exception.Response.Headers -HeaderName 'X-RateLimit-Reset' -ExpectedType ([long]) -DefaultValue $null
                        
                        # When all the required headers are available, set the retry interval based on the 'X-RateLimit-Reset' header
                        if ($null -ne $rateLimitRemaining -and $rateLimitRemaining -eq 0 -and $rateLimitResetFileTime) {
                            try {
                                $rateLimitResetDateTime = [datetime]::FromFileTimeUtc($rateLimitResetFileTime)
                                $retryAfter = ($rateLimitResetDateTime - [datetime]::UtcNow).TotalSeconds
                                
                                # Only use the calculated retry interval if it's positive
                                if ($retryAfter -gt 0) {
                                    Write-Verbose "Rate limit exceeded. Waiting for quota reset in $retryAfter seconds..."
                                } else {
                                    Write-Verbose "X-RateLimit-Reset time is in the past. Falling back to exponential backoff."
                                    $retryAfter = $null
                                }
                            } catch {
                                Write-Verbose "Could not convert X-RateLimit-Reset FileTime to DateTime: $rateLimitResetFileTime. Falling back to exponential backoff."
                            }
                        }
                    }
                }
            }
            
            # Check if this is a retryable error (429 or 5xx)
            $isRetryable = ($statusCode -eq 429) -or ($statusCode -ge 500 -and $statusCode -lt 600)
            
            if (-not $isRetryable -or $attempt -eq $MaxRetries) {
                Write-Verbose "Non-retryable error or max retries reached. Status: $statusCode, Attempt: $($attempt + 1)"
                throw $lastException
            }
            
            $attempt++
            
            # Calculate delay with exponential backoff and jitter
            if ($statusCode -eq 429 -and $retryAfter) {
                # Use server-provided retry-after value, or the 'MaxDelaySeconds' if it is shorter
                $delaySeconds = [Math]::Min($retryAfter, $MaxDelaySeconds)
                if ($retryAfter -gt $MaxDelaySeconds) {
                    Write-Information "Rate limited (429). Server requested waiting $retryAfter seconds which exceeds MaxDelaySeconds. Waiting for $MaxDelaySeconds seconds."
                }
                else {
                    Write-Information "Rate limited (429). Waiting $delaySeconds seconds as requested by server."
                }
            } else {
                # Use exponential backoff with jitter
                $exponentialDelay = [Math]::Pow($RetryBackOffExponentialFactor, $attempt - 1) * $BaseDelaySeconds
                $jitter = (Get-Random -Minimum 0.0 -Maximum 1.0) * 0.1 * $exponentialDelay
                $delaySeconds = [Math]::Min($exponentialDelay + $jitter, $MaxDelaySeconds)
                
                $errorType = if ($statusCode -eq 429) { "Rate limited" } else { "Server error ($statusCode)" }
                Write-Warning "$errorType. Retrying in $([Math]::Round($delaySeconds, 2)) seconds (attempt $attempt of $MaxRetries)"
            }
            
            Start-Sleep -Seconds $delaySeconds
        }
    }
    
    throw $lastException
}
