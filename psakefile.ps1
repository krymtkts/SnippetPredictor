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
    dotnet test --nologo --verbosity detailed --blame-hang-timeout 5s --blame-hang-dump-type full
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

Task Release -PreCondition { $Stage -eq 'Release' } -Depends TestAll, ExternalHelp {
    "Release $($ModuleName)! version=$ModuleVersion dryrun=$DryRun"

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
        ApiKey = (Get-Credential API-key -Message 'Enter your API key as the password').GetNetworkCredential().Password
        WhatIf = $DryRun
        Verbose = $true
    }
    Publish-PSResource @Params
}
