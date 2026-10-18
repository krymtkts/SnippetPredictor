---
document type: cmdlet
external help file: SnippetPredictor-Help.xml
HelpUri: https://github.com/krymtkts/SnippetPredictor/blob/main/docs/SnippetPredictor/Add-Snippet.md
Locale: en-US
Module Name: SnippetPredictor
ms.date: 10-17-2026
PlatyPS schema version: 2024-05-01
title: Add-Snippet
---

# Add-Snippet

## SYNOPSIS

Add snippets with tooltip to the snippet configuration.

## SYNTAX

### \_\_AllParameterSets

```
Add-Snippet [-Snippet] <string> [[-Tooltip] <string>] [[-Group] <string>]
 [-PredictionContexts <PredictionContexts[]>] [<CommonParameters>]
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

### Example 5

```powershell
Add-Snippet 'Select-Object Name' -PredictionContexts Command, Pipeline
```

Suggest the snippet in command and pipeline positions.

### Example 6

```powershell
Add-Snippet 'git push --force-with-lease' -Group rewrite -PredictionContexts IdentifierOnly
```

Keep the snippet available through identifiers without suggesting it for ordinary input.

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

### -PredictionContexts

The positions in which to suggest the snippet for ordinary input.
`Command` allows the start of a line, after a leading block comment, and after `;`.
`Pipeline` allows the second and later commands in a pipeline.
Use `Command, Pipeline` to allow both positions.
The default is `Command`.
Use `IdentifierOnly` alone to omit the snippet from ordinary predictions.
The parameter rejects null, empty arrays and overlapping contexts.
Identifier-based predictions, completion, and accept handlers remain available with every setting.
This setting doesn't verify whether the snippet accepts pipeline input.

```yaml
Type: SnippetPredictor.PredictionContexts[]
DefaultValue: Command
SupportsWildcards: false
Aliases: []
ParameterSets:
  - Name: (All)
    Position: Named
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

### SnippetPredictor.PredictionContexts[]

Prediction contexts supplied through a matching property on a pipeline object.

## OUTPUTS

### System.Object

No output.

## NOTES

## RELATED LINKS

- [SnippetPredictor.md](https://github.com/krymtkts/SnippetPredictor/blob/main/docs/SnippetPredictor/SnippetPredictor.md)
