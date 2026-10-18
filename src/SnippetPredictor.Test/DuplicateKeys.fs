module SnippetPredictorTest.DuplicateKeys

open System
open System.IO
open Expecto
open Expecto.Flip
open SnippetPredictor
open SnippetPredictorTest.Utility
open SnippetPredictorTest.Snippet.CacheDisposeBehavior

let private load json =
    use file = new TempFile(Config.snippetFilesName, json)
    let sync = Store.loadConfig file.GetSnippetPath

    let asyncState =
        Config.parseSnippetFile(file.GetSnippetPath()).GetAwaiter().GetResult()

    [| sync; asyncState |]

let private expectInvalid name path state =
    match state with
    | Config.ConfigState.Invalid entry ->
        entry.Tooltip.Contains($"Duplicate property: {name}")
        |> Expect.isTrue "should identify the duplicate property"

        entry.Tooltip.Contains($"Path: {path}")
        |> Expect.isTrue "should identify the containing object"
    | _ -> failtest "Expected an invalid configuration"

let private expectValid state =
    match state with
    | Config.ConfigState.Valid config -> config
    | _ -> failtest "Expected a valid configuration"

[<Tests>]
let tests =
    testList
        "duplicate configuration keys"
        [
            for name, value in
                [|
                    "Snippets", "[]"
                    "SearchCaseSensitive", "false"
                    "PredictOnlyWithIdentifier", "false"
                    "Extra", "{}"
                |] do
                for duplicate in [| name; name.ToLowerInvariant() |] do
                    testCase $"rejects root {name} then {duplicate}" (fun () ->
                        let json = $"{{\"{name}\":{value},\"{duplicate}\":{value}}}"
                        load json |> Array.iter (expectInvalid duplicate "$"))

            for name, value in
                [|
                    "Snippet", "\"git\""
                    "Tooltip", "null"
                    "Group", "\"git\""
                    "PredictionContexts", "[\"Command\"]"
                    "Extra", "{}"
                |] do
                for duplicate in [| name; name.ToLowerInvariant() |] do
                    testCase $"rejects entry {name} then {duplicate}" (fun () ->
                        let snippet = if name = "Snippet" then "" else "\"Snippet\":\"git\","

                        let json =
                            "{\"Snippets\":[{\"Snippet\":\"first\"},{"
                            + snippet
                            + $"\"{name}\":{value},\"{duplicate}\":{value}}}]}}"

                        load json |> Array.iter (expectInvalid duplicate "$.Snippets[1]"))

            for json, duplicate, path in
                [|
                    """{"SearchCaseSensitive":true,"searchcasesensitive":false}""", "searchcasesensitive", "$"
                    """{"searchcasesensitive":false,"SearchCaseSensitive":true}""", "SearchCaseSensitive", "$"
                    """{"Snippets":[],"\u0053nippets":[]}""", "Snippets", "$"
                    """{"Snippets":[{"Snippet":"git","snip\u0070et":"status"}]}""", "snippet", "$.Snippets[0]"
                    """{"Snippets":[{"snippet":"git","Snippet":"status"}]}""", "Snippet", "$.Snippets[0]"
                |] do
                testCase $"rejects different values or escaped names {json}" (fun () ->
                    load json |> Array.iter (expectInvalid duplicate path))

            for json in
                [|
                    """{"Snippets":null,"Snippets":[]}"""
                    """{"Snippets":42,"snippets":[]}"""
                    """{"Snippets":[{"Snippet":null,"snippet":"git"}]}"""
                    """{"Snippets":[{"Snippet":"git","snippet":null}]}"""
                    """{"Snippets":[{"Snippet":"git","Tooltip":42,"tooltip":"valid"}]}"""
                    """{"Snippets":[{"Snippet":"git","tooltip":"valid","Tooltip":42}]}"""
                |] do
                testCase $"rejects overwrites regardless of value validity {json}" (fun () ->
                    load json
                    |> Array.iter (function
                        | Config.ConfigState.Invalid _ -> ()
                        | _ -> failtest "Expected an invalid configuration"))

            for json in
                [|
                    "{}"
                    """{"Snippets":null}"""
                    """{"Snippets":[],"SearchCaseSensitive":null,"PredictOnlyWithIdentifier":null}"""
                    """{"snippets":[{"snippet":"git","tooltip":null,"group":null,"predictioncontexts":null}]}"""
                    """{"Snippets":[{"Snippet":"git"},{"Snippet":"git"}]}"""
                    """{"Extra":{"Key":1,"key":2},"Snippets":[{"Snippet":"git","Extra":{"Key":1,"key":2}}]}"""
                    """{"Snippets":[{"Snippet":"git","PredictionContexts":["Command","Pipeline"]}]}"""
                    """{/* comment */"snippets":[{"snippet":"git",}],}"""
                |] do
                testCase $"preserves supported input {json}" (fun () ->
                    let states = load json |> Array.map expectValid
                    states[0] |> Expect.equal "should use the same reading contract" states[1])

            for json in [| "null"; "[]"; "true"; "0"; "\"git\"" |] do
                testCase $"rejects non-object configuration {json}" (fun () ->
                    load json
                    |> Array.iter (function
                        | Config.ConfigState.Invalid _ -> ()
                        | _ -> failtest "Expected an invalid configuration"))

            test "preserves saving and subsequent loading" {
                use file =
                    new TempFile(
                        Config.snippetFilesName,
                        """{"Snippets":[{"Snippet":"git","PredictionContexts":"IdentifierOnly"}]}"""
                    )

                let config = Store.loadConfig file.GetSnippetPath |> expectValid

                config
                |> Config.storeConfig file.GetSnippetPath
                |> Expect.wantOk "should keep serialization independent of read-only converters"
                |> ignore

                Store.loadConfig file.GetSnippetPath
                |> expectValid
                |> Expect.equal "should round trip configuration" config
            }

            test "refreshes diagnostics and recovers after duplicate keys are removed" {
                use file =
                    new TempFile(Config.snippetFilesName, """{"Snippets":[{"Snippet":"git"}]}""")

                let directory = file.GetSnippetDirectoryPath()
                let path = file.GetSnippetPath()
                let watcher = new TestWatcher(directory, Config.snippetFilesName)
                use cache = new CacheForTest((fun _ -> watcher), ignore)

                let refresh (json: string) =
                    File.WriteAllText(path, json)
                    watcher.TriggerChanged(directory, Config.snippetFilesName)

                try
                    cache.load (fun () -> directory, path)

                    (fun () -> cache.getExactIdentifierSnippetTexts ":snp" = [| "git" |])
                    |> expectEventually "should finish initial loading"

                    """{"Snippets":[{"Snippet":"git","snippet":"status"}]}""" |> refresh

                    (fun () ->
                        cache.getPredictiveSuggestions "parsing"
                        |> Seq.exists (fun suggestion ->
                            match suggestion.ToolTip with
                            | null -> false
                            | tooltip -> tooltip.Contains("Duplicate property: snippet")))
                    |> expectEventually "should expose duplicate-key diagnostics"

                    """{"Snippets":[{"Snippet":"status"}]}""" |> refresh

                    (fun () -> cache.getExactIdentifierSnippetTexts ":snp" = [| "status" |])
                    |> expectEventually "should recover after correcting the configuration"
                finally
                    (cache :> IDisposable).Dispose()
                    watcher.ReleaseHandles()
            }
        ]
