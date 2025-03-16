# SnippetPredictor

[![PowerShell Gallery](https://img.shields.io/powershellgallery/v/SnippetPredictor)](https://www.powershellgallery.com/packages/SnippetPredictor)
[![PowerShell Gallery](https://img.shields.io/powershellgallery/dt/SnippetPredictor)](https://www.powershellgallery.com/packages/SnippetPredictor)
![Test main status](https://github.com/krymtkts/SnippetPredictor/actions/workflows/main.yml/badge.svg)

A command-line predictor written in F# that suggests code snippet based on the input.
This module requires PowerShell 7.2 and PSReadLine 2.2.2.

This project is based on this article.

[How to create a command-line predictor - PowerShell | Microsoft Learn](https://learn.microsoft.com/en-us/powershell/scripting/dev-cross-plat/create-cmdline-predictor?view=powershell-7.4)

## Installation

[PowerShell Gallery | pocof](https://www.powershellgallery.com/packages/pocof/)

```powershell
# PSResourceGet (also known as PowerShellGet 3.0)
Install-PSResource -Name SnippetPredictor

# PowerShellGet 2.x
Install-Module -Name SnippetPredictor
```

## Cmdlet help

See the documents as following.

- [`Add-Snippet.md`](./docs/SnippetPredictor/Add-Snippet.md)
- [`Get-Snippet.md`](./docs/SnippetPredictor/Get-Snippet.md)
- [`Remove-Snippet.md`](./docs/SnippetPredictor/Remove-Snippet.md)

## Usage

```powershell
# PredictionSource = HistoryAndPlugin required.
Get-PSReadLineOption | Select-Object PredictionSource
#
# PredictionSource
# ----------------
# HistoryAndPlugin

Import-Module SnippetPredictor

# Confirm SnippetPredictor(Snippet) loaded.
Get-PSSubsystem -Kind CommandPredictor
#
# Kind              SubsystemType      IsRegistered Implementations
# ----              -------------      ------------ ---------------
# CommandPredictor  ICommandPredictor          True {Snippet, Windows Package Manager - WinGet}
```
