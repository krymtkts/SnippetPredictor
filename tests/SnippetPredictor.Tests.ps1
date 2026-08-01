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
        It 'Given <Action>, New-SnippetPredictorKeyHandler should return a scriptblock' -TestCases @(
            @{ Action = 'TabCompleteNext' }
            @{ Action = 'TabCompletePrevious' }
            @{ Action = 'NextSuggestion' }
            @{ Action = 'PreviousSuggestion' }
        ) {
            New-SnippetPredictorKeyHandler -Action $Action | Should -BeOfType ([scriptblock])
        }
        It 'Given <Action>, New-SnippetPredictorKeyHandler should return the fixed scriptblock' -TestCases @(
            @{ Action = 'TabCompleteNext' }
            @{ Action = 'TabCompletePrevious' }
            @{ Action = 'NextSuggestion' }
            @{ Action = 'PreviousSuggestion' }
        ) {
            $first = New-SnippetPredictorKeyHandler -Action $Action
            $second = New-SnippetPredictorKeyHandler -Action $Action

            [object]::ReferenceEquals($first, $second) | Should -BeTrue
        }
        It 'New-SnippetPredictorKeyHandler should return a different scriptblock for every action' {
            $actions = @(
                'TabCompleteNext'
                'TabCompletePrevious'
                'NextSuggestion'
                'PreviousSuggestion'
            )
            $handlers = $actions | ForEach-Object { New-SnippetPredictorKeyHandler -Action $_ }

            for ($left = 0; $left -lt $handlers.Count; $left++) {
                for ($right = $left + 1; $right -lt $handlers.Count; $right++) {
                    [object]::ReferenceEquals($handlers[$left], $handlers[$right]) | Should -BeFalse
                }
            }
        }
        It 'Given <Action>, the completion handler should return <CoreResult>' -TestCases @(
            @{ Action = 'TabCompleteNext'; ExpectedDirection = 1; CoreResult = $true }
            @{ Action = 'TabCompleteNext'; ExpectedDirection = 1; CoreResult = $false }
            @{ Action = 'TabCompletePrevious'; ExpectedDirection = -1; CoreResult = $true }
            @{ Action = 'TabCompletePrevious'; ExpectedDirection = -1; CoreResult = $false }
        ) {
            InModuleScope SnippetPredictor.PSReadLine -Parameters @{
                Action = $Action
                ExpectedDirection = $ExpectedDirection
                CoreResult = $CoreResult
            } {
                Mock Invoke-SnippetPredictorKeyHandler { $CoreResult }

                $handler = New-SnippetPredictorKeyHandler -Action $Action
                & $handler $null $null | Should -Be $CoreResult

                Should -Invoke Invoke-SnippetPredictorKeyHandler -Times 1 -Exactly -ParameterFilter {
                    $Direction -eq $ExpectedDirection
                }
            }
        }
        It 'Given <Action>, the prediction handler should return <CoreResult>' -TestCases @(
            @{ Action = 'NextSuggestion'; ExpectedDirection = 1; CoreResult = $true }
            @{ Action = 'NextSuggestion'; ExpectedDirection = 1; CoreResult = $false }
            @{ Action = 'PreviousSuggestion'; ExpectedDirection = -1; CoreResult = $true }
            @{ Action = 'PreviousSuggestion'; ExpectedDirection = -1; CoreResult = $false }
        ) {
            InModuleScope SnippetPredictor.PSReadLine -Parameters @{
                Action = $Action
                ExpectedDirection = $ExpectedDirection
                CoreResult = $CoreResult
            } {
                Mock Invoke-SnippetPredictorPredictionKeyHandler { $CoreResult }

                $handler = New-SnippetPredictorKeyHandler -Action $Action
                & $handler 'key' 'arg' | Should -Be $CoreResult

                Should -Invoke Invoke-SnippetPredictorPredictionKeyHandler -Times 1 -Exactly -ParameterFilter {
                    $Key -eq 'key' -and $Arg -eq 'arg' -and $Direction -eq $ExpectedDirection
                }
            }
        }
        It 'Enable-SnippetPredictorKeyHandler should register completion bindings' {
            $bindings = [ordered]@{
                'Ctrl+Alt+Shift+F21' = 'CustomAction'
                'Ctrl+Alt+Shift+F22' = 'CustomAction'
            }
            $unboundChords = @()

            try {
                foreach ($chord in $bindings.Keys) {
                    $existing = Get-PSReadLineKeyHandler -Chord $chord -ErrorAction SilentlyContinue
                    $existing | Should -BeNullOrEmpty
                    $unboundChords += $chord
                }

                Enable-SnippetPredictorKeyHandler `
                    -NextChord ($bindings.Keys)[0] `
                    -PreviousChord ($bindings.Keys)[1]

                foreach ($entry in $bindings.GetEnumerator()) {
                    (Get-PSReadLineKeyHandler -Chord $entry.Key).Function | Should -Be $entry.Value
                }
            }
            finally {
                if ($unboundChords.Count -gt 0) {
                    Remove-PSReadLineKeyHandler -Chord $unboundChords -ErrorAction SilentlyContinue
                }
            }
        }
        It 'Enable-SnippetPredictorKeyHandler should expose only completion chord parameters' {
            $parameters = (Get-Command Enable-SnippetPredictorKeyHandler).Parameters

            $parameters.Keys | Should -Contain 'NextChord'
            $parameters.Keys | Should -Contain 'PreviousChord'
            $parameters.Keys | Should -Not -Contain 'NextSuggestionChord'
            $parameters.Keys | Should -Not -Contain 'PreviousSuggestionChord'
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
