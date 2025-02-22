# Based on the `CodeFormatting.psd1` settings for PSScriptAnalyzer 1.23.0.
# https://github.com/PowerShell/PSScriptAnalyzer/blob/1.23.0/Engine/Settings/CodeFormatting.psd1
@{
    IncludeRules = @(
        'PSPlaceOpenBrace',
        'PSPlaceCloseBrace',
        'PSUseConsistentWhitespace',
        'PSUseConsistentIndentation',
        'PSAlignAssignmentStatement',
        'PSUseCorrectCasing'
    )

    Rules = @{

        PSPlaceOpenBrace = @{
            # Disable alignment of assignment statements.
            Enable = $true
            OnSameLine = $true
            NewLineAfter = $true
            IgnoreOneLineBlock = $true
        }

        PSPlaceCloseBrace = @{
            Enable = $true
            NewLineAfter = $true
            IgnoreOneLineBlock = $true
            NoEmptyLineBefore = $false
        }

        PSUseConsistentIndentation = @{
            Enable = $true
            Kind = 'space'
            PipelineIndentation = 'IncreaseIndentationForFirstPipeline'
            IndentationSize = 4
        }

        PSUseConsistentWhitespace = @{
            Enable = $true
            CheckInnerBrace = $true
            CheckOpenBrace = $true
            CheckOpenParen = $true
            CheckOperator = $true
            CheckPipe = $true
            CheckPipeForRedundantWhitespace = $false
            CheckSeparator = $true
            CheckParameter = $false
            IgnoreAssignmentOperatorInsideHashTable = $true
        }

        PSAlignAssignmentStatement = @{
            # Disable alignment of assignment statements.
            Enable = $false
            CheckHashtable = $true
        }

        PSUseCorrectCasing = @{
            Enable = $true
        }
    }
}
