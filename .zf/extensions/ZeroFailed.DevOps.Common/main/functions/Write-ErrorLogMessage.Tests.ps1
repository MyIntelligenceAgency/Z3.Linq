# <copyright file="Write-ErrorLogMessage.Tests.ps1" company="Endjin Limited">
# Copyright (c) Endjin Limited. All rights reserved.
# </copyright>

BeforeAll {
    # sut
    . $PSCommandPath.Replace('.Tests.ps1','.ps1')
}

Describe 'Write-ErrorLogMessage' {
    BeforeAll {
        Mock Write-Information {}
        Mock Write-Error {}
        $errorMessage = 'Something went wrong'
    }
    
    Context 'When running in GitHub Actions' {
        
        It 'should log the correctly formatted message' {
            
            $IsGitHubActions = $true
            $IsAzureDevOps = $false

            Write-ErrorLogMessage -Message $errorMessage
            
            Should -Invoke Write-Information -ParameterFilter { $MessageData -eq ("`n::error::{0}" -f $errorMessage) }
            Should -Not -Invoke Write-Error
        }
    }

    Context 'When running in Azure DevOps' {
        
        It 'should log the correctly formatted message' {
            
            $IsGitHubActions = $false
            $IsAzureDevOps = $true

            Write-ErrorLogMessage -Message $errorMessage

            Should -Invoke Write-Information -ParameterFilter { $MessageData -eq ("`n##[error]{0}" -f $errorMessage) }
            Should -Not -Invoke Write-Error
        }
    }

    Context 'When not running on a CI/CD platform' {
        
        It 'should log a standard non-terminating error message' {
            $IsGitHubActions = $false
            $IsAzureDevOps = $false

            Write-ErrorLogMessage -Message $errorMessage

            Should -Invoke Write-Error -ParameterFilter { $Message -eq $errorMessage }
            Should -Not -Invoke Write-Information
        }
    }

}