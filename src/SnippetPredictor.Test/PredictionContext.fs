module SnippetPredictorTest.PredictionContext

open System.Text.Json
open Expecto
open Expecto.Flip
open SnippetPredictor
open SnippetPredictorTest.Utility.PredictionFixture

let private range input =
    context input |> PredictionInput.tryCommandRange

let private entries: SnippetEntry array =
    [|
        {
            Snippet = "digit"
            Tooltip = "substring"
            Group = null
            PredictionContexts = PredictionContexts.Command ||| PredictionContexts.Pipeline
        }
        {
            Snippet = "git status --short"
            Tooltip = "status"
            Group = "git"
            PredictionContexts = PredictionContexts.Command ||| PredictionContexts.Pipeline
        }
        {
            Snippet = "git"
            Tooltip = "exact"
            Group = "git"
            PredictionContexts = PredictionContexts.Command ||| PredictionContexts.Pipeline
        }
        {
            Snippet = "Write-Host git"
            Tooltip = "word start"
            Group = null
            PredictionContexts = PredictionContexts.Command ||| PredictionContexts.Pipeline
        }
        {
            Snippet = "git"
            Tooltip = "duplicate"
            Group = null
            PredictionContexts = PredictionContexts.Command ||| PredictionContexts.Pipeline
        }
        {
            Snippet = "Select-Object Name"
            Tooltip = "select"
            Group = null
            PredictionContexts = PredictionContexts.Command ||| PredictionContexts.Pipeline
        }
        {
            Snippet = "Out-File result"
            Tooltip = "output"
            Group = null
            PredictionContexts = PredictionContexts.Command ||| PredictionContexts.Pipeline
        }
    |]

let private configuration restricted sensitive =
    JsonSerializer.Serialize(
        {|
            Snippets = entries
            PredictOnlyWithIdentifier = restricted
            SearchCaseSensitive = sensitive
        |}
    )

[<Tests>]
let tests =
    testList
        "terminal command range"
        [
            test "preserves leading commands comments and pipelines" {
                for prefix, query, predictionContext in
                    [|
                        "Write-Host 'start.'; ", "git", PredictionContexts.Command
                        "<# 調査用 #> ", "git", PredictionContexts.Command
                        "Get-Content file | ", "Select", PredictionContexts.Pipeline
                        "Get-Content file | Select | ", "Out-F", PredictionContexts.Pipeline
                        "Write-Host '; |'; ", "git", PredictionContexts.Command
                        "<# ; | #> ", "git", PredictionContexts.Command
                        "Get-Content 'a|b;c' | ", "Select", PredictionContexts.Pipeline
                        "Write-Host x;   ", "git st --short  ", PredictionContexts.Command
                        "Write-Host x;\n", "git", PredictionContexts.Command
                        "Write-Host x; Get-Content file | ", "Select", PredictionContexts.Pipeline
                        "Get-Content file | Select; ", "git", PredictionContexts.Command
                    |] do
                    prefix + query
                    |> range
                    |> Expect.equal
                        "should replace the terminal command including trailing whitespace"
                        (ValueSome(struct (prefix.Length, query.Length, predictionContext)))
            }

            test "leaves unsupported input on the existing prediction path" {
                for input in
                    [|
                        ""
                        "   "
                        "git"
                        "  git  "
                        ":snp git"
                        "  :git"
                        "Write-Host x; :snp git"
                        "git;"
                        "git |"
                        "git | # comment"
                        "Write-Host x; git # comment"
                        "Write-Host x; git > result"
                        "Write-Host x; & git"
                        "Write-Host x; 'git'"
                        "if ($true) { git }"
                        "Write-Host x; $(git)"
                        "Write-Host x; & { git }"
                        "Write-Host x; git 'unfinished"
                        "Write-Host x; git \"unfinished"
                        "Write-Host x; git { unfinished"
                        "Write-Host x; git { Write-Host x }"
                        "Write-Host x; git $(Write-Host x)"
                        "begin { Write-Host x } end { git }"
                    |] do
                    input |> range |> Expect.equal "should retain whole-line prediction" ValueNone
            }

            test "handles Unicode and long trailing whitespace" {
                let prefix = "Write-Host x;\u3000\u2003"

                for whitespace in [| "\t\r\n\u3000\u2003"; String.replicate 65536 " " |] do
                    let query = "git" + whitespace

                    prefix + query
                    |> range
                    |> Expect.equal
                        "should consume trailing whitespace without recursive stack growth"
                        (ValueSome(struct (prefix.Length, query.Length, PredictionContexts.Command)))
            }

            test "returns full lines preserving ranking tooltips and duplicates" {
                withPredictor (configuration false false) entries.Length (fun cache suggest ->
                    for prefix, query in
                        [|
                            "Write-Host 'start.'; ", "git"
                            "<# 調査用 #> ", "git"
                            "Get-Content file | ", "Select"
                            "Get-Content file | Select | ", "Out-F"
                            "Write-Host '; |'; ", "git"
                            "<# ; | #> ", "git"
                            "Get-Content 'a|b;c' | ", "Select"
                            "Write-Host x;\n\u3000", "git  "
                            "Write-Host x; ", "git status --short"
                            "Write-Host x; ", "missing"
                        |] do
                        let expected =
                            cache.getPredictiveSuggestions query
                            |> pairs
                            |> Array.map (fun (text, tooltip) -> prefix + text, tooltip)

                        suggest (prefix + query)
                        |> Expect.equal "should compose only the terminal command" expected

                    suggest "Write-Host x; git"
                    |> Array.map snd
                    |> Expect.equal
                        "should rank exact prefix word-start and substring matches"
                        [| "[git]exact"; "duplicate"; "[git]status"; "word start"; "substring" |])
            }

            test "retains whole-line predictions for identifiers and unsupported contexts" {
                for restricted in [ false; true ] do
                    withPredictor (configuration restricted false) entries.Length (fun cache suggest ->
                        for input in
                            [|
                                ""
                                "   "
                                "git"
                                "  git  "
                                ":"
                                ":s"
                                ":snp"
                                ":snp git"
                                ":snp git; git"
                                ":git git; git"
                                ":tip status; git"
                                ":git"
                                ":git git"
                                ":tip status"
                                ":unknown"
                                "  :snp git"
                                "Write-Host x; :snp git"
                                "git;"
                                "git |"
                                "if ($true) { git }"
                                "Write-Host x; git # comment"
                                "Write-Host x; git > result"
                                "Write-Host x; git 'unfinished"
                                "Write-Host x; git $(Write-Host x)"
                            |] do
                            suggest input
                            |> Expect.equal
                                "should preserve the existing prediction path"
                                (cache.getPredictiveSuggestions input |> pairs)

                        if restricted then
                            for input in [| "Write-Host x; git"; "<# comment #> git"; "Get-Content file | Select" |] do
                                suggest input
                                |> Expect.isEmpty "should suppress ordinary contextual predictions")
            }

            test "uses configured case sensitivity for terminal command matching" {
                for sensitive in [ false; true ] do
                    withPredictor (configuration false sensitive) entries.Length (fun cache suggest ->
                        let expected =
                            cache.getPredictiveSuggestions "GIT"
                            |> pairs
                            |> Array.map (fun (text, tooltip) -> "Write-Host x; " + text, tooltip)

                        suggest "Write-Host x; GIT"
                        |> Expect.equal "should reuse the configured comparison" expected)
            }

            test "does not prepend leading commands to configuration diagnostics" {
                withPredictor "{invalid" 1 (fun cache suggest ->
                    suggest ":snp" |> Expect.isNonEmpty "should expose configuration errors"

                    for input in [| ":snp"; "parsing"; "Write-Host x; parsing"; "<# comment #> parsing" |] do
                        suggest input
                        |> Expect.equal
                            "should preserve diagnostics without composing command text"
                            (cache.getPredictiveSuggestions input |> pairs))
            }
        ]
