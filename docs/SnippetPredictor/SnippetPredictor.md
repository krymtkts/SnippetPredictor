---
document type: module
Help Version: 0.1.0
HelpInfoUri: "https://github.com/krymtkts/SnippetPredictor/blob/main/docs/SnippetPredictor/SnippetPredictor.md"
Locale: en-US
Module Guid: 46275f69-83fc-4a16-89b5-fd0e750c6358
Module Name: SnippetPredictor
ms.date: 02-22-2025
PlatyPS schema version: 2024-05-01
title: SnippetPredictor Module
---

# SnippetPredictor Module

## Description

A predictor that suggests a snippet based on the input.
The snippet configuration is located at `~/.snippet-predictor.json`.

Filter snippets in your `~/.snippet-predictor.json` file using the following keywords:

- Use `:snp {input}` to search for `{input}` in the `Snippet` field.
- Use `:tip {input}` to search for `{input}` in the `Tooltip` field.
- Use `:{group} {input}` to search for `{input}` in the `Snippet` field for snippets in a specified `Group`.
  - Allowed characters for the `Group` field: `^[a-zA-Z0-9]+$`.
    (That is, the group name must consist of alphanumeric characters only.)

Default, snippets are searched in a case-insensitive manner.
You can specify the case-sensitivity to `SearchCaseSensitive` in `.snippet-predictor.json`.
default value is `SearchCaseSensitive = false.`

## SnippetPredictor

### [Add-Snippet](Add-Snippet.md)

Add snippets with tooltip to the snippet configuration.

### [Get-Snippet](Get-Snippet.md)

Retrieves a saved snippets.

### [Remove-Snippet](Remove-Snippet.md)

Remove specified snippets from the snippet configuration.
