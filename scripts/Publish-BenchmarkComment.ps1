[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $ReportPath
)

$ErrorActionPreference = 'Stop'
$event = Get-Content -LiteralPath $env:GITHUB_EVENT_PATH -Raw | ConvertFrom-Json
if ($event.pull_request.head.repo.full_name -ne $env:GITHUB_REPOSITORY) {
    throw 'Benchmark comments are only supported for same-repository pull requests.'
}
$body = @(
    '## Benchmark comparison'
    ''
    "Base: $($event.pull_request.base.sha)"
    "Head: $($event.pull_request.head.sha)"
    "[Workflow run]($env:GITHUB_SERVER_URL/$env:GITHUB_REPOSITORY/actions/runs/$env:GITHUB_RUN_ID/attempts/$env:GITHUB_RUN_ATTEMPT)"
    ''
    (Get-Content -LiteralPath $ReportPath -Raw)
) -join "`n"

$body | gh pr comment $event.number --repo $env:GITHUB_REPOSITORY --body-file -
if ($LASTEXITCODE) {
    throw 'Failed to publish benchmark comment.'
}
