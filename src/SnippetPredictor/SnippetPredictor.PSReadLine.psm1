$script:SnippetPredictorKeyHandlerSession = $null

function Invoke-SnippetPredictorKeyHandler {
    [CmdletBinding()]
    param(
        $Key,
        $Arg,
        [ValidateSet(-1, 1)]
        [int] $Direction,
        [ValidateSet('TabCompleteNext', 'TabCompletePrevious')]
        [string] $Fallback
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
        return
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

        $script:SnippetPredictorKeyHandlerSession = [pscustomobject]@{
            Matches = $completions
            Index = $index
            LastReplacement = $replacement
        }
        return
    }

    $script:SnippetPredictorKeyHandlerSession = $null

    switch ($Fallback) {
        'TabCompletePrevious' {
            [Microsoft.PowerShell.PSConsoleReadLine]::TabCompletePrevious($Key, $Arg)
            return
        }
        'TabCompleteNext' {
            [Microsoft.PowerShell.PSConsoleReadLine]::TabCompleteNext($Key, $Arg)
            return
        }
    }
}

$script:SnippetPredictorTabCompleteNextHandler = {
    param($key, $arg)

    Invoke-SnippetPredictorKeyHandler -Key $key -Arg $arg -Direction 1 -Fallback TabCompleteNext
}

$script:SnippetPredictorTabCompletePreviousHandler = {
    param($key, $arg)

    Invoke-SnippetPredictorKeyHandler -Key $key -Arg $arg -Direction -1 -Fallback TabCompletePrevious
}

function New-SnippetPredictorKeyHandler {
    [CmdletBinding()]
    param(
        [ValidateSet('TabCompleteNext', 'TabCompletePrevious')]
        [string] $Fallback = 'TabCompleteNext'
    )

    $Fallback -eq 'TabCompletePrevious' ? $script:SnippetPredictorTabCompletePreviousHandler : $script:SnippetPredictorTabCompleteNextHandler
}

function Enable-SnippetPredictorKeyHandler {
    [CmdletBinding()]
    param(
        [string] $NextChord = 'Tab',
        [string] $PreviousChord = 'Shift+Tab'
    )

    $script:SnippetPredictorKeyHandlerSession = $null

    Set-PSReadLineKeyHandler -Chord $NextChord -ScriptBlock (
        New-SnippetPredictorKeyHandler -Fallback TabCompleteNext
    )

    Set-PSReadLineKeyHandler -Chord $PreviousChord -ScriptBlock (
        New-SnippetPredictorKeyHandler -Fallback TabCompletePrevious
    )
}

Export-ModuleMember -Function @(
    'Enable-SnippetPredictorKeyHandler'
    'New-SnippetPredictorKeyHandler'
)
