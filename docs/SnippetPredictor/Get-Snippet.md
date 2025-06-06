﻿---
document type: cmdlet
external help file: SnippetPredictor-Help.xml
HelpUri: https://github.com/krymtkts/SnippetPredictor/blob/main/docs/SnippetPredictor/Get-Snippet.md
Locale: en-US
Module Name: SnippetPredictor
ms.date: 05-10-2025
PlatyPS schema version: 2024-05-01
title: Get-Snippet
---

# Get-Snippet

## SYNOPSIS

Retrieves a saved snippets.

## SYNTAX

### \_\_AllParameterSets

```
Get-Snippet [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Retrieves a saved snippets.
The save snippets stored in the snippet configuration.
The snippet configuration resides in `~/.snippet-predictor.json`.

## EXAMPLES

### Example 1

```powershell
Get-Snippet
```

Retrieves a saved snippets.

## PARAMETERS

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### SnippetPredictor.SnippetEntry

A record containing a snippet and its tooltip.

## NOTES

## RELATED LINKS

- [SnippetPredictor.md](https://github.com/krymtkts/SnippetPredictor/blob/main/docs/SnippetPredictor/SnippetPredictor.md)
