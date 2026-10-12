module SnippetPredictorTest.PredictionContext

open System.Management.Automation.Subsystem.Prediction
open System.Management.Automation.Language
open Expecto
open Expecto.Flip
open SnippetPredictor

let private range input =
    let ast, tokens, _ = Parser.ParseInput input
    PredictionContext(ast, tokens) |> PredictionInput.tryCommandRange

[<Tests>]
let tests =
    testList
        "terminal command range"
        [
            test "preserves leading commands comments and pipelines" {
                for prefix, query in
                    [|
                        "Write-Host 'start.'; ", "git"
                        "<# 調査用 #> ", "git"
                        "Get-Content file | ", "Select"
                        "Get-Content file | Select | ", "Out-F"
                        "Write-Host '; |'; ", "git"
                        "<# ; | #> ", "git"
                        "Get-Content 'a|b;c' | ", "Select"
                        "Write-Host x;   ", "git st --short  "
                        "Write-Host x;\n", "git"
                    |] do
                    prefix + query
                    |> range
                    |> Expect.equal
                        "should replace the terminal command including trailing whitespace"
                        (ValueSome(struct (prefix.Length, query.Length)))
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
                        (ValueSome(struct (prefix.Length, query.Length)))
            }
        ]
