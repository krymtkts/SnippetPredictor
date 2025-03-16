# SnippetPredictor

[![PowerShell Gallery](https://img.shields.io/powershellgallery/v/SnippetPredictor)](https://www.powershellgallery.com/packages/SnippetPredictor)
[![PowerShell Gallery](https://img.shields.io/powershellgallery/dt/SnippetPredictor)](https://www.powershellgallery.com/packages/SnippetPredictor)
![Test main status](https://github.com/krymtkts/SnippetPredictor/actions/workflows/main.yml/badge.svg)

This project is based on this article.

[How to create a command-line predictor - PowerShell | Microsoft Learn](https://learn.microsoft.com/en-us/powershell/scripting/dev-cross-plat/create-cmdline-predictor?view=powershell-7.4)

## Usage

```powershell
# PredictionSource = HistoryAndPlugin required.
Get-PSReadLineOption | Select-Object PredictionSource
#
# PredictionSource
# ----------------
# HistoryAndPlugin

dotnet publish -c Release
Import-Module .\bin\SnippetPredictor\SamplePredictor.dll

# Confirm SamplePredictor loaded.
Get-PSSubsystem -Kind CommandPredictor
#
# Kind              SubsystemType      IsRegistered Implementations
# ----              -------------      ------------ ---------------
# CommandPredictor  ICommandPredictor          True {Windows Package Manager - WinGet, SnippetPredictor}
```
