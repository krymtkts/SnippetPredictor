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
            @{ Expected = @('Disable-SnippetPredictorKeyHandler', 'Enable-SnippetPredictorKeyHandler', 'New-SnippetPredictorKeyHandler') }
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
        # NOTE: Use function keys for real PSReadLine bindings. Enum-style D0-D9 chord names
        # fall back to ConsoleKey parsing with KeyChar '\0'. On non-Windows, PSKeyInfo
        # normalizes '\0' to "@", so D0-D9 alias one binding; F1-F24 remain distinct.
        It 'Enable and Disable should register and remove custom completion bindings' {
            $bindings = [ordered]@{
                'Ctrl+Alt+Shift+F21' = 'SnippetPredictorTabCompleteNext'
                'Ctrl+Alt+Shift+F22' = 'SnippetPredictorTabCompletePrevious'
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

                Disable-SnippetPredictorKeyHandler

                foreach ($chord in $bindings.Keys) {
                    Get-PSReadLineKeyHandler -Chord $chord -ErrorAction SilentlyContinue |
                        Where-Object Function -CLike 'SnippetPredictorTabComplete*' |
                        Should -BeNullOrEmpty
                }
            }
            finally {
                Disable-SnippetPredictorKeyHandler -ErrorAction SilentlyContinue
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
        It 'Disable-SnippetPredictorKeyHandler should be idempotent' {
            {
                Disable-SnippetPredictorKeyHandler
                Disable-SnippetPredictorKeyHandler
            } | Should -Not -Throw
        }
        It 'Enable-SnippetPredictorKeyHandler should reject the same chord' {
            $chord = 'Ctrl+Alt+Shift+F23'

            try {
                Get-PSReadLineKeyHandler -Chord $chord -ErrorAction SilentlyContinue | Should -BeNullOrEmpty

                {
                    Enable-SnippetPredictorKeyHandler -NextChord $chord -PreviousChord $chord
                } | Should -Throw

                Get-PSReadLineKeyHandler -Chord $chord -ErrorAction SilentlyContinue | Should -BeNullOrEmpty
            }
            finally {
                Disable-SnippetPredictorKeyHandler -ErrorAction SilentlyContinue
                Remove-PSReadLineKeyHandler -Chord $chord -ErrorAction SilentlyContinue
            }
        }
        It 'Enable-SnippetPredictorKeyHandler should replace its previous custom bindings' {
            $firstChords = @('Ctrl+Alt+Shift+F17', 'Ctrl+Alt+Shift+F18')
            $secondChords = @('Ctrl+Alt+Shift+F19', 'Ctrl+Alt+Shift+F20')
            $allChords = $firstChords + $secondChords

            try {
                foreach ($chord in $allChords) {
                    Get-PSReadLineKeyHandler -Chord $chord -ErrorAction SilentlyContinue | Should -BeNullOrEmpty
                }

                Enable-SnippetPredictorKeyHandler -NextChord $firstChords[0] -PreviousChord $firstChords[1]
                Enable-SnippetPredictorKeyHandler -NextChord $secondChords[0] -PreviousChord $secondChords[1]

                foreach ($chord in $firstChords) {
                    Get-PSReadLineKeyHandler -Chord $chord -ErrorAction SilentlyContinue |
                        Where-Object Function -CLike 'SnippetPredictorTabComplete*' |
                        Should -BeNullOrEmpty
                }

                (Get-PSReadLineKeyHandler -Chord $secondChords[0]).Function | Should -Be 'SnippetPredictorTabCompleteNext'
                (Get-PSReadLineKeyHandler -Chord $secondChords[1]).Function | Should -Be 'SnippetPredictorTabCompletePrevious'
            }
            finally {
                Disable-SnippetPredictorKeyHandler -ErrorAction SilentlyContinue
                Remove-PSReadLineKeyHandler -Chord $allChords -ErrorAction SilentlyContinue
            }
        }
        It 'Disable-SnippetPredictorKeyHandler should preserve a later user binding' {
            $nextChord = 'Ctrl+Alt+Shift+F15'
            $previousChord = 'Ctrl+Alt+Shift+F16'
            $chords = @($nextChord, $previousChord)

            try {
                foreach ($chord in $chords) {
                    Get-PSReadLineKeyHandler -Chord $chord -ErrorAction SilentlyContinue | Should -BeNullOrEmpty
                }

                Enable-SnippetPredictorKeyHandler -NextChord $nextChord -PreviousChord $previousChord
                Set-PSReadLineKeyHandler -Chord $nextChord -ScriptBlock { } -BriefDescription UserOwnedAction

                Disable-SnippetPredictorKeyHandler

                (Get-PSReadLineKeyHandler -Chord $nextChord).Function | Should -Be 'UserOwnedAction'
                Get-PSReadLineKeyHandler -Chord $previousChord -ErrorAction SilentlyContinue |
                    Where-Object Function -CLike 'SnippetPredictorTabComplete*' |
                    Should -BeNullOrEmpty
            }
            finally {
                Disable-SnippetPredictorKeyHandler -ErrorAction SilentlyContinue
                Remove-PSReadLineKeyHandler -Chord $chords -ErrorAction SilentlyContinue
            }
        }
        It 'Enable-SnippetPredictorKeyHandler should rollback a partial registration' {
            InModuleScope SnippetPredictor.PSReadLine {
                $script:SnippetPredictorKeyHandlerBindings = @()

                Mock Set-PSReadLineKeyHandler {
                    if ($Chord -eq 'Ctrl+Alt+Shift+F31') {
                        throw 'registration failure'
                    }
                }
                Mock Get-PSReadLineKeyHandler {
                    [pscustomobject]@{ Function = 'SnippetPredictorTabCompleteNext' }
                } -ParameterFilter { $Chord -eq 'Ctrl+Alt+Shift+F30' }
                Mock Remove-PSReadLineKeyHandler

                {
                    Enable-SnippetPredictorKeyHandler `
                        -NextChord 'Ctrl+Alt+Shift+F30' `
                        -PreviousChord 'Ctrl+Alt+Shift+F31'
                } | Should -Throw

                Should -Invoke Remove-PSReadLineKeyHandler -Times 1 -Exactly -ParameterFilter {
                    $Chord -eq 'Ctrl+Alt+Shift+F30'
                }
                $script:SnippetPredictorKeyHandlerBindings | Should -BeNullOrEmpty
            }
        }
        It 'Enable-SnippetPredictorKeyHandler should retain a binding when rollback fails' {
            InModuleScope SnippetPredictor.PSReadLine {
                $script:SnippetPredictorKeyHandlerBindings = @()

                Mock Set-PSReadLineKeyHandler {
                    if ($Chord -eq 'Ctrl+Alt+Shift+D9') {
                        throw 'registration failure'
                    }
                }
                Mock Get-PSReadLineKeyHandler {
                    [pscustomobject]@{ Function = 'SnippetPredictorTabCompleteNext' }
                } -ParameterFilter { $Chord -eq 'Ctrl+Alt+Shift+D8' }
                Mock Remove-PSReadLineKeyHandler { throw 'rollback failure' }
                Mock Write-Error

                {
                    Enable-SnippetPredictorKeyHandler `
                        -NextChord 'Ctrl+Alt+Shift+D8' `
                        -PreviousChord 'Ctrl+Alt+Shift+D9'
                } | Should -Throw -ExpectedMessage 'registration failure'

                $script:SnippetPredictorKeyHandlerBindings.Chord | Should -Be 'Ctrl+Alt+Shift+D8'
                Should -Invoke Write-Error -Times 1 -Exactly
            }
        }
        It 'Disable-SnippetPredictorKeyHandler should restore baseline completion bindings' {
            InModuleScope SnippetPredictor.PSReadLine {
                $script:SnippetPredictorKeyHandlerBindings = @()

                Mock Set-PSReadLineKeyHandler
                Mock Get-PSReadLineKeyHandler {
                    $function = $Chord -ceq 'Tab' ? 'SnippetPredictorTabCompleteNext' : 'SnippetPredictorTabCompletePrevious'
                    [pscustomobject]@{ Function = $function }
                }
                Mock Remove-PSReadLineKeyHandler

                Enable-SnippetPredictorKeyHandler
                Disable-SnippetPredictorKeyHandler

                Should -Invoke Set-PSReadLineKeyHandler -Times 1 -Exactly -ParameterFilter {
                    $Chord -ceq 'Tab' -and $Function -eq 'TabCompleteNext'
                }
                Should -Invoke Set-PSReadLineKeyHandler -Times 1 -Exactly -ParameterFilter {
                    $Chord -ceq 'Shift+Tab' -and $Function -eq 'TabCompletePrevious'
                }
                Should -Not -Invoke Remove-PSReadLineKeyHandler
                $script:SnippetPredictorKeyHandlerBindings | Should -BeNullOrEmpty
            }
        }
        It 'Removing the module should clean up custom completion bindings' {
            $modulePath = (Get-Module SnippetPredictor).Path
            $chords = @('Ctrl+Alt+Shift+F13', 'Ctrl+Alt+Shift+F14')

            try {
                foreach ($chord in $chords) {
                    Get-PSReadLineKeyHandler -Chord $chord -ErrorAction SilentlyContinue | Should -BeNullOrEmpty
                }

                Enable-SnippetPredictorKeyHandler -NextChord $chords[0] -PreviousChord $chords[1]
                Remove-Module SnippetPredictor -Force

                foreach ($chord in $chords) {
                    Get-PSReadLineKeyHandler -Chord $chord -ErrorAction SilentlyContinue |
                        Where-Object Function -CLike 'SnippetPredictorTabComplete*' |
                        Should -BeNullOrEmpty
                }
            }
            finally {
                Remove-PSReadLineKeyHandler -Chord $chords -ErrorAction SilentlyContinue
                if (-not (Get-Module SnippetPredictor)) {
                    Import-Module $modulePath -Global
                }
            }
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
