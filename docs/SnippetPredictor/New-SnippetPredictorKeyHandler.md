---
document type: cmdlet
external help file: SnippetPredictor-Help.xml
HelpUri: https://github.com/krymtkts/SnippetPredictor/blob/main/docs/SnippetPredictor/New-SnippetPredictorKeyHandler.md
Locale: en-US
Module Name: SnippetPredictor
ms.date: 08-01-2026
PlatyPS schema version: 2024-05-01
title: New-SnippetPredictorKeyHandler
---

# New-SnippetPredictorKeyHandler

## SYNOPSIS

Returns a composable SnippetPredictor key handler.

## SYNTAX

### __AllParameterSets

```
New-SnippetPredictorKeyHandler [[-Action] <string>] [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Returns a fixed script block for the selected SnippetPredictor action.
The script block returns `$true` when SnippetPredictor handles the current input.
It returns `$false` when the input is outside the supported scope.
In that case, it doesn't invoke fallback completion.

The Tab completion actions complete `:snp` and configured group identifiers.
They accept `:` or a partial identifier.
They complete snippets after an exact `:snp` or group identifier.
The `:tip` identifier isn't included.
For one candidate, the handler ends the candidate session.
The next Tab or Shift+Tab starts a new completion lookup.

The caller must consume the Boolean result and decide which fallback action to invoke.
Do not register the returned script block directly with `Set-PSReadLineKeyHandler`.
`Enable-SnippetPredictorKeyHandler` doesn't register the prediction navigation actions.

## EXAMPLES

### Example 1

```powershell
$handler = New-SnippetPredictorKeyHandler -Action NextSuggestion
```

Returns the fixed handler for selecting the next prediction.

## PARAMETERS

### -Action

Specifies the SnippetPredictor action implemented by the returned script block.

```yaml
Type: System.String
DefaultValue: TabCompleteNext
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: (All)
    Position: 0
    IsRequired: false
    ValueFromPipeline: false
    ValueFromPipelineByPropertyName: false
    ValueFromRemainingArguments: false
DontShow: false
AcceptedValues:
  - TabCompleteNext
  - TabCompletePrevious
  - NextSuggestion
  - PreviousSuggestion
HelpMessage: ""
```

### CommonParameters

This cmdlet supports the common parameters.
For details, see [about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### System.Object

A fixed `System.Management.Automation.ScriptBlock`.
The script block returns whether SnippetPredictor handled the input.

## NOTES

The returned script block belongs to the SnippetPredictor script module.
It shares the module's handler state.
Tab completion requires the cursor at the end of the line.
Every character before the identifier must be whitespace.
Identifier matching is case-sensitive.
Snippet matching follows the `SearchCaseSensitive` configuration value.

## RELATED LINKS

- [Enable-SnippetPredictorKeyHandler](Enable-SnippetPredictorKeyHandler.md)
- [SnippetPredictor.md](SnippetPredictor.md)
