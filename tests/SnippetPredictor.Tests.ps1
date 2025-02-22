Describe 'SnippetPredictor' {
    Context 'SnippetPredictor module' {
        It 'Given the SnippetPredictor module, it should have a nonzero version' {
            $m = Get-Module 'SnippetPredictor'
            $m.Version | Should -Not -Be $null
        }
        It 'Given the SnippetPredictor module, it should have <Expected> commands' -TestCases @(
            @{ Expected = @('Add-Snippet', 'Get-Snippet', 'Remove-Snippet') }
        ) {
            $m = Get-Module 'SnippetPredictor'
            ($m.ExportedCmdlets).Values | Select-Object -ExpandProperty Name | Should -Eq $Expected
        }
    }
}
