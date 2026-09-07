$script:SnippetPredictorKeyHandlerSession = $null
$script:SnippetPredictorPredictionKeyHandlerSession = $null
$script:SnippetPredictorKeyHandlerBindings = @()
$script:SnippetPredictorDefaultFunctionsByChord = @{
    'Tab' = 'TabCompleteNext'
    'Shift+Tab' = 'TabCompletePrevious'
    'Enter' = 'AcceptLine'
}

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

function Get-SnippetPredictorBufferState {
    [CmdletBinding()]
    param()

    $line = $null
    $cursor = 0
    [Microsoft.PowerShell.PSConsoleReadLine]::GetBufferState(
        [ref] $line,
        [ref] $cursor
    )

    [pscustomobject]@{
        Line = $line
        Cursor = $cursor
    }
}

function Get-SnippetPredictorAcceptCandidates {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string] $Line
    )

    [string[]] $snippets = [SnippetPredictor.Integration]::GetExactIdentifierSnippetTexts($Line)
    if ($snippets.Count -gt 0) {
        return [pscustomobject]@{
            IsExactIdentifier = $true
            Texts = $snippets
        }
    }

    [string[]] $identifiers = @(
        [SnippetPredictor.Integration]::GetCompletionTexts($Line) |
            Where-Object { $_ -cne $Line.Trim() }
    )
    [pscustomobject]@{
        IsExactIdentifier = $false
        Texts = $identifiers
    }
}

function Invoke-SnippetPredictorReplace {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [int] $Start,
        [Parameter(Mandatory)]
        [int] $Length,
        [Parameter(Mandatory)]
        [string] $Replacement
    )

    [Microsoft.PowerShell.PSConsoleReadLine]::Replace(
        $Start,
        $Length,
        $Replacement
    )
}

function Invoke-SnippetPredictorNextSuggestion {
    [CmdletBinding()]
    param(
        $Key,
        $Arg
    )

    [Microsoft.PowerShell.PSConsoleReadLine]::NextSuggestion($Key, $Arg)
}

function Invoke-SnippetPredictorAcceptLine {
    [CmdletBinding()]
    param(
        $Key,
        $Arg
    )

    [Microsoft.PowerShell.PSConsoleReadLine]::AcceptLine($Key, $Arg)
}

function Invoke-SnippetPredictorAcceptKeyHandler {
    [CmdletBinding()]
    param(
        $Key,
        $Arg
    )

    $bufferState = Get-SnippetPredictorBufferState
    $line = $bufferState.Line
    $cursor = $bufferState.Cursor

    if ([string]::IsNullOrEmpty($line)) {
        return $false
    }

    if ($cursor -ne $line.Length) {
        return $false
    }

    $candidates = Get-SnippetPredictorAcceptCandidates -Line $line

    if ($candidates.IsExactIdentifier -and $candidates.Texts.Count -gt 1) {
        Invoke-SnippetPredictorNextSuggestion -Key $Key -Arg $Arg
        return $true
    }

    if ($candidates.Texts.Count -gt 0) {
        Invoke-SnippetPredictorReplace `
            -Start 0 `
            -Length $line.Length `
            -Replacement $candidates.Texts[0]
        return $true
    }

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

$script:SnippetPredictorAcceptHandler = {
    param($key, $arg)

    if (-not (Invoke-SnippetPredictorAcceptKeyHandler -Key $key -Arg $arg)) {
        Invoke-SnippetPredictorAcceptLine -Key $key -Arg $arg
    }
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

function Test-SnippetPredictorKeyHandlerBinding {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [pscustomobject] $Binding
    )

    $current = Get-PSReadLineKeyHandler -Chord $Binding.Chord -ErrorAction SilentlyContinue |
        Where-Object Function -CEQ $Binding.BriefDescription

    return $null -ne $current
}

function Remove-SnippetPredictorKeyHandlerBinding {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [pscustomobject] $Binding
    )

    if (-not (Test-SnippetPredictorKeyHandlerBinding -Binding $Binding)) {
        return
    }

    $parameters = @{
        Chord = $Binding.Chord
        ErrorAction = 'Stop'
    }
    if ($null -ne $Binding.ViMode) {
        $parameters.ViMode = $Binding.ViMode
    }
    if ($null -ne $Binding.RestoreFunction) {
        $parameters.Function = $Binding.RestoreFunction
        Set-PSReadLineKeyHandler @parameters
    }
    else {
        Remove-PSReadLineKeyHandler @parameters
    }
}

function Remove-SnippetPredictorKeyHandlerBindings {
    [CmdletBinding()]
    param()

    $remainingBindings = [System.Collections.Generic.List[object]]::new()
    $cleanupErrors = [System.Collections.Generic.List[System.Management.Automation.ErrorRecord]]::new()

    foreach ($binding in $script:SnippetPredictorKeyHandlerBindings) {
        try {
            Remove-SnippetPredictorKeyHandlerBinding -Binding $binding
        }
        catch {
            $remainingBindings.Add($binding)
            $cleanupErrors.Add($_)
        }
    }

    $script:SnippetPredictorKeyHandlerBindings = $remainingBindings.ToArray()
    $script:SnippetPredictorKeyHandlerSession = $null

    if ($cleanupErrors.Count -gt 0) {
        $PSCmdlet.ThrowTerminatingError($cleanupErrors[0])
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

function Disable-SnippetPredictorKeyHandler {
    [CmdletBinding()]
    param()

    Remove-SnippetPredictorKeyHandlerBindings
}

function Enable-SnippetPredictorKeyHandler {
    [CmdletBinding()]
    param(
        [string] $NextChord = 'Tab',
        [string] $PreviousChord = 'Shift+Tab',
        [ValidateNotNullOrWhiteSpace()]
        [string] $AcceptChord
    )

    $acceptChordSpecified = $PSBoundParameters.ContainsKey('AcceptChord')

    if ($NextChord -ieq $PreviousChord) {
        throw 'NextChord and PreviousChord must be different.'
    }
    if ($acceptChordSpecified -and ($AcceptChord -ieq $NextChord -or $AcceptChord -ieq $PreviousChord)) {
        throw 'AcceptChord must be different from NextChord and PreviousChord.'
    }

    Remove-SnippetPredictorKeyHandlerBindings
    $script:SnippetPredictorKeyHandlerSession = $null

    $acceptViMode = if ($acceptChordSpecified -and (Get-PSReadLineOption).EditMode.ToString() -ceq 'Vi') {
        'Insert'
    }

    $bindings = @(
        [pscustomobject]@{
            Chord = $NextChord
            BriefDescription = 'SnippetPredictorTabCompleteNext'
            Description = 'Complete SnippetPredictor input using the next candidate'
            ScriptBlock = $script:SnippetPredictorTabCompleteNextHandler
            ViMode = $null
            RestoreFunction = $script:SnippetPredictorDefaultFunctionsByChord[$NextChord]
        }
        if ($acceptChordSpecified) {
            [pscustomobject]@{
                Chord = $AcceptChord
                BriefDescription = 'SnippetPredictorAccept'
                Description = 'Expand or search an exact SnippetPredictor identifier'
                ScriptBlock = $script:SnippetPredictorAcceptHandler
                ViMode = $acceptViMode
                RestoreFunction = $script:SnippetPredictorDefaultFunctionsByChord[$AcceptChord]
            }
        }
        [pscustomobject]@{
            Chord = $PreviousChord
            BriefDescription = 'SnippetPredictorTabCompletePrevious'
            Description = 'Complete SnippetPredictor input using the previous candidate'
            ScriptBlock = $script:SnippetPredictorTabCompletePreviousHandler
            ViMode = $null
            RestoreFunction = $script:SnippetPredictorDefaultFunctionsByChord[$PreviousChord]
        }
    )
    $registeredBindings = [System.Collections.Generic.List[object]]::new()

    try {
        foreach ($binding in $bindings) {
            $parameters = @{
                Chord = $binding.Chord
                ScriptBlock = $binding.ScriptBlock
                BriefDescription = $binding.BriefDescription
                Description = $binding.Description
                ErrorAction = 'Stop'
            }
            if ($null -ne $binding.ViMode) {
                $parameters.ViMode = $binding.ViMode
            }

            Set-PSReadLineKeyHandler @parameters
            $registeredBindings.Add($binding)
        }
    }
    catch {
        $registrationError = $_
        $remainingBindings = [System.Collections.Generic.List[object]]::new()

        foreach ($binding in $registeredBindings) {
            try {
                Remove-SnippetPredictorKeyHandlerBinding -Binding $binding
            }
            catch {
                $remainingBindings.Add($binding)
                Write-Error -ErrorRecord $_ -ErrorAction Continue
            }
        }

        $script:SnippetPredictorKeyHandlerBindings = $remainingBindings.ToArray()
        throw $registrationError
    }

    $script:SnippetPredictorKeyHandlerBindings = $bindings
}

$ExecutionContext.SessionState.Module.OnRemove += {
    Remove-SnippetPredictorKeyHandlerBindings
}

Export-ModuleMember -Function @(
    'Disable-SnippetPredictorKeyHandler'
    'Enable-SnippetPredictorKeyHandler'
    'New-SnippetPredictorKeyHandler'
)
