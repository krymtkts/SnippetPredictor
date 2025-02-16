Import-Module -Name "$PSScriptRoot\SnippetPredictor.dll" -PassThru | Out-Null

function Add-Snippet {
    [CmdletBinding()]
    param (
        [Parameter(
            Mandatory,
            Position = 0,
            ValueFromPipeline,
            ValueFromPipelineByPropertyName,
            HelpMessage = 'The text of the snippet'
        )]
        [string]$Text,
        [Parameter(
            Position = 1,
            ValueFromPipelineByPropertyName,
            HelpMessage = 'The tooltip of the snippet'
        )]
        [string]$Tooltip = ''
    )
    begin {
        $configPath = "${env:USERPROFILE}/.snippet-predictor.json"
        if (-not (Test-Path $configPath)) {
            New-Item -Path $configPath -ItemType File | Out-Null
            $config = @{
                snippets = @()
            }
        }
        else {
            $config = Get-Content $configPath -Raw | ConvertFrom-Json
            if (-not $config) {
                $config = @{
                    snippets = @()
                }
            }
            if ((-not $config.snippets) -or ($config.snippets -isnot [System.Array])) {
                $config.snippets = @()
            }
        }
    }
    process {
        $config.snippets += @{
            snippet = $Text
            tooltip = $Tooltip
        }
    }
    end {
        $config | ConvertTo-Json -Depth 2 | Set-Content $configPath
    }
}

function Get-Snippet {
    [CmdletBinding()]
    param (
    )
    $configPath = "${env:USERPROFILE}/.snippet-predictor.json"
    if (Test-Path $configPath) {
        $config = Get-Content $configPath -Raw
        if ($config) {
            $config | ConvertFrom-Json
        }
    }
}

function Remove-Snippet {
    [CmdletBinding()]
    param (
        [Parameter(
            Mandatory,
            Position = 0,
            ValueFromPipeline,
            ValueFromPipelineByPropertyName,
            HelpMessage = 'The text of the snippet to remove'
        )]
        [string]$Text
    )
    $configPath = "${env:USERPROFILE}/.snippet-predictor.json"
    if (Test-Path $configPath) {
        $config = Get-Content $configPath -Raw | ConvertFrom-Json
        if ($config.snippets) {
            $config.snippets = $config.snippets | Where-Object { $_.snippet -ne $Text }
            $config | ConvertTo-Json -Depth 2 | Set-Content $configPath
        }
    }
}
