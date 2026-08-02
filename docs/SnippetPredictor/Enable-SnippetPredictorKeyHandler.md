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

Registers PSReadLine key handlers that complete SnippetPredictor input with Tab and Shift+Tab.

For `:` or a partial identifier, the handlers complete `:snp` and matching group identifiers.
The `:tip` identifier isn't included.
For an exact `:snp` or group identifier, the handlers complete matching snippets.

Repeated Tab or Shift+Tab cycles through matching candidates.
For one candidate, the next Tab or Shift+Tab starts a new lookup using the replaced input.
For example, press Tab twice after `:sn`.
The first press completes `:snp`; the second starts snippet completion.

The handlers require the cursor at the end of the line.
Every character before the identifier must be whitespace.
Unsupported input delegates to standard PSReadLine completion.

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
Type `:` and press Tab to cycle through `:snp` and configured group identifiers.

## PARAMETERS

### -NextChord

Specifies the chord that selects the next identifier or snippet completion.

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

Specifies the chord that selects the previous identifier or snippet completion.

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
Identifier matching is case-sensitive.
Snippet matching follows the `SearchCaseSensitive` configuration value.
Input such as `x :` isn't handled because a non-whitespace character precedes the identifier.

## RELATED LINKS

- [New-SnippetPredictorKeyHandler](New-SnippetPredictorKeyHandler.md)
- [SnippetPredictor.md](SnippetPredictor.md)
