module SnippetPredictorTest.Snippet

open Expecto
open Expecto.Flip

open SnippetPredictor

[<Tests>]
let tests_parseSnippets =
    testList
        "parseSnippets"
        [

          test "when JSON is empty string" {
              ""
              |> Snippet.parseSnippets
              |> _.IsEmpty
              |> Expect.equal "should return ConfigState.Empty" true
          }

          test "when JSON is null" {
              "null"
              |> Snippet.parseSnippets
              |> function
                  | Snippet.ConfigState.Invalid entry -> entry
                  | _ -> failtest "Expected ConfigState.Invalid but got a different state"
              |> Expect.equal
                  "should return ConfigState.Invalid"
                  { SnippetEntry.Snippet = "'.snippet-predictor.json is null or invalid format.'"
                    SnippetEntry.Tooltip = "" }
          }

          test "when JSON is empty" {
              "{}"
              |> Snippet.parseSnippets
              |> function
                  | Snippet.ConfigState.Valid entry -> entry
                  | _ -> failtest "Expected ConfigState.Valid but got a different state"
              |> Expect.equal "should return ConfigState.Valid" { SnippetConfig.Snippets = null }
          }

          test "when JSON is broken" {
              "{"
              |> Snippet.parseSnippets
              |> function
                  | Snippet.ConfigState.Invalid entry -> entry
                  | _ -> failtest "Expected ConfigState.Invalid but got a different state"
              |> Expect.equal
                  "should return ConfigState.Invalid"
                  { SnippetEntry.Snippet = "'An error occurred while parsing .snippet-predictor.json'"
                    SnippetEntry.Tooltip =
                      "Expected depth to be zero at the end of the JSON payload. There is an open JSON object or array that should be closed. Path: $ | LineNumber: 0 | BytePositionInLine: 1." }
          }

          test "when JSON has null snippets" {
              """{"snippets":null}"""
              |> Snippet.parseSnippets
              |> function
                  | Snippet.ConfigState.Valid entry -> entry
                  | _ -> failtest "Expected ConfigState.Valid but got a different state"
              |> Expect.equal "should return ConfigState.Valid" { SnippetConfig.Snippets = null }
          }

          test "when JSON has empty snippets" {
              """{"snippets":[]}"""
              |> Snippet.parseSnippets
              |> function
                  | Snippet.ConfigState.Valid entry -> entry
                  | _ -> failtest "Expected ConfigState.Valid but got a different state"
              |> Expect.equal "should return ConfigState.Valid" { SnippetConfig.Snippets = [||] }
          }

          test "when JSON has snippets" {
              """{"snippets":[{"snippet": "echo 'example'", "tooltip": "example tooltip"}]}"""
              |> Snippet.parseSnippets
              |> function
                  | Snippet.ConfigState.Valid entry -> entry
                  | _ -> failtest "Expected ConfigState.Valid but got a different state"
              |> Expect.equal
                  "should return ConfigState.Valid"
                  { SnippetConfig.Snippets =
                      [| { SnippetEntry.Snippet = "echo 'example'"
                           SnippetEntry.Tooltip = "example tooltip" } |] }
          }

          test "when JSON has snippets with trailing comma" {
              """{
    // comment
    "snippets":[
        {"snippet": "echo 'example'", "tooltip": "example tooltip"},
    ]
}"""
              |> Snippet.parseSnippets
              |> function
                  | Snippet.ConfigState.Valid entry -> entry
                  | _ -> failtest "Expected ConfigState.Valid but got a different state"
              |> Expect.equal
                  "should return ConfigState.Valid"
                  { SnippetConfig.Snippets =
                      [| { SnippetEntry.Snippet = "echo 'example'"
                           SnippetEntry.Tooltip = "example tooltip" } |] }
          }

          ]

module getSnippet =
    open System

    let PathSeparator = IO.Path.DirectorySeparatorChar

    [<Tests>]
    let tests_getSnippetPathWith =
        testList
            "getSnippetPathWith"
            [

              test "when env var is set" {
                  Snippet.getSnippetPathWith (fun _ -> ".") (fun _ -> "")
                  |> Expect.equal
                      "should return the path based on env var."
                      (".", $".{PathSeparator}.snippet-predictor.json")
              }

              test "when env var is null" {
                  let userProfile = "/Users/username"

                  Snippet.getSnippetPathWith (fun _ -> null) (fun _ -> userProfile)
                  |> Expect.equal
                      "should return the default path"
                      (userProfile, $"{userProfile}{PathSeparator}.snippet-predictor.json")
              }

              test "when env var is empty" {
                  let userProfile = "/Users/username"

                  Snippet.getSnippetPathWith (fun _ -> "") (fun _ -> userProfile)
                  |> Expect.equal
                      "should return the default path"
                      (userProfile, $"{userProfile}{PathSeparator}.snippet-predictor.json")
              }

              ]

module getPredictiveSuggestions =
    open System
    open System.Management.Automation.Subsystem.Prediction

    [<Tests>]
    let tests_getPredictiveSuggestions =
        Snippet.snippets.Clear()

        let expected =
            { SnippetEntry.Snippet = "example code"
              SnippetEntry.Tooltip = "example tooltip" }

        [ expected ] |> List.iter Snippet.snippets.Enqueue

        testList
            "getPredictiveSuggestions"
            [

              test "when snippet symbol is set" {
                  Snippet.getPredictiveSuggestions ":snp      example    "
                  |> Expect.all
                      "should return the snippets filtered by the input removing snippet symbol."
                      (fun actual -> actual.SuggestionText = expected.Snippet && actual.ToolTip = expected.Tooltip)
              }

              test "when snippet symbol is not set" {
                  Snippet.getPredictiveSuggestions "    example    "
                  |> Expect.all "should return the snippets filtered by the input." (fun actual ->
                      actual.SuggestionText = expected.Snippet && actual.ToolTip = expected.Tooltip)
              }

              test "when " {
                  Snippet.getPredictiveSuggestions "    exo    "
                  |> Expect.isEmpty "should return empty."
              }

              ]
