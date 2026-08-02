$script:SnippetPredictorKeyHandlerSession = $null
$script:SnippetPredictorPredictionKeyHandlerSession = $null

function Invoke-SnippetPredictorKeyHandler {
    [CmdletBinding()]
    param(
        [ValidateSet(-1, 1)]
        [int] $Direction
    )

    $line = $null
    $cursor = 0
    [Microsoft.PowerShell.PSConsoleReadLine]::GetBufferState(
        [ref] $line,
        [ref] $cursor
    )

    $session = $script:SnippetPredictorKeyHandlerSession
    $isCursorAtEnd = $cursor -eq $line.Length

    if (
        $isCursorAtEnd -and
        $null -ne $session -and
        $line -eq $session.LastReplacement -and
        $session.Matches.Count -gt 0
    ) {
        $nextIndex = ($session.Index + $Direction + $session.Matches.Count) % $session.Matches.Count
        $nextReplacement = $session.Matches[$nextIndex]

        [Microsoft.PowerShell.PSConsoleReadLine]::Replace(
            0,
            $line.Length,
            $nextReplacement
        )

        $script:SnippetPredictorKeyHandlerSession = [pscustomobject]@{
            Matches = $session.Matches
            Index = $nextIndex
            LastReplacement = $nextReplacement
        }
        return $true
    }

    [string[]] $completions = $isCursorAtEnd ? [SnippetPredictor.Integration]::GetCompletionTexts($line) : $null

    if ($completions.Count -gt 0) {
        $index = $Direction -lt 0 ? $completions.Count - 1 : 0
        $replacement = $completions[$index]

        [Microsoft.PowerShell.PSConsoleReadLine]::Replace(
            0,
            $line.Length,
            $replacement
        )

        $script:SnippetPredictorKeyHandlerSession = if ($completions.Count -gt 1) {
            [pscustomobject]@{
                Matches = $completions
                Index = $index
                LastReplacement = $replacement
            }
        }
        else {
            $null
        }
        return $true
    }

    $script:SnippetPredictorKeyHandlerSession = $null
    return $false
}

function Invoke-SnippetPredictorPredictionKeyHandler {
    [CmdletBinding()]
    param(
        $Key,
        $Arg,
        [ValidateSet(-1, 1)]
        [int] $Direction
    )

    $line = $null
    $cursor = 0
    [Microsoft.PowerShell.PSConsoleReadLine]::GetBufferState(
        [ref] $line,
        [ref] $cursor
    )

    $session = $script:SnippetPredictorPredictionKeyHandlerSession
    $isCursorAtEnd = $cursor -eq $line.Length
    $isContinuation = (
        $isCursorAtEnd -and
        $null -ne $session -and
        $line -eq $session.LastReplacement
    )
    [string[]] $completions = if ($isCursorAtEnd -and -not $isContinuation) {
        [SnippetPredictor.Integration]::GetCompletionTexts($line)
    }

    if ($isContinuation -or $completions.Count -gt 0) {
        if ($Direction -lt 0) {
            [Microsoft.PowerShell.PSConsoleReadLine]::PreviousSuggestion($Key, $Arg)
        }
        else {
            [Microsoft.PowerShell.PSConsoleReadLine]::NextSuggestion($Key, $Arg)
        }

        [Microsoft.PowerShell.PSConsoleReadLine]::GetBufferState(
            [ref] $line,
            [ref] $cursor
        )
        $script:SnippetPredictorPredictionKeyHandlerSession = [pscustomobject]@{
            LastReplacement = $line
        }
        return $true
    }

    $script:SnippetPredictorPredictionKeyHandlerSession = $null
    return $false
}

$script:SnippetPredictorTabCompleteNextComposableHandler = {
    param($key, $arg)

    Invoke-SnippetPredictorKeyHandler -Direction 1
}

$script:SnippetPredictorTabCompletePreviousComposableHandler = {
    param($key, $arg)

    Invoke-SnippetPredictorKeyHandler -Direction -1
}

$script:SnippetPredictorNextSuggestionComposableHandler = {
    param($key, $arg)

    Invoke-SnippetPredictorPredictionKeyHandler -Key $key -Arg $arg -Direction 1
}

$script:SnippetPredictorPreviousSuggestionComposableHandler = {
    param($key, $arg)

    Invoke-SnippetPredictorPredictionKeyHandler -Key $key -Arg $arg -Direction -1
}

$script:SnippetPredictorTabCompleteNextHandler = {
    param($key, $arg)

    if (-not (& $script:SnippetPredictorTabCompleteNextComposableHandler $key $arg)) {
        [Microsoft.PowerShell.PSConsoleReadLine]::TabCompleteNext($key, $arg)
    }
}

$script:SnippetPredictorTabCompletePreviousHandler = {
    param($key, $arg)

    if (-not (& $script:SnippetPredictorTabCompletePreviousComposableHandler $key $arg)) {
        [Microsoft.PowerShell.PSConsoleReadLine]::TabCompletePrevious($key, $arg)
    }
}

function New-SnippetPredictorKeyHandler {
    [CmdletBinding()]
    param(
        [ValidateSet(
            'TabCompleteNext',
            'TabCompletePrevious',
            'NextSuggestion',
            'PreviousSuggestion'
        )]
        [string] $Action = 'TabCompleteNext'
    )

    switch ($Action) {
        'TabCompletePrevious' {
            $script:SnippetPredictorTabCompletePreviousComposableHandler
        }
        'NextSuggestion' {
            $script:SnippetPredictorNextSuggestionComposableHandler
        }
        'PreviousSuggestion' {
            $script:SnippetPredictorPreviousSuggestionComposableHandler
        }
        default {
            $script:SnippetPredictorTabCompleteNextComposableHandler
        }
    }
}

function Enable-SnippetPredictorKeyHandler {
    [CmdletBinding()]
    param(
        [string] $NextChord = 'Tab',
        [string] $PreviousChord = 'Shift+Tab'
    )

    $script:SnippetPredictorKeyHandlerSession = $null

    Set-PSReadLineKeyHandler -Chord $NextChord -ScriptBlock $script:SnippetPredictorTabCompleteNextHandler
    Set-PSReadLineKeyHandler -Chord $PreviousChord -ScriptBlock $script:SnippetPredictorTabCompletePreviousHandler
}

Export-ModuleMember -Function @(
    'Enable-SnippetPredictorKeyHandler'
    'New-SnippetPredictorKeyHandler'
)
