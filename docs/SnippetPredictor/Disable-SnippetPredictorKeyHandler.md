---
document type: cmdlet
external help file: SnippetPredictor-Help.xml
HelpUri: https://github.com/krymtkts/SnippetPredictor/blob/main/docs/SnippetPredictor/Disable-SnippetPredictorKeyHandler.md
Locale: en-US
Module Name: SnippetPredictor
ms.date: 08-02-2026
PlatyPS schema version: 2024-05-01
title: Disable-SnippetPredictorKeyHandler
---

# Disable-SnippetPredictorKeyHandler

## SYNOPSIS

Removes PSReadLine key bindings registered by SnippetPredictor.

## SYNTAX

### __AllParameterSets

```
Disable-SnippetPredictorKeyHandler [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Removes the PSReadLine completion bindings registered by `Enable-SnippetPredictorKeyHandler`.
The command checks ownership before cleanup and leaves user-owned bindings unchanged.
It preserves a binding that the user replaced after enabling the handlers.

For the enable command's default chords, the command restores the completion bindings.
It assigns `TabCompleteNext` to Tab and `TabCompletePrevious` to Shift+Tab.
The command removes bindings for custom chords.

The command doesn't restore a custom binding that `Enable-SnippetPredictorKeyHandler` overwrote.
PSReadLine doesn't expose the original script block required to restore an arbitrary custom action.

Removing the SnippetPredictor module performs the same cleanup automatically.
Calling this command more than once is safe.

## EXAMPLES

### Example 1

```powershell
Disable-SnippetPredictorKeyHandler
```

Removes the current SnippetPredictor completion bindings.

## PARAMETERS

### CommonParameters

This cmdlet supports the common parameters.
For details, see [about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### System.Object

No output.

## NOTES

The binding state applies to the current PowerShell session.
This command doesn't manage key bindings created by user code.
This includes bindings that use handlers returned by `New-SnippetPredictorKeyHandler`.
User code controls those bindings.

## RELATED LINKS

- [Enable-SnippetPredictorKeyHandler](https://github.com/krymtkts/SnippetPredictor/blob/main/docs/SnippetPredictor/Enable-SnippetPredictorKeyHandler.md)
- [New-SnippetPredictorKeyHandler](https://github.com/krymtkts/SnippetPredictor/blob/main/docs/SnippetPredictor/New-SnippetPredictorKeyHandler.md)
- [SnippetPredictor.md](https://github.com/krymtkts/SnippetPredictor/blob/main/docs/SnippetPredictor/SnippetPredictor.md)
