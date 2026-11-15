module SnippetPredictorTest.ConfigReading

open System.IO
open Expecto
open Expecto.Flip
open SnippetPredictor
open SnippetPredictorTest.Utility

let private load path =
    Config.parseSnippetFile(path).GetAwaiter().GetResult()

let private expectEmpty =
    function
    | Config.ConfigState.Empty -> ()
    | _ -> failtest "Expected an empty configuration"

let private expectInvalid message =
    function
    | Config.ConfigState.Invalid entry ->
        entry.Snippet |> Expect.equal "should identify the failure" message
        entry.Tooltip |> Expect.isNotEmpty "should include diagnostic details"
    | _ -> failtest "Expected an invalid configuration"

[<Tests>]
let tests =
    testList
        "asynchronous configuration reading"
        [
            test "reads a valid snippet" {
                use file =
                    new TempFile(
                        Config.snippetFilesName,
                        """{"Snippets":[{"Snippet":"git status","Tooltip":"status","Group":"git","PredictionContexts":["Command","Pipeline"]}]}"""
                    )

                let expected =
                    {
                        SnippetEntry.Snippet = "git status"
                        Tooltip = "status"
                        Group = "git"
                        PredictionContexts = PredictionContexts.Command ||| PredictionContexts.Pipeline
                    }

                match load (file.GetSnippetPath()) with
                | Config.ConfigState.Valid config ->
                    match config.Snippets with
                    | null -> failtest "Expected snippets"
                    | entries -> entries |> Expect.equal "should preserve snippet values" [| expected |]
                | _ -> failtest "Expected a valid configuration"
            }

            test "treats empty and whitespace files as empty configurations" {
                for content in [| ""; " \t\r\n" |] do
                    use file = new TempFile(Config.snippetFilesName, content)
                    file.GetSnippetPath() |> load |> expectEmpty
            }

            test "treats a missing file as an empty configuration" {
                use directory = new TempDirectory("SnippetPredictor.Test.")
                Path.Combine(directory.Path, Config.snippetFilesName) |> load |> expectEmpty
            }

            test "reports malformed JSON as an invalid configuration" {
                use file = new TempFile(Config.snippetFilesName, "{")

                file.GetSnippetPath()
                |> load
                |> expectInvalid "'An error occurred while parsing .snippet-predictor.json'"
            }

            test "reports file read failures as an invalid configuration" {
                use directory = new TempDirectory("SnippetPredictor.Test.")

                directory.Path
                |> load
                |> expectInvalid "'An error occurred while reading .snippet-predictor.json'"
            }
        ]
