[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $BasePath,

    [Parameter(Mandatory)]
    [string] $HeadPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$culture = [System.Globalization.CultureInfo]::InvariantCulture

function Read-Benchmarks([string] $Path) {
    $benchmarks = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json | ForEach-Object Benchmarks
    if (-not $benchmarks) {
        throw "No benchmark results in $Path."
    }
    $indexed = @{}
    foreach ($benchmark in $benchmarks) {
        if ($null -eq $benchmark.Statistics) {
            throw "Missing measurements for $($benchmark.FullName)."
        }
        $indexed.Add($benchmark.FullName, $benchmark)
    }
    return $indexed
}

function Format-Number([double] $Value) {
    $Value.ToString('0.##', $culture)
}

function Get-Allocation($Benchmark) {
    $metric = $Benchmark.Metrics | Where-Object { $_.Descriptor.Id -eq 'Allocated Memory' }
    if ($null -eq $metric) {
        throw "Missing allocation for $($Benchmark.FullName)."
    }
    return [double] $metric.Value
}

$base = Read-Benchmarks $BasePath
$head = Read-Benchmarks $HeadPath
$common = @($base.Keys | Where-Object { $head.ContainsKey($_) })
if ($common.Count -eq 0) {
    throw 'No common benchmark cases.'
}

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('| Case | Base mean ± SD (ns) | Head mean ± SD (ns) | Time change | Base allocated (B) | Head allocated (B) | Allocation change (B) |')
$lines.Add('| --- | ---: | ---: | ---: | ---: | ---: | ---: |')
foreach ($key in @(@($base.Keys) + @($head.Keys) | Sort-Object -Unique)) {
    $label = $key.Replace('|', '\|').Replace("`r", ' ').Replace("`n", ' ')
    if (-not $base.ContainsKey($key) -or -not $head.ContainsKey($key)) {
        $side = if ($base.ContainsKey($key)) { 'Removed' } else { 'Added' }
        $lines.Add("| $label | — | — | $side; not compared | — | — | — |")
        continue
    }
    $before = $base[$key]
    $after = $head[$key]
    $change = if ($before.Statistics.Mean -gt 0) {
        "$(Format-Number (($after.Statistics.Mean / $before.Statistics.Mean - 1) * 100))%"
    }
    else { 'N/A' }
    $beforeAllocation = Get-Allocation $before
    $afterAllocation = Get-Allocation $after
    $values = @(
        $before.Statistics.Mean
        $before.Statistics.StandardDeviation
        $after.Statistics.Mean
        $after.Statistics.StandardDeviation
        $beforeAllocation
        $afterAllocation
        ($afterAllocation - $beforeAllocation)
    ) | ForEach-Object { Format-Number $_ }

    $cells = @(
        $label
        "$($values[0]) ± $($values[1])"
        "$($values[2]) ± $($values[3])"
        $change
        $values[4]
        $values[5]
        $values[6]
    )
    $lines.Add("| $($cells -join ' | ') |")
}
$lines.Add('')
$lines.Add('Positive changes indicate higher measured time or allocation, not a statistical regression verdict. SD is the standard deviation. Managed allocation is measured per operation.')
$lines -join "`n"
