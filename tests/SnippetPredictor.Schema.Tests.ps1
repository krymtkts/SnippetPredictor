BeforeDiscovery {
    $cases = @(
        @{ Name = 'omitted settings'; Json = '{}'; Valid = $true }
        @{ Name = 'null settings'; Json = '{"Snippets":null,"SearchCaseSensitive":null,"PredictOnlyWithIdentifier":null}'; Valid = $true }
        @{ Name = 'empty snippets'; Json = '{"Snippets":[]}'; Valid = $true }
        @{ Name = 'boolean settings'; Json = '{"SearchCaseSensitive":true,"PredictOnlyWithIdentifier":false}'; Valid = $true }
        @{ Name = 'omitted entry settings'; Json = '{"Snippets":[{"Snippet":"git status"}]}'; Valid = $true }
        @{ Name = 'null entry settings'; Json = '{"Snippets":[{"Snippet":"git","Tooltip":null,"Group":null,"PredictionContexts":null}]}'; Valid = $true }
        @{ Name = 'empty tooltip'; Json = '{"Snippets":[{"Snippet":"git","Tooltip":""}]}'; Valid = $true }
        @{ Name = 'alphanumeric group'; Json = '{"Snippets":[{"Snippet":"git","Group":"Git123"}]}'; Valid = $true }
        @{ Name = 'group with final LF'; Json = '{"Snippets":[{"Snippet":"git","Group":"git\n"}]}'; Valid = $true }
        @{ Name = 'lowercase property names'; Json = '{"snippets":[{"snippet":"git","tooltip":"status","group":"git","predictioncontexts":["command"]}],"searchcasesensitive":false,"predictonlywithidentifier":true}'; Valid = $true }
        @{ Name = 'uppercase snippet property'; Json = '{"SNIPPETS":[{"SNIPPET":"git"}]}'; Valid = $true }
        @{ Name = 'escaped property name'; Json = '{"Snippets":[{"\u0053nippet":"git"}]}'; Valid = $true }
        @{ Name = 'unknown properties'; Json = '{"Extra":{"Value":true},"Snippets":[{"Snippet":"git","Extra":[]}]}'; Valid = $true }
        @{ Name = 'distinct unknown names'; Json = '{"Snippets":[{"Snippet":"git","Extra":1,"Extra ":2}]}'; Valid = $true }
        @{ Name = 'unknown root name with LF'; Json = '{"snippets\n":[null]}'; Valid = $true }
        @{ Name = 'unknown Unicode property name'; Json = '{"Snippets":[{"Snippet":"git","PredİctionContexts":0}]}'; Valid = $true }
        @{ Name = 'repeated entries and values'; Json = '{"Snippets":[{"Snippet":"git","Tooltip":"git","Group":"git","PredictionContexts":["Command"]},{"Snippet":"git","Tooltip":"git","Group":"git","PredictionContexts":["Command"]}]}'; Valid = $true }
        foreach ($value in @('"IdentifierOnly"', '"identifieronly"', '"IDENTIFIERONLY"', '["Command","\u0050ipeline"]', '["\u0070ipeline","\u0063ommand"]')) {
            @{ Name = "valid contexts $value"; Json = '{"Snippets":[{"Snippet":"git","PredictionContexts":' + $value + '}]}'; Valid = $true }
        }
        foreach ($value in @('null', '[]', 'true', '1', '"git"')) {
            @{ Name = "invalid root $value"; Json = $value; Valid = $false }
        }
        foreach ($value in @('{}', 'true', '1', '"git"')) {
            @{ Name = "invalid Snippets $value"; Json = '{"Snippets":' + $value + '}'; Valid = $false }
        }
        foreach ($value in @('null', '[]', '{}', 'true', '1', '"git"')) {
            @{ Name = "invalid entry $value"; Json = '{"Snippets":[' + $value + ']}'; Valid = $false }
        }
        foreach ($value in @('null', '[]', '{}', 'true', '1', '""')) {
            @{ Name = "invalid Snippet $value"; Json = '{"Snippets":[{"Snippet":' + $value + '}]}'; Valid = $false }
        }
        @{ Name = 'missing snippet with trailing LF in name'; Json = '{"Snippets":[{"snippet\n":"git"}]}'; Valid = $false }
        foreach ($code in @(0x0009, 0x000A, 0x000B, 0x000C, 0x000D, 0x0020, 0x0085, 0x00A0, 0x1680, 0x2000, 0x2001, 0x2002, 0x2003, 0x2004, 0x2005, 0x2006, 0x2007, 0x2008, 0x2009, 0x200A, 0x2028, 0x2029, 0x202F, 0x205F, 0x3000)) {
            $value = '"\u' + $code.ToString('X4') + '"'
            @{ Name = "blank Snippet U+$($code.ToString('X4'))"; Json = '{"Snippets":[{"Snippet":' + $value + '}]}'; Valid = $false }
        }
        foreach ($value in @('" \t\u3000"', '"\uFEFF"', '"\u200B"', '" \tgit\n"')) {
            @{ Name = "snippet whitespace boundary $value"; Json = '{"Snippets":[{"Snippet":' + $value + '}]}'; Valid = $value -ne '" \t\u3000"' }
        }
        foreach ($name in @('SearchCaseSensitive', 'PredictOnlyWithIdentifier')) {
            foreach ($value in @('"true"', '1', '[]', '{}')) {
                @{ Name = "invalid $name $value"; Json = '{"' + $name + '":' + $value + '}'; Valid = $false }
            }
        }
        foreach ($name in @('Tooltip', 'Group')) {
            foreach ($value in @('false', '1', '[]', '{}')) {
                @{ Name = "invalid $name $value"; Json = '{"Snippets":[{"Snippet":"git","' + $name + '":' + $value + '}]}'; Valid = $false }
            }
        }
        foreach ($value in @('""', '" "', '"git-1"', '"git_1"', '"git\r"', '"git\r\n"', '"git\n\n"', '"git\u2028"')) {
            @{ Name = "invalid Group $value"; Json = '{"Snippets":[{"Snippet":"git","Group":' + $value + '}]}'; Valid = $false }
        }
        foreach ($value in @('false', '1', '{}', '[]', '"Command"', '"IdentifierOnly\n"', '["IdentifierOnly"]', '["IdentifierOnly","IdentifierOnly"]', '["Command","IdentifierOnly"]', '["Command",null]', '["Command",true]', '["Pipeline",1]', '["Command",""]', '["Command"," Pipeline"]', '["Command","Pipeline\n"]', '["Unsupported"]', '["Command","\u0043ommand"]', '["Pipeline","\u0050ipeline"]', '["Command","\u0063ommand"]')) {
            @{ Name = "invalid contexts $value"; Json = '{"Snippets":[{"Snippet":"git","PredictionContexts":' + $value + '}]}'; Valid = $false }
        }
        $names = @('Command', 'command', 'Pipeline', 'PIPELINE')
        foreach ($first in $names) {
            @{ Name = "single context $first"; Json = '{"Snippets":[{"Snippet":"git","PredictionContexts":["' + $first + '"]}]}'; Valid = $true }
            foreach ($second in $names) {
                @{ Name = "context pair $first $second"; Json = '{"Snippets":[{"Snippet":"git","PredictionContexts":["' + $first + '","' + $second + '"]}]}'; Valid = $first -ine $second }
                foreach ($third in $names) {
                    @{ Name = "context triple $first $second $third"; Json = '{"Snippets":[{"Snippet":"git","PredictionContexts":["' + $first + '","' + $second + '","' + $third + '"]}]}'; Valid = $false }
                }
            }
        }
    )
}

BeforeAll {
    $module = Get-Module SnippetPredictor
    $schemaPath = Join-Path $module.ModuleBase 'schemas/snippet-predictor.schema.json'
    $sourceSchemaPath = Join-Path $PSScriptRoot '../schemas/snippet-predictor.schema.json'
    # NOTE: Vendored from https://json-schema.org/draft-07/schema; tests require no downloads.
    $metaSchemaPath = Join-Path $PSScriptRoot 'fixtures/json-schema-draft-07.schema.json'
    $configPath = Join-Path $TestDrive '.snippet-predictor.json'
}

Describe 'Snippet configuration JSON Schema' {
    It 'bundles the source schema with the imported module' {
        (Get-FileHash -LiteralPath $schemaPath).Hash | Should -Be (Get-FileHash -LiteralPath $sourceSchemaPath).Hash
    }

    It 'includes the schema in the module file list' {
        $manifest = Import-PowerShellDataFile -LiteralPath (Join-Path $module.ModuleBase 'SnippetPredictor.psd1')
        $manifest.FileList | Should -Contain 'schemas/snippet-predictor.schema.json'
    }

    It 'validates <Name> with expected validity <Valid>' -TestCases $cases {
        [System.IO.File]::WriteAllText($configPath, $Json)
        $state = [SnippetPredictor.Config]::parseSnippetFile($configPath).GetAwaiter().GetResult()
        $state.IsValid | Should -Be $Valid
        Test-Json -Json $Json -SchemaFile $schemaPath -ErrorAction SilentlyContinue | Should -Be $Valid
    }

    It 'conforms to the Draft 7 meta-schema' {
        Test-Json -Json (Get-Content -LiteralPath $schemaPath -Raw) -SchemaFile $metaSchemaPath | Should -BeTrue
    }

    It 'rejects an invalid schema in the meta-schema control' {
        Test-Json -Json '{"type":"unknown"}' -SchemaFile $metaSchemaPath -ErrorAction SilentlyContinue | Should -BeFalse
    }

    It 'treats an empty file as an empty runtime configuration without Schema validation' {
        [System.IO.File]::WriteAllText($configPath, '')
        $state = [SnippetPredictor.Config]::parseSnippetFile($configPath).GetAwaiter().GetResult()
        $state.IsEmpty | Should -BeTrue
    }
}
