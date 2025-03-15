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
                    SnippetEntry.Tooltip = ""
                    SnippetEntry.Group = null }
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
                      "Expected depth to be zero at the end of the JSON payload. There is an open JSON object or array that should be closed. Path: $ | LineNumber: 0 | BytePositionInLine: 1."
                    SnippetEntry.Group = null }

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
                           SnippetEntry.Tooltip = "example tooltip"
                           SnippetEntry.Group = null } |] }
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
                           SnippetEntry.Tooltip = "example tooltip"
                           SnippetEntry.Group = null } |] }
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

    [<Tests>]
    let tests_getPredictiveSuggestions =
        let cache = Snippet.Cache()
        cache.load (fun () -> "./", "./.snippet-predictor-valid.json")

        let expected =
            { SnippetEntry.Snippet = "echo 'example'"
              SnippetEntry.Tooltip = "example tooltip"
              SnippetEntry.Group = "group" }

        testList
            "getPredictiveSuggestions"
            [

              test "when snippet symbol is set and matched" {
                  cache.getPredictiveSuggestions ":snp      echo    "
                  |> Expect.all
                      "should return the snippets filtered by the input removing snippet symbol."
                      (fun actual -> actual.SuggestionText = expected.Snippet && actual.ToolTip = expected.Tooltip)
              }

              test "when snippet symbol is not set and not matched" {
                  cache.getPredictiveSuggestions "    echo    "
                  |> Expect.all "should return the snippets filtered by the input." (fun actual ->
                      actual.SuggestionText = expected.Snippet && actual.ToolTip = expected.Tooltip)
              }

              test "when no snippets matched" {
                  cache.getPredictiveSuggestions "    exo    "
                  |> Expect.isEmpty "should return empty."
              }

              test "when input is whitespace" {
                  cache.getPredictiveSuggestions "    " |> Expect.isEmpty "should return empty."
              }

              test "when tooltip symbol is set and matched" {
                  cache.getPredictiveSuggestions ":tip      tooltip    "
                  |> Expect.all
                      "should return the snippets filtered by the input removing tooltip symbol."
                      (fun actual -> actual.SuggestionText = expected.Snippet && actual.ToolTip = expected.Tooltip)
              }

              test "when tooltip symbol is not set and not matched" {
                  cache.getPredictiveSuggestions "    tooltip    "
                  |> Expect.isEmpty "should return empty."
              }

              test "when group symbol is set and matched" {
                  cache.getPredictiveSuggestions ":group     "
                  |> Expect.all "should return the snippets filtered by the input removing group symbol." (fun actual ->
                      actual.SuggestionText = expected.Snippet && actual.ToolTip = expected.Tooltip)
              }

              test "when no group symbol is set and not matched" {
                  cache.getPredictiveSuggestions "    group    "
                  |> Expect.isEmpty "should return empty."
              }

              ]

[<Tests>]
let tests_loadSnippets =
    testList
        "loadSnippets"
        [

          test "when snippet file is not found" {
              Snippet.loadSnippets (fun () -> "./", "./not-found.json")
              |> function
                  | Ok s -> s
                  | Error e -> failtest $"Expected Error but got Error. {e}"
              |> Expect.isEmpty "should return Empty"
          }

          test "when snippet file is invalid" {
              Snippet.loadSnippets (fun () -> "./", "./.snippet-predictor-invalid.json")
              |> function
                  | Ok _ -> failtest "Expected Error but got Ok."
                  | Error e -> e
              |> Expect.equal
                  "should return Error entry"
                  "'An error occurred while parsing .snippet-predictor.json': Expected depth to be zero at the end of the JSON payload. There is an open JSON object or array that should be closed. Path: $.Snippets[0] | LineNumber: 1 | BytePositionInLine: 15."
          }

          test "when snippet file is valid and null" {
              Snippet.loadSnippets (fun () -> "./", "./.snippet-predictor-null.json")
              |> function
                  | Ok _ -> failtest "Expected Error but got Ok."
                  | Error e -> e
              |> Expect.equal "should return Error entry" "'.snippet-predictor.json is null or invalid format.'"
          }

          test "when snippet file is valid and snippets is null" {
              Snippet.loadSnippets (fun () -> "./", "./.snippet-predictor-snippet-null.json")
              |> function
                  | Ok s -> s
                  | Error e -> failtest $"Expected Error but got Error. {e}"
              |> Expect.isEmpty "should return Empty"
          }

          test "when snippet file is valid" {
              let expected =
                  [| { SnippetEntry.Snippet = "echo 'example'"
                       SnippetEntry.Tooltip = "example tooltip"
                       SnippetEntry.Group = "group" } |]

              Snippet.loadSnippets (fun () -> "./", "./.snippet-predictor-valid.json")
              |> function
                  | Ok s -> s
                  | Error e -> failtest $"Expected Error but got Error. {e}"
              |> Expect.equal "should return snippets" expected
          }

          ]

module addAndRemoveSnippets =
    open System
    open System.IO

    let normalizeNewlines (s: string) = s.Replace("\r\n", "\n")

    type TempDirectory(directory: string) =
        member val Path: string = Directory.CreateTempSubdirectory(directory).FullName

        interface IDisposable with
            member __.Dispose() =
                if Directory.Exists(__.Path) then
                    Directory.Delete(__.Path, true)
                else
                    failwith $"Directory '{__.Path}' does not exist. maybe the test failed to create it."

    [<Tests>]
    let tests_addSnippets =
        testList
            "addSnippets"
            [

              test "when snippet file is not found" {
                  use tmp = new TempDirectory("SnippetPredictor.Test.")
                  let path = Path.Combine(tmp.Path, "not-found.json")

                  [ { SnippetEntry.Snippet = "echo '1'"
                      SnippetEntry.Tooltip = "1 tooltip"
                      SnippetEntry.Group = null } ]
                  |> Snippet.addSnippets (fun () -> tmp.Path, path)
                  |> function
                      | Ok s -> s
                      | Error e -> failtest $"Expected Error but got Error. {e}"
                  |> Expect.equal "should return Ok" ()

                  let expected =
                      """{
  "Snippets": [
    {
      "Snippet": "echo '1'",
      "Tooltip": "1 tooltip"
    }
  ]
}"""
                      |> normalizeNewlines

                  File.ReadAllText(path)
                  |> normalizeNewlines
                  |> Expect.equal "should create the snippet file" expected
              }

              test "when snippet file is invalid" {
                  use tmp = new TempDirectory("SnippetPredictor.Test.")
                  let path = Path.Combine(tmp.Path, ".snippet-predictor-invalid.json")
                  File.WriteAllText(path, """{"Snippets":[}""")

                  [ { SnippetEntry.Snippet = "echo '2'"
                      SnippetEntry.Tooltip = "2 tooltip"
                      SnippetEntry.Group = null } ]
                  |> Snippet.addSnippets (fun () -> tmp.Path, path)
                  |> function
                      | Ok _ -> failtest "Expected Error but got Ok."
                      | Error e -> e
                  |> Expect.equal
                      "should return Error entry"
                      "'An error occurred while parsing .snippet-predictor.json': '}' is an invalid start of a value. Path: $.Snippets[0] | LineNumber: 0 | BytePositionInLine: 13."
              }

              test "when snippet file is valid" {
                  use tmp = new TempDirectory("SnippetPredictor.Test.")
                  let path = Path.Combine(tmp.Path, ".snippet-predictor-valid.json")
                  File.WriteAllText(path, """{"Snippets": []}""")

                  [| { SnippetEntry.Snippet = "echo '3'"
                       SnippetEntry.Tooltip = "3 tooltip"
                       SnippetEntry.Group = null } |]
                  |> Snippet.addSnippets (fun () -> tmp.Path, path)
                  |> function
                      | Ok s -> s
                      | Error e -> failtest $"Expected Error but got Error. {e}"
                  |> Expect.equal "should return snippets" ()

                  let expected =
                      """{
  "Snippets": [
    {
      "Snippet": "echo '3'",
      "Tooltip": "3 tooltip"
    }
  ]
}"""
                      |> normalizeNewlines

                  File.ReadAllText(path)
                  |> normalizeNewlines
                  |> Expect.equal "should add the snippet to snippet file" expected

              }

              test "when snippet file is valid and null" {
                  use tmp = new TempDirectory("SnippetPredictor.Test.")
                  let path = Path.Combine(tmp.Path, ".snippet-predictor-valid.json")
                  File.WriteAllText(path, """{"Snippets": null}""")

                  [| { SnippetEntry.Snippet = "echo '3'"
                       SnippetEntry.Tooltip = "3 tooltip"
                       SnippetEntry.Group = null } |]
                  |> Snippet.addSnippets (fun () -> tmp.Path, path)
                  |> function
                      | Ok s -> s
                      | Error e -> failtest $"Expected Error but got Error. {e}"
                  |> Expect.equal "should return snippets" ()

                  let expected =
                      """{
  "Snippets": [
    {
      "Snippet": "echo '3'",
      "Tooltip": "3 tooltip"
    }
  ]
}"""
                      |> normalizeNewlines

                  File.ReadAllText(path)
                  |> normalizeNewlines
                  |> Expect.equal "should add the snippet to snippet file" expected

              }

              ]

    [<Tests>]
    let tests_removeSnippets =
        testList
            "removeSnippets"
            [

              test "when snippet file is not found" {
                  use tmp = new TempDirectory("SnippetPredictor.Test.")
                  let path = Path.Combine(tmp.Path, "not-found.json")

                  [ "echo '1'" ]
                  |> Snippet.removeSnippets (fun () -> tmp.Path, path)
                  |> function
                      | Ok s -> s
                      | Error e -> failtest $"Expected Error but got Error. {e}"
                  |> Expect.equal "should return Ok" ()

                  Directory.GetFiles(tmp.Path)
                  |> Expect.isEmpty "should not create the snippet file"
              }

              test "when snippet file is invalid" {
                  use tmp = new TempDirectory("SnippetPredictor.Test.")
                  let path = Path.Combine(tmp.Path, ".snippet-predictor-invalid.json")
                  File.WriteAllText(path, """{"Snippets":[}""")

                  [ "echo '2'" ]
                  |> Snippet.removeSnippets (fun () -> tmp.Path, path)
                  |> function
                      | Ok _ -> failtest "Expected Error but got Ok."
                      | Error e -> e
                  |> Expect.equal
                      "should return Error entry"
                      "'An error occurred while parsing .snippet-predictor.json': '}' is an invalid start of a value. Path: $.Snippets[0] | LineNumber: 0 | BytePositionInLine: 13."
              }

              test "when snippet file is valid" {
                  use tmp = new TempDirectory("SnippetPredictor.Test.")
                  let path = Path.Combine(tmp.Path, ".snippet-predictor-valid.json")
                  File.AppendAllText(path, """{"Snippet": "echo '3'", "Tooltip": "3 tooltip"}""")

                  [ "echo '1'"; "echo '3'"; "echo '3'" ]
                  |> Snippet.removeSnippets (fun () -> tmp.Path, path)
                  |> function
                      | Ok s -> s
                      | Error e -> failtest $"Expected Error but got Error. {e}"
                  |> Expect.equal "should return snippets" ()

                  let expected =
                      """{
  "Snippets": []
}"""
                      |> normalizeNewlines

                  File.ReadAllText(path)
                  |> normalizeNewlines
                  |> Expect.equal "should remove the snippet from snippet file" expected
              }

              ]
