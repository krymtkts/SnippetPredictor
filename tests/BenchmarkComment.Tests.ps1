BeforeAll {
    $script:publish = Join-Path $PSScriptRoot '../scripts/Publish-BenchmarkComment.ps1'
    $script:variables = @(
        'GITHUB_EVENT_PATH', 'GITHUB_REPOSITORY', 'GITHUB_SERVER_URL',
        'GITHUB_RUN_ID', 'GITHUB_RUN_ATTEMPT'
    )

    function gh {
        [CmdletBinding(PositionalBinding = $false)]
        param(
            [Parameter(ValueFromPipeline)]
            [string] $InputObject,

            [Parameter(ValueFromRemainingArguments)]
            [string[]] $Arguments
        )
        process {
            $ghState.Body = $InputObject
            $ghState.Arguments = $Arguments
            $ghState.Calls++
            $global:LASTEXITCODE = $ghState.ExitCode
        }
    }
}

Describe 'Benchmark comment publication' {
    BeforeEach {
        $script:saved = @{}
        $script:savedExitCode = $global:LASTEXITCODE
        $script:ghState = @{ ExitCode = 0; Calls = 0; Body = $null; Arguments = @() }
        foreach ($name in $variables) {
            $saved[$name] = [Environment]::GetEnvironmentVariable($name)
        }
        $env:GITHUB_EVENT_PATH = Join-Path $TestDrive event.json
        $env:GITHUB_REPOSITORY = 'example/repository'
        $env:GITHUB_SERVER_URL = 'https://github.com'
        $env:GITHUB_RUN_ID = '123'
        $env:GITHUB_RUN_ATTEMPT = '2'
        $script:report = Join-Path $TestDrive report.md
        Set-Content -LiteralPath $report -Value 'Measured results'
        $script:event = @{
            number = 42
            pull_request = @{
                base = @{ sha = 'base-sha' }
                head = @{ sha = 'head-sha'; repo = @{ full_name = 'example/repository' } }
            }
        }
    }

    AfterEach {
        $global:LASTEXITCODE = $savedExitCode
        foreach ($name in $variables) {
            [Environment]::SetEnvironmentVariable($name, $saved[$name])
        }
    }

    It 'creates a comment using the event destination and measured report' {
        $event | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $env:GITHUB_EVENT_PATH
        & $publish -ReportPath $report
        $ghState.Calls | Should -Be 1
        ($ghState.Arguments -join ' ') | Should -Be 'pr comment 42 --repo example/repository --body-file -'
        $ghState.Body | Should -Match 'base-sha'
        $ghState.Body | Should -Match 'head-sha'
        $ghState.Body | Should -Match 'actions/runs/123/attempts/2'
        $ghState.Body | Should -Match 'Measured results'
    }

    It 'does not post for a fork pull request' {
        $event.pull_request.head.repo.full_name = 'external/repository'
        $event | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $env:GITHUB_EVENT_PATH
        { & $publish -ReportPath $report } | Should -Throw '*same-repository*'
        $ghState.Calls | Should -Be 0
    }

    It 'includes the benchmark workload change notice from the report' {
        $notice = 'Benchmark project changed. Even matching cases may measure different workloads; interpret the comparison accordingly.'
        Set-Content -LiteralPath $report -Value $notice
        $event | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $env:GITHUB_EVENT_PATH
        & $publish -ReportPath $report
        $ghState.Calls | Should -Be 1
        $ghState.Body.Contains($notice) | Should -BeTrue
    }

    It 'fails on a nonzero CLI exit code without retrying' {
        $event | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $env:GITHUB_EVENT_PATH
        $ghState.ExitCode = 1
        { & $publish -ReportPath $report } | Should -Throw '*Failed to publish*'
        $ghState.Calls | Should -Be 1
    }
}
