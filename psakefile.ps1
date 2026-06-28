Properties {
    if (-not $Stage) {
        $Stage = 'Debug'
    }
    if ($DryRun -eq $null) {
        $DryRun = $true
    }
    $ModuleName = Get-ChildItem ./src/*/*.psd1 | Select-Object -ExpandProperty BaseName
    $ModuleSrcPath = Resolve-Path "./src/${ModuleName}/"
    $ModuleSrcProject = Resolve-Path "$ModuleSrcPath/*.fsproj"
    $ModuleVersion = ($ModuleSrcProject | Select-Xml '//Version/text()').Node.Value
    $ModulePublishPath = Resolve-Path "./publish/${ModuleName}/"
    $ChangelogPath = Join-Path $PSScriptRoot 'CHANGELOG.md'
    $FullChangelogUrl = "https://github.com/krymtkts/${ModuleName}/blob/main/CHANGELOG.md"
    $ModuleManifest = Get-Item -LiteralPath (Join-Path $ModuleSrcPath "$ModuleName.psd1")

    "Module: ${ModuleName} ver${ModuleVersion} root=${ModuleSrcPath} publish=${ModulePublishPath}"
}

Task default -Depends TestAll
Task TestAll -Depends Init, Build, UnitTest, Coverage, E2ETest, Lint

Task Init {
    'Init is running!'
    dotnet tool restore
}

Task Clean {
    'Clean is running!'
    Get-Module $ModuleName -All | Remove-Module -Force -ErrorAction SilentlyContinue
    @(
        "./src/*/*/${Stage}"
        './release'
        "${ModulePublishPath}/*"
    ) | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue -Exclude .gitignore, .gitkeep
}

function Get-ValidMarkdownCommentHelp {
    if (Get-Command Measure-PlatyPSMarkdown -ErrorAction SilentlyContinue) {
        $help = Measure-PlatyPSMarkdown ./docs\$ModuleName\*.md | Where-Object Filetype -Match CommandHelp
        $validations = $help.FilePath | Test-MarkdownCommandHelp -DetailView
        if (-not $validations.IsValid) {
            $validations.Messages | Where-Object { $_ -notlike 'PASS:*' } | Write-Error
            throw 'Invalid markdown help files.'
        }
        $help
    }
    else {
        Write-Warning 'PlatyPS is not installed.'
    }
}

Task Lint {
    dotnet fantomas ./src --check
    if (-not $?) {
        throw 'dotnet fantomas failed.'
    }
    $analyzerPath = dotnet build $ModuleSrcPath --getProperty:PkgIonide_Analyzers
    Get-ChildItem './src/*/*.fsproj' | ForEach-Object {
        dotnet fsharp-analyzers --project $_ --analyzers-path $analyzerPath --report "analysis/$($_.BaseName)-report.sarif" --code-root src --exclude-files '**/obj/**/*' '**/bin/**/*'
        if (-not $?) {
            throw "dotnet fsharp-analyzers for $($_.BaseName) failed."
        }
    }
    @('./psakefile.ps1', "./tests/$ModuleName.Tests.ps1") | ForEach-Object {
        $warn = Invoke-ScriptAnalyzer -Path $_ -Settings .\PSScriptAnalyzerSettings.psd1
        if ($warn) {
            $warn
            throw "Invoke-ScriptAnalyzer for ${_} failed."
        }
    }
    Get-ValidMarkdownCommentHelp | Out-Null
}

Task Build -Depends Clean {
    'Build command let!'
    Import-LocalizedData -BindingVariable module -BaseDirectory $ModuleSrcPath -FileName "${ModuleName}.psd1"
    if ($module.ModuleVersion -ne (Resolve-Path "./src/*/${ModuleName}.fsproj" | Select-Xml '//Version/text()').Node.Value) {
        throw 'Module manifest (.psd1) version does not match project (.fsproj) version.'
    }
    dotnet publish -c $Stage
    if (-not $?) {
        throw 'dotnet publish failed.'
    }
    "Completed to build $ModuleName ver$ModuleVersion"
}

Task UnitTest {
    dotnet test --verbosity detailed --hangdump --hangdump-timeout 5s --hangdump-type full
    if (-not $?) {
        throw 'dotnet test failed.'
    }
}

Task Coverage -Depends UnitTest {
    $target = "./src/${ModuleName}.Test/bin/Debug/*/${ModuleName}.Test.dll" | Resolve-Path -Relative
    dotnet coverlet $target --target 'dotnet' --targetargs 'test --no-build' --format cobertura --output ./coverage.cobertura.xml --include "[${ModuleName}*]*" --exclude-by-attribute 'CompilerGeneratedAttribute'

    Remove-Item ./coverage/* -Force -ErrorAction SilentlyContinue
    dotnet reportgenerator
}

Task Import -Depends Build {
    "Import $ModuleName ver$ModuleVersion"
    if ( -not ($ModuleName -and $ModuleVersion)) {
        throw "ModuleName or ModuleVersion not defined. $ModuleName, $ModuleVersion"
    }
    Import-Module $ModulePublishPath -Global
}

Task E2ETest -Depends Import {
    $result = Invoke-Pester -PassThru
    if ($result.Failed) {
        throw 'Invoke-Pester failed.'
    }
}

Task ExternalHelp -Depends Import {
    $help = Get-ValidMarkdownCommentHelp
    $help.FilePath | Update-MarkdownCommandHelp -NoBackup
    $help.FilePath | Import-MarkdownCommandHelp | Export-MamlCommandHelp -OutputFolder ./src/ -Force | Out-Null
}

Task ReleaseNotes {
    'Syncing module manifest ReleaseNotes from CHANGELOG.md.'

    $releaseNotes = Get-KeepAChangelogManifestReleaseNotes -Path $ChangelogPath -Version $ModuleVersion -FullChangelogUrl $FullChangelogUrl
    Set-KeepAChangelogManifestReleaseNotes -ManifestPath $ModuleManifest.FullName -ReleaseNotes $releaseNotes
}

Task ValidateReleaseMetadata {
    'Validating release metadata.'

    Assert-KeepAChangelogReleaseMetadata -Version $ModuleVersion -ReleaseTag $ReleaseTag
}

Task ReleaseTag -Depends ValidateReleaseMetadata {
    'Creating a signed release tag from CHANGELOG.md.'

    $run = {
        param(
            [Parameter(Mandatory)]
            [ValidateNotNull()]
            [scriptblock] $Command,

            [Parameter(Mandatory)]
            [ValidateNotNullOrEmpty()]
            [string] $FailureMessage
        )

        $output = @(
            & $Command 2>&1 |
                ForEach-Object { "$_" } |
                Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        )
        if (-not $LASTEXITCODE) {
            return , $output
        }
        if ($output.Count -eq 0) {
            throw $FailureMessage
        }
        throw "$FailureMessage`n$($output -join "`n")"
    }

    $gitReleaseTag = ($ReleaseTag -replace '^refs/tags/', '').Trim()
    $statusOutput = & $run { git status --porcelain=v1 --untracked-files=all } 'Failed to inspect git working tree status.'
    if ($statusOutput.Count -gt 0) {
        throw "Git working tree must be clean before release tagging. Remaining changes: $($statusOutput -join '; ')"
    }

    $existingTag = & $run { git tag --list $gitReleaseTag } "Failed to inspect local release tag '$gitReleaseTag'."
    if ($existingTag.Count -gt 0) {
        throw "Local release tag '$gitReleaseTag' already exists."
    }

    $releaseNotes = Get-KeepAChangelogEntry -Path $ChangelogPath -ReleaseTag $ReleaseTag
    $tagMessage = if ($releaseNotes.EndsWith("`n")) { $releaseNotes } else { "$releaseNotes`n" }
    & $run {
        git tag --sign --cleanup=verbatim $gitReleaseTag --message $tagMessage
    } "Failed to create signed release tag '$gitReleaseTag'." | Out-Null

    Write-Host "Created local signed release tag '$gitReleaseTag'." -ForegroundColor Green
}

Task Release -Depends TestAll {
    "Release $($ModuleName)! version=$ModuleVersion dryrun=$DryRun"

    if ( $Stage -ne 'Release' ) {
        throw "Stage must be 'Release' for publishing. Current stage: $Stage"
    }

    $m = Get-Module $ModuleName
    if ($m.Version -ne $ModuleVersion) {
        throw "Version inconsistency between project and module. $($m.Version), $ModuleVersion"
    }
    $p = Get-ChildItem "${ModulePublishPath}/*.psd1"
    if (-not $p) {
        throw "Module manifest not found. $($m.ModuleBase)/*.psd1"
    }

    $Params = @{
        Path = $p.FullName
        Repository = 'PSGallery'
        ApiKey = ConvertFrom-SecureString $ApiKey -AsPlainText
        WhatIf = $DryRun
        Verbose = $true
    }
    Publish-PSResource @Params
}

Task CheckUnusedSecurityPins {
    $securityPins = Get-Content Directory.Packages.props -Raw |
        ForEach-Object { ([xml]$_).Project.ItemGroup.PackageVersion } |
        Where-Object { $_.SecurityPin }
    if (-not $securityPins) {
        Write-Output 'No security pins found in Directory.Packages.props.'
        return
    }

    $topLevelPackageNames = dotnet package list --format json |
        ConvertFrom-Json -Depth 10 |
        ForEach-Object { $_.projects.frameworks.topLevelPackages.id } |
        ForEach-Object -Begin { $set = [System.Collections.Generic.HashSet[string]]::new() } -Process {
            $set.Add($_) | Out-Null
        } -End { , $set }

    $unused = $securityPins | Where-Object { -not $topLevelPackageNames.Contains($_.Include) } |
        ForEach-Object {
            [PSCustomObject]@{
                Package = $_.Include
                Version = $_.Version
                Status = 'PossiblyUnused'
            }
        }
    if ($unused) {
        $unused | Format-Table
        throw 'Found possibly unused security pins in Directory.Packages.props. Please check the above list and remove the pins if the packages are no longer used.'
    }
}