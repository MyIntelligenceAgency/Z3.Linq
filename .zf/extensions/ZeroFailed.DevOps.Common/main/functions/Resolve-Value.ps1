# <copyright file="Resolve-Value.ps1" company="Endjin Limited">
# Copyright (c) Endjin Limited. All rights reserved.
# </copyright>

function Resolve-Value {
    <#
        .SYNOPSIS
            Evaluates a provided value which may be static or dynamic.

        .DESCRIPTION
            This cmdlet accepts a value of any type and evaluates it:

            - If the value is a scriptblock, it invokes the scriptblock and returns its result
            - If the value is any other type, it returns the value unchanged

            This allows configuration to be defined either as static values or as scriptblocks that provide dynamic values determined at runtime.

        .PARAMETER Value
            The configuration value to be resolved. This can be any object type, including a scriptblock.

        .OUTPUTS
            System.Object

        .EXAMPLE
            ```powershell
            "StaticValue" | Resolve-Value
            StaticValue
            ```

        .EXAMPLE
            ```powershell
            { Get-Date -Format "yyyy-MM-dd" } | Resolve-Value
            2023-04-15
            ```

        .EXAMPLE
            ```powershell
            $foo = { $bar }
            PS> $bar = "DeferredValue"
            PS> Resolve-Value $foo
            DeferredValue
            ```
    #>
    [CmdletBinding()]
    [OutputType([Object])]
    param (
        [Parameter(Mandatory, ValueFromPipeline)]
        [AllowEmptyString()]
        [AllowNull()]
        $Value
    )

    if ($Value -is [scriptblock]) {
        $Value.Invoke()
    }
    else {
        $Value
    }
}
