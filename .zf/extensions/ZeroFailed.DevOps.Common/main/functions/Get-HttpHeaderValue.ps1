# <copyright file="Get-HttpHeaderValue.ps1" company="Endjin Limited">
# Copyright (c) Endjin Limited. All rights reserved.
# </copyright>

function Get-HttpHeaderValue {
    <#
        .SYNOPSIS
            Extracts and validates HTTP header values from a headers collection with type conversion.

        .DESCRIPTION
            This function searches for a specific HTTP header in a headers collection and attempts to convert it to the specified type.
            It provides robust error handling, case-insensitive header matching, and verbose logging for debugging.

        .PARAMETER Headers
            The collection of HTTP headers to search through. Each header should have a Key and Value property.

        .PARAMETER HeaderName
            The name of the header to search for (case-insensitive).

        .PARAMETER ExpectedType
            The .NET type to convert the header value to (e.g., [int], [datetime], [string], [double]).

        .PARAMETER DefaultValue
            The default value to return if the header is not found or cannot be parsed.
            Must be compatible with ExpectedType.

        .OUTPUTS
            System.Object

        .EXAMPLE
            ```powershell
            $retryAfter = Get-HttpHeaderValue -Headers $response.Headers -HeaderName "Retry-After" -ExpectedType ([int]) -DefaultValue 0
            ```

        .EXAMPLE
            ```powershell
            $headers = $response.Headers
            $resetTime = Get-HttpHeaderValue -Headers $headers -HeaderName "X-RateLimit-Reset" -ExpectedType ([long]) -DefaultValue 0
            if ($resetTime -gt 0) {
                $resetDateTime = [datetime]::FromFileTimeUtc($resetTime)
            }
            ```

        .EXAMPLE
            ```powershell
            $customValue = Get-HttpHeaderValue -Headers $response.Headers -HeaderName "X-Custom-Header" -ExpectedType ([string]) -DefaultValue "default"
            ```
    #>
    [CmdletBinding()]
    [OutputType([Object])]
    param (
        [Parameter(Mandatory=$true)]
        [AllowNull()]
        [AllowEmptyCollection()]
        [object[]] $Headers,
        
        [Parameter(Mandatory=$true)]
        [string] $HeaderName,
        
        [Parameter(Mandatory=$true)]
        [type] $ExpectedType,
        
        [Parameter()]
        [object] $DefaultValue = $null
    )
    
    # Handle null or empty headers collection
    if (-not $Headers -or $Headers.Count -eq 0) {
        Write-Verbose "Headers collection is null or empty. Returning default value for header '$HeaderName'."
        return $DefaultValue
    }
    
    # Search for header (case-insensitive)
    $header = $Headers | Where-Object { $_.Key -ieq $HeaderName }
    
    if (-not $header) {
        Write-Verbose "Header '$HeaderName' not found in headers collection. Returning default value."
        return $DefaultValue
    }
    
    # Handle case where header exists but has no value or empty value array
    if (-not $header.Value -or $header.Value.Count -eq 0) {
        Write-Verbose "Header '$HeaderName' found but has no value. Returning default value."
        return $DefaultValue
    }
    
    # Get the first value if multiple values exist
    $headerValue = $header.Value[0]
    
    if ([string]::IsNullOrWhiteSpace($headerValue)) {
        Write-Verbose "Header '$HeaderName' found but value is null or whitespace. Returning default value."
        return $DefaultValue
    }
    
    # Attempt type conversion
    try {
        if ($ExpectedType -eq [string]) {
            # No conversion needed for string
            Write-Verbose "Successfully extracted header '$HeaderName' as string: '$headerValue'"
            return $headerValue
        }
        elseif ($ExpectedType -eq [int]) {
            $converted = [int]$headerValue
            Write-Verbose "Successfully parsed header '$HeaderName' as int: $converted"
            return $converted
        }
        elseif ($ExpectedType -eq [long]) {
            $converted = [long]$headerValue
            Write-Verbose "Successfully parsed header '$HeaderName' as long: $converted"
            return $converted
        }
        elseif ($ExpectedType -eq [double]) {
            $converted = [double]$headerValue
            Write-Verbose "Successfully parsed header '$HeaderName' as double: $converted"
            return $converted
        }
        elseif ($ExpectedType -eq [datetime]) {
            $converted = [datetime]$headerValue
            Write-Verbose "Successfully parsed header '$HeaderName' as datetime: $converted"
            return $converted
        }
        else {
            # Generic type conversion for other types
            $converted = $headerValue -as $ExpectedType
            if ($null -eq $converted) {
                throw "Type conversion failed"
            }
            Write-Verbose "Successfully converted header '$HeaderName' to $($ExpectedType.Name): $converted"
            return $converted
        }
    }
    catch {
        Write-Verbose "Could not parse $HeaderName header: $headerValue. Error: $($_.Exception.Message). Returning default value."
        return $DefaultValue
    }
}
