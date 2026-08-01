---
document type: cmdlet
external help file: SnippetPredictor-Help.xml
HelpUri: https://github.com/krymtkts/SnippetPredictor/blob/main/docs/SnippetPredictor/Enable-SnippetPredictorKeyHandler.md
Locale: en-US
Module Name: SnippetPredictor
ms.date: 08-01-2026
PlatyPS schema version: 2024-05-01
title: Enable-SnippetPredictorKeyHandler
---

# Enable-SnippetPredictorKeyHandler

## SYNOPSIS

Registers opt-in PSReadLine key handlers for SnippetPredictor.

## SYNTAX

### __AllParameterSets

```
Enable-SnippetPredictorKeyHandler [[-NextChord] <string>] [[-PreviousChord] <string>]
 [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Registers PSReadLine key handlers that complete `:snp` input with Tab and Shift+Tab.
Unsupported Tab input delegates to standard PSReadLine completion.

The command doesn't change the prediction ListView bindings.
Use the standard PSReadLine UpArrow and DownArrow bindings to navigate the ListView.

The command overwrites existing bindings for the selected chords.
The bindings apply to the current PowerShell session.

## EXAMPLES

### Example 1

```powershell
Enable-SnippetPredictorKeyHandler
```

Registers the default Tab and Shift+Tab completion bindings.

## PARAMETERS

### -NextChord

Specifies the chord that selects the next snippet completion.

```yaml
Type: System.String
DefaultValue: Tab
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
AcceptedValues: []
HelpMessage: ""
```

### -PreviousChord

Specifies the chord that selects the previous snippet completion.

```yaml
Type: System.String
DefaultValue: Shift+Tab
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: (All)
    Position: 1
    IsRequired: false
    ValueFromPipeline: false
    ValueFromPipelineByPropertyName: false
    ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ""
```

### CommonParameters

This cmdlet supports the common parameters.
For details, see [about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### System.Object

No output.

## NOTES

The command doesn't change the PSReadLine prediction source or view style.

## RELATED LINKS

- [New-SnippetPredictorKeyHandler](New-SnippetPredictorKeyHandler.md)
- [SnippetPredictor.md](SnippetPredictor.md)
