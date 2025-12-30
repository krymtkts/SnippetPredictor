---
document type: cmdlet
external help file: SnippetPredictor-Help.xml
HelpUri: https://github.com/krymtkts/SnippetPredictor/blob/main/docs/SnippetPredictor/Add-Snippet.md
Locale: en-US
Module Name: SnippetPredictor
ms.date: 12-30-2025
PlatyPS schema version: 2024-05-01
title: Add-Snippet
---

# Add-Snippet

## SYNOPSIS

Add snippets with tooltip to the snippet configuration.

## SYNTAX

### \_\_AllParameterSets

```
Add-Snippet [-Snippet] <string> [[-Tooltip] <string>] [[-Group] <string>] [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Add snippets with tooltip to the snippet configuration.
The snippet configuration resides in `~/.snippet-predictor.json`.

## EXAMPLES

### Example 1

```powershell
Add-Snippet "echo hello"
```

Add a snippet.

### Example 2

```powershell
Add-Snippet 'echo hello' 'say hello'
```

Add a snippet with tooltip.

### Example 3

```powershell
{echo hello} | Add-Snippet
```

Add a snippet.
You can pass code blocks. Code blocks are implicitly converted to strings through the pipeline.

### Example 4

```powershell
@{Snippet='echo hello'; Tooltip='say hello'},@{Snippet='echo goodbye'; Tooltip='say goobye'} | % {[pscustomobject]$_} | Add-Snippet
```

Add a snippet through pipeline.

## PARAMETERS

### -Group

The group of the snippet

```yaml
Type: System.String
DefaultValue: ""
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: (All)
    Position: 2
    IsRequired: false
    ValueFromPipeline: false
    ValueFromPipelineByPropertyName: true
    ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ""
```

### -Snippet

The text of the snippet

```yaml
Type: System.String
DefaultValue: ""
SupportsWildcards: false
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

### -Tooltip

The tooltip of the snippet

```yaml
Type: System.String
DefaultValue: ""
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: (All)
    Position: 1
    IsRequired: false
    ValueFromPipeline: false
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

The snippet string to add.

## OUTPUTS

### System.Object

No output.

## NOTES

## RELATED LINKS

- [SnippetPredictor.md](https://github.com/krymtkts/SnippetPredictor/blob/main/docs/SnippetPredictor/SnippetPredictor.md)
