Describe 'SnippetPredictor' {
    Context 'SnippetPredictor module' {
        It 'Given the SnippetPredictor module, it should have a nonzero version' {
            $m = Get-Module 'SnippetPredictor'
            $m.Version | Should -Not -Be $null
        }
        It 'Given the SnippetPredictor module, it should have <Expected> commands' -TestCases @(
            @{ Expected = @('Add-Snippet', 'Get-Snippet', 'Remove-Snippet') }
        ) {
            $m = Get-Module 'SnippetPredictor'
            ($m.ExportedCmdlets).Values | Select-Object -ExpandProperty Name | Should -Eq $Expected
        }
        It 'Given the SnippetPredictor module, it should have <Expected> functions' -TestCases @(
            @{ Expected = @('Enable-SnippetPredictorKeyHandler', 'New-SnippetPredictorKeyHandler') }
        ) {
            $m = Get-Module 'SnippetPredictor'
            ($m.ExportedFunctions).Values | Select-Object -ExpandProperty Name | Should -Eq $Expected
        }
        It 'Given <Fallback>, New-SnippetPredictorKeyHandler should return a scriptblock' -TestCases @(
            @{ Fallback = 'TabCompleteNext' }
            @{ Fallback = 'TabCompletePrevious' }
        ) {
            New-SnippetPredictorKeyHandler -Fallback $Fallback | Should -BeOfType ([scriptblock])
        }
        It 'Given <Fallback>, New-SnippetPredictorKeyHandler should return the fixed scriptblock' -TestCases @(
            @{ Fallback = 'TabCompleteNext' }
            @{ Fallback = 'TabCompletePrevious' }
        ) {
            $first = New-SnippetPredictorKeyHandler -Fallback $Fallback
            $second = New-SnippetPredictorKeyHandler -Fallback $Fallback

            [object]::ReferenceEquals($first, $second) | Should -BeTrue
        }
        It 'Given different fallbacks, New-SnippetPredictorKeyHandler should return different scriptblocks' {
            $next = New-SnippetPredictorKeyHandler -Fallback TabCompleteNext
            $previous = New-SnippetPredictorKeyHandler -Fallback TabCompletePrevious

            [object]::ReferenceEquals($next, $previous) | Should -BeFalse
        }
    }
    BeforeAll {
        $originalConfigPath = $env:SNIPPET_PREDICTOR_CONFIG
        $env:SNIPPET_PREDICTOR_CONFIG = $PSScriptRoot
    }
    AfterAll {
        # Remove-Module -Name 'SnippetPredictor' -Force
        Remove-Item Env:SNIPPET_PREDICTOR_CONFIG -ErrorAction SilentlyContinue
        if ($originalConfigPath) {
            $env:SNIPPET_PREDICTOR_CONFIG = $originalConfigPath
        }
    }
    Context 'Get-Snippet' {
        It 'Given the Get-Snippet command, it should return a snippet' {
            Get-Snippet | Should -Be $null
        }
    }
    Context 'Add-Snippet' {
        It 'Given the Add-Snippet command, it should add a snippet' {
            Add-Snippet 'echo Hello' 'say Hello'
            $snippets = Get-Snippet
            $snippets.Count | Should -Be 1
            $snippets[0].Snippet | Should -Be 'echo Hello'
            $snippets[0].Tooltip | Should -Be 'say Hello'
        }
    }
    Context 'Remove-Snippet' {
        It 'Given the Remove-Snippet command, it should remove a snippet' {
            Remove-Snippet 'echo Hello'
            Get-Snippet | Should -Be $null
        }
    }
}
