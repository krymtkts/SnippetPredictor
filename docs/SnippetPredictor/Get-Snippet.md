---
document type: cmdlet
external help file: SnippetPredictor.dll-Help.xml
HelpUri: ""
Locale: en-US
Module Name: SnippetPredictor
ms.date: 02-22-2025
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
The snippet configuration is located at `~/.snippet-predictor.json`.

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

{{ Fill in the related links here }}
