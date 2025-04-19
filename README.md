# SnippetPredictor

[![PowerShell Gallery](https://img.shields.io/powershellgallery/v/SnippetPredictor)](https://www.powershellgallery.com/packages/SnippetPredictor)
[![PowerShell Gallery](https://img.shields.io/powershellgallery/dt/SnippetPredictor)](https://www.powershellgallery.com/packages/SnippetPredictor)
![Test main status](https://github.com/krymtkts/SnippetPredictor/actions/workflows/main.yml/badge.svg)
[![codecov](https://codecov.io/gh/krymtkts/SnippetPredictor/graph/badge.svg?token=7HA9NC8PHT)](https://codecov.io/gh/krymtkts/SnippetPredictor)
![Top Language](https://img.shields.io/github/languages/top/krymtkts/SnippetPredictor?color=%23b845fc)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)

SnippetPredictor is a command-line predictor written in F# that suggests code snippet based on the input.
This module requires PowerShell 7.2 and PSReadLine 2.2.2.

This project is based on this article.

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

Before using SnippetPredictor, verify that your PowerShell `PredictionSource` is set to `HistoryAndPlugin`[^1]:

```powershell
# PredictionSource = HistoryAndPlugin required.
Get-PSReadLineOption | Select-Object PredictionSource
#
# PredictionSource
# ----------------
# HistoryAndPlugin
```

[^1]: [Using predictors in PSReadLine - PowerShell | Microsoft Learn](https://learn.microsoft.com/en-us/powershell/scripting/learn/shell/using-predictors?view=powershell-7.4#managing-predictive-intellisense)

Next, import the SnippetPredictor module and confirm that the predictor has been loaded:

```powershell
Import-Module SnippetPredictor

# Confirm SnippetPredictor(Snippet) loaded.
Get-PSSubsystem -Kind CommandPredictor
#
# Kind              SubsystemType      IsRegistered Implementations
# ----              -------------      ------------ ---------------
# CommandPredictor  ICommandPredictor          True {Snippet, Windows Package Manager - WinGet}
```

Finally, set the prediction view style to `ListView`[^2]:

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
    (That is, the group name must consist of alphanumeric characters only.)

Default, snippets are searched in a case-insensitive manner.
You can specify the case-sensitivity to `SearchCaseSensitive` in `.snippet-predictor.json`.
default value is `SearchCaseSensitive = false.`

You can list your registered snippets with the `Get-Snippet` command.

To remove a snippet, use the `Remove-Snippet` command or delete it directly from `~/.snippet-predictor.json`.

For example:

```powershell
Remove-Snippet "echo 'hello'"
```

By combining `Get-Snippet` and `Remove-Snippet`, you can remove multiple snippets that match a specific pattern in one go:

```powershell
Get-Snippet | Where-Object -Property Tooltip -like *test* | Remove-Snippet
```
