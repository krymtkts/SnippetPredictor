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
              """{"snippets":[{"snippet": "echo example", "tooltip": "example tooltip"}]}"""
              |> Snippet.parseSnippets
              |> function
                  | Snippet.ConfigState.Valid entry -> entry
                  | _ -> failtest "Expected ConfigState.Valid but got a different state"
              |> Expect.equal
                  "should return ConfigState.Valid"
                  { SnippetConfig.Snippets =
                      [| { SnippetEntry.Snippet = "echo example"
                           SnippetEntry.Tooltip = "example tooltip" } |] }
          }

          test "when JSON has snippets with trailing comma" {
              """{"snippets":[{"snippet": "echo example", "tooltip": "example tooltip"},]}"""
              |> Snippet.parseSnippets
              |> function
                  | Snippet.ConfigState.Valid entry -> entry
                  | _ -> failtest "Expected ConfigState.Valid but got a different state"
              |> Expect.equal
                  "should return ConfigState.Valid"
                  { SnippetConfig.Snippets =
                      [| { SnippetEntry.Snippet = "echo example"
                           SnippetEntry.Tooltip = "example tooltip" } |] }
          }

          ]
