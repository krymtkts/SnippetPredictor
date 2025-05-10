# SnippetPredictor

[![PowerShell Gallery](https://img.shields.io/powershellgallery/v/SnippetPredictor)](https://www.powershellgallery.com/packages/SnippetPredictor)
[![PowerShell Gallery](https://img.shields.io/powershellgallery/dt/SnippetPredictor)](https://www.powershellgallery.com/packages/SnippetPredictor)
![Test main status](https://github.com/krymtkts/SnippetPredictor/actions/workflows/main.yml/badge.svg)
[![codecov](https://codecov.io/gh/krymtkts/SnippetPredictor/graph/badge.svg?token=7HA9NC8PHT)](https://codecov.io/gh/krymtkts/SnippetPredictor)
![Top Language](https://img.shields.io/github/languages/top/krymtkts/SnippetPredictor?color=%23b845fc)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)

SnippetPredictor is a command-line predictor written in F#. It suggests code snippets based on the input.
This module requires PowerShell 7.2 and PSReadLine 2.2.2.

This project builds upon the following article:

[How to create a command-line predictor - PowerShell | Microsoft Learn](https://learn.microsoft.com/en-us/powershell/scripting/dev-cross-plat/create-cmdline-predictor?view=powershell-7.4)

## Installation

Install SnippetPredictor from the PowerShell Gallery:

[PowerShell Gallery | SnippetPredictor](https://www.powershellgallery.com/packages/SnippetPredictor/)

```powershell
# Recommended: PSResourceGet (PowerShellGet 3.0)
Install-PSResource -Name SnippetPredictor

# Alternatively, if you are using PowerShellGet 2.x:
Install-Module -Name SnippetPredictor
```

Before using SnippetPredictor, ensure that your PowerShell `PredictionSource` equals `HistoryAndPlugin`[^1]:

```powershell
# PredictionSource = HistoryAndPlugin required.
Get-PSReadLineOption | Select-Object PredictionSource
#
# PredictionSource
# ----------------
# HistoryAndPlugin
```

[^1]: [Using predictors in PSReadLine - PowerShell | Microsoft Learn](https://learn.microsoft.com/en-us/powershell/scripting/learn/shell/using-predictors?view=powershell-7.4#managing-predictive-intellisense)

Import the SnippetPredictor module and verify that the predictor loads:

```powershell
Import-Module SnippetPredictor

# Confirm SnippetPredictor(Snippet) loaded.
Get-PSSubsystem -Kind CommandPredictor
#
# Kind              SubsystemType      IsRegistered Implementations
# ----              -------------      ------------ ---------------
# CommandPredictor  ICommandPredictor          True {Snippet, Windows Package Manager - WinGet}
```

Set the prediction view style to `ListView`[^2]:

```powershell
Set-PSReadLineOption -PredictionViewStyle ListView
```

[^2]: [Using predictors in PSReadLine - PowerShell | Microsoft Learn](https://learn.microsoft.com/en-us/powershell/scripting/learn/shell/using-predictors?view=powershell-7.4#using-other-predictor-plug-ins)

## Cmdlet help

Refer to the following documents for detailed cmdlet help:

- [`Add-Snippet.md`](./docs/SnippetPredictor/Add-Snippet.md)
- [`Get-Snippet.md`](./docs/SnippetPredictor/Get-Snippet.md)
- [`Remove-Snippet.md`](./docs/SnippetPredictor/Remove-Snippet.md)

## Usage

First, set up your `~/.snippet-predictor.json` file.
The easiest way to do this is to run the `Add-Snippet` command:

```powershell
Add-Snippet "echo 'hello'" -Tooltip 'say hello' -Group 'greeting'
```

This will create a file with the following content.
You can also create the file manually if you prefer.

```json
{
  "Snippets": [
    {
      "Snippet": "echo 'hello'",
      "Tooltip": "say hello.",
      "Group": "greeting"
    }
  ]
}
```

Filter snippets in your `~/.snippet-predictor.json` file using the following keywords:

- Use `:snp {input}` to search for `{input}` in the `Snippet` field.
- Use `:tip {input}` to search for `{input}` in the `Tooltip` field.
- Use `:{group} {input}` to search for `{input}` in the `Snippet` field for snippets in a specified `Group`.
  - Allowed characters for the `Group` field: `^[a-zA-Z0-9]+$`.
    (Group names must consist of alphanumeric characters.)
  - Typing `:` or a partial group name (e.g., `:p`) suggests matching groups like `:pwsh`.

By default, the predictor searches snippets in a case-insensitive manner.
To enable case-sensitive search, set `SearchCaseSensitive` to `true` in `.snippet-predictor.json`.
The default value is `false`.

You can list your registered snippets with the `Get-Snippet` command.

To remove a snippet, use the `Remove-Snippet` command.

```powershell
Remove-Snippet "echo 'hello'"
```

You can also delete it directly from `~/.snippet-predictor.json`.

By combining `Get-Snippet` and `Remove-Snippet`, you can remove snippets that match a specific pattern. Perform this in one step:

```powershell
Get-Snippet | Where-Object -Property Tooltip -like *test* | Remove-Snippet
```
