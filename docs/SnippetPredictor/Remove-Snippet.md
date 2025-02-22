---
document type: cmdlet
external help file: SnippetPredictor-Help.xml
HelpUri: ""
Locale: en-US
Module Name: SnippetPredictor
ms.date: 02-22-2025
PlatyPS schema version: 2024-05-01
title: Remove-Snippet
---

# Remove-Snippet

## SYNOPSIS

Remove specified snippets from the snippet configuration.

## SYNTAX

### \_\_AllParameterSets

```
Remove-Snippet [-Snippet] <string> [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Remove specified snippets from the snippet configuration.
The snippet configuration is located at `~/.snippet-predictor.json`.

## EXAMPLES

### Example 1

```powershell
Remove-Snippet "echo hello"
```

Remove the snippet.

### Example 2

```powershell
{echo hello} | Remove-Snippet
```

Remove the snippet.
You can pass code blocks because code blocks are implicitly converted to strings through the pipeline.

### Example 3

```powershell
Get-Snippet | ? tooltip -like *test* | Remove-Snippet
```

Remove snippets whose tooltip contains "test".

## PARAMETERS

### -Snippet

The text of the snippet to remove

```yaml
Type: System.String
DefaultValue: ""
SupportsWildcards: false
ParameterValue: []
Aliases: []
ParameterSets:
  - Name: (All)
    Position: 0
    IsRequired: true
    ValueFromPipeline: true
    ValueFromPipelineByPropertyName: true
    ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ""
```

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### System.String

The snippet string to be removed.

## OUTPUTS

### System.Object

No output.

## NOTES

## RELATED LINKS

{{ Fill in the related links here }}
