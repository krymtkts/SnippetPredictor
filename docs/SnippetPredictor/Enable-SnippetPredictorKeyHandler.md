---
document type: cmdlet
external help file: SnippetPredictor-Help.xml
HelpUri: https://github.com/krymtkts/SnippetPredictor/blob/main/docs/SnippetPredictor/Enable-SnippetPredictorKeyHandler.md
Locale: en-US
Module Name: SnippetPredictor
ms.date: 08-08-2026
PlatyPS schema version: 2024-05-01
title: Enable-SnippetPredictorKeyHandler
---

# Enable-SnippetPredictorKeyHandler

## SYNOPSIS

Registers opt-in PSReadLine key bindings for SnippetPredictor.

## SYNTAX

### __AllParameterSets

```
Enable-SnippetPredictorKeyHandler [[-NextChord] <string>] [[-PreviousChord] <string>]
 [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Registers PSReadLine key bindings that invoke SnippetPredictor completion handlers.
A chord is the key or sequence of keys assigned to a handler.

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
It doesn't preserve an arbitrary custom action for later restoration.
Enabling the handlers again first cleans up bindings from the previous call.
Use `Disable-SnippetPredictorKeyHandler` to remove the registered bindings explicitly.
Removing the SnippetPredictor module performs the same cleanup automatically.
The bindings apply to the current PowerShell session.

## EXAMPLES

### Example 1

```powershell
Enable-SnippetPredictorKeyHandler
```

Registers completion bindings for the default Tab and Shift+Tab chords.
Type `:` and press Tab to cycle through `:snp` and configured group identifiers.

### Example 2

```powershell
Enable-SnippetPredictorKeyHandler -NextChord Ctrl+j -PreviousChord Ctrl+k
Disable-SnippetPredictorKeyHandler
```

Registers and then removes completion bindings for custom chords.

## PARAMETERS

### -NextChord

Specifies the key or sequence of keys to bind to the next completion handler.

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

Specifies the key or sequence of keys to bind to the previous completion handler.

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
During cleanup, the command assigns `TabCompleteNext` to Tab and `TabCompletePrevious` to Shift+Tab.
The command removes bindings for other registered chords.
A binding replaced by the user after this command runs isn't changed during cleanup.

## RELATED LINKS

- [Disable-SnippetPredictorKeyHandler](https://github.com/krymtkts/SnippetPredictor/blob/main/docs/SnippetPredictor/Disable-SnippetPredictorKeyHandler.md)
- [New-SnippetPredictorKeyHandler](https://github.com/krymtkts/SnippetPredictor/blob/main/docs/SnippetPredictor/New-SnippetPredictorKeyHandler.md)
- [SnippetPredictor.md](https://github.com/krymtkts/SnippetPredictor/blob/main/docs/SnippetPredictor/SnippetPredictor.md)
