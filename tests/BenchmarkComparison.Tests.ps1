BeforeAll {
    $script:compare = Join-Path $PSScriptRoot '../scripts/Compare-BenchmarkResults.ps1'

    function New-Measurement([string] $Name, [double] $Mean = 100, [double] $Allocated = 32) {
        @{
            FullName = $Name
            Statistics = @{ Mean = $Mean; StandardDeviation = 2 }
            Metrics = @(@{ Value = $Allocated; Descriptor = @{ Id = 'Allocated Memory' } })
        }
    }

    function Write-Report([string] $Name, [object[]] $Measurements) {
        $path = Join-Path $TestDrive $Name
        @{ Benchmarks = $Measurements } | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $path
        $path
    }
}

Describe 'Benchmark comparison' {
    It 'reports time and allocation changes without failing on an increase' {
        $base = Write-Report base.json @(New-Measurement 'Example.Run(Count: 1000)')
        $head = Write-Report head.json @(New-Measurement 'Example.Run(Count: 1000)' 120 48)
        $report = & $compare -BasePath $base -HeadPath $head
        $report | Should -Match '100 ± 2.*120 ± 2.*20%.*32.*48.*16'
    }

    It 'identifies unchanged and faster cases' {
        $base = Write-Report base.json @((New-Measurement Same), (New-Measurement Faster))
        $head = Write-Report head.json @((New-Measurement Same), (New-Measurement Faster 50))
        $report = & $compare -BasePath $base -HeadPath $head
        $report | Should -Match 'Same.*0%'
        $report | Should -Match 'Faster.*-50%'
    }

    It 'reports added and removed cases while comparing common cases' {
        $base = Write-Report base.json @((New-Measurement Common), (New-Measurement Removed))
        $head = Write-Report head.json @((New-Measurement Common), (New-Measurement Added))
        $report = & $compare -BasePath $base -HeadPath $head
        $report | Should -Match 'Removed; not compared'
        $report | Should -Match 'Added; not compared'
    }

    It 'rejects empty results' {
        $base = Write-Report base.json @()
        $head = Write-Report head.json @(New-Measurement Common)
        { & $compare -BasePath $base -HeadPath $head } | Should -Throw '*No benchmark results*'
    }

    It 'rejects unrelated cases' {
        $base = Write-Report base.json @(New-Measurement Before)
        $head = Write-Report head.json @(New-Measurement After)
        { & $compare -BasePath $base -HeadPath $head } | Should -Throw '*No common*'
    }

    It 'rejects invalid JSON and missing files' {
        $base = Join-Path $TestDrive invalid.json
        Set-Content -LiteralPath $base -Value '{'
        $head = Write-Report head.json @(New-Measurement Common)
        { & $compare -BasePath $base -HeadPath $head } | Should -Throw
        { & $compare -BasePath "$base.missing" -HeadPath $head } | Should -Throw
    }

    It 'rejects failed measurements' {
        $measurement = New-Measurement Common
        $measurement.Statistics = $null
        $base = Write-Report base.json @($measurement)
        $head = Write-Report head.json @(New-Measurement Common)
        { & $compare -BasePath $base -HeadPath $head } | Should -Throw '*Missing measurements*'
    }

    It 'rejects missing allocation measurements' {
        $measurement = New-Measurement Common
        $measurement.Metrics = @()
        $base = Write-Report base.json @($measurement)
        $head = Write-Report head.json @(New-Measurement Common)
        { & $compare -BasePath $base -HeadPath $head } | Should -Throw '*Missing allocation*'
    }
}
