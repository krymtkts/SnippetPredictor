module SnippetPredictorTest.PredictionScope

open System
open System.IO
open System.Management.Automation.Subsystem.Prediction
open System.Text.Json
open System.Threading
open Expecto
open Expecto.Flip
open SnippetPredictor
open SnippetPredictorTest.Utility
open SnippetPredictorTest.Snippet.CacheDisposeBehavior

let private entries =
    [|
        {
            Snippet = "git status"
            Tooltip = "working tree"
            Group = "git"
        }
        {
            Snippet = "git"
            Tooltip = "git command"
            Group = "git"
        }
        {
            Snippet = "Write-Host ':'"
            Tooltip = "colon"
            Group = null
        }
    |]

let private json predictOnlyWithIdentifier =
    JsonSerializer.Serialize(
        {
            SearchCaseSensitive = false
            PredictOnlyWithIdentifier = predictOnlyWithIdentifier
            Snippets = entries
        }
    )

let private parseConfiguration content =
    use file = new TempFile(Config.snippetFilesName, content)
    Store.loadConfig file.GetSnippetPath

let private prediction input (cache: Suggestion.Cache) =
    cache.getPredictiveSuggestions input
    |> Seq.map (fun suggestion -> suggestion.SuggestionText, suggestion.ToolTip)
    |> Seq.toArray

let private withCache enabled action =
    use file = new TempFile(Config.snippetFilesName, json enabled)
    use cache = new Suggestion.Cache()
    cache.load (fun () -> file.GetSnippetDirectoryPath(), file.GetSnippetPath())

    (fun () -> cache.getCompletionTexts ":snp" |> Array.length = entries.Length)
    |> expectEventually "should load prediction scope fixture"

    action cache

[<Tests>]
let tests =
    testList
        "prediction scope"
        [
            test "defaults to ordinary predictions for omitted and null configuration" {
                for configuration in
                    [|
                        "{}"
                        """{"PredictOnlyWithIdentifier":null}"""
                        """{"predictonlywithidentifier":false}"""
                    |] do
                    match parseConfiguration configuration with
                    | Config.ConfigState.Valid config ->
                        config.PredictOnlyWithIdentifier
                        |> Expect.isFalse "should retain ordinary predictions"
                    | _ -> failtest "Expected a valid configuration"
            }

            test "round trips explicit prediction scope" {
                for enabled in [ false; true ] do
                    match json enabled |> parseConfiguration with
                    | Config.ConfigState.Valid config ->
                        config.PredictOnlyWithIdentifier
                        |> Expect.equal "should retain the setting" enabled
                    | _ -> failtest "Expected a valid configuration"

                match parseConfiguration """{"predictonlywithidentifier":true}""" with
                | Config.ConfigState.Valid config ->
                    config.PredictOnlyWithIdentifier
                    |> Expect.isTrue "should ignore property name casing"
                | _ -> failtest "Expected a valid configuration"
            }

            test "rejects non-boolean prediction scope values with JSON paths" {
                for value in [| "\"true\""; "1"; "[]"; "{}" |] do
                    match parseConfiguration ($"{{\"PredictOnlyWithIdentifier\":{value}}}") with
                    | Config.ConfigState.Invalid entry ->
                        entry.Tooltip.Contains("$.PredictOnlyWithIdentifier")
                        |> Expect.isTrue "should report the invalid property's path"
                    | _ -> failtest "Expected an invalid configuration"
            }

            test "suppresses ordinary input without changing identifier predictions" {
                withCache false (fun ordinary ->
                    withCache true (fun restricted ->
                        for input in [| "git"; "  git  "; "x :snp git"; "Write-Host"; ":-" |] do
                            restricted
                            |> prediction input
                            |> Expect.isEmpty "should suppress non-identifier input"

                        ordinary
                        |> prediction "git"
                        |> Expect.isNonEmpty "should retain default predictions"

                        for input in
                            [|
                                ":snp"
                                ":snp git"
                                ":git"
                                ":git git"
                                "  :git git"
                                ":tip working"
                                ":tip"
                                ":s"
                                ":g"
                                ":unknown"
                                ":"
                                "  :  "
                                ""
                                "   "
                            |] do
                            restricted
                            |> prediction input
                            |> Expect.equal "should retain existing identifier results" (ordinary |> prediction input)

                        restricted
                        |> prediction ":"
                        |> Expect.isNonEmpty "should retain literal colon predictions"

                        restricted
                        |> prediction ":snp git"
                        |> Array.map fst
                        |> Expect.equal "should retain relevance ordering" [| "git"; "git status" |]))
            }

            test "does not change completion accept or unknown identifier detection" {
                withCache false (fun ordinary ->
                    withCache true (fun restricted ->
                        for input in
                            [|
                                ":"
                                ":s"
                                ":snp"
                                ":snp git"
                                ":git"
                                ":git git"
                                ":tip"
                                ":unknown"
                                "x :snp"
                            |] do
                            restricted.getCompletionTexts input
                            |> Expect.equal "should retain completion" (ordinary.getCompletionTexts input)

                            restricted.getExactIdentifierSnippetTexts input
                            |> Expect.equal
                                "should retain accept candidates"
                                (ordinary.getExactIdentifierSnippetTexts input)

                            restricted.isUnknownGroupIdentifier input
                            |> Expect.equal
                                "should retain unknown identifier detection"
                                (ordinary.isUnknownGroupIdentifier input)))
            }

            test "preserves prediction scope when adding and removing snippets" {
                use file = new TempFile(Config.snippetFilesName, json true)

                [|
                    {
                        Snippet = "git log"
                        Tooltip = "history"
                        Group = "git"
                    }
                |]
                |> Store.addSnippets file.GetSnippetPath
                |> Expect.wantOk "should add a snippet"
                |> ignore

                match Store.loadConfig file.GetSnippetPath with
                | Config.ConfigState.Valid config ->
                    config.PredictOnlyWithIdentifier
                    |> Expect.isTrue "should preserve scope after adding"
                | _ -> failtest "Expected a valid configuration"

                [| "git log" |]
                |> Store.removeSnippets file.GetSnippetPath
                |> Expect.wantOk "should remove a snippet"
                |> ignore

                match Store.loadConfig file.GetSnippetPath with
                | Config.ConfigState.Valid config ->
                    config.PredictOnlyWithIdentifier
                    |> Expect.isTrue "should preserve scope after removing"

                    config.Snippets |> Expect.equal "should retain other snippets" entries
                | _ -> failtest "Expected a valid configuration"
            }

            test "refreshes prediction scope and does not suppress error notifications" {
                use file = new TempFile(Config.snippetFilesName, json false)
                let directory = file.GetSnippetDirectoryPath()
                let path = file.GetSnippetPath()
                let watcher = new TestWatcher(directory, Config.snippetFilesName)
                use cache = new CacheForTest((fun _ -> watcher), ignore)

                try
                    cache.load (fun () -> directory, path)

                    (fun () -> cache |> prediction "git" |> Array.length = 2)
                    |> expectEventually "should initially predict ordinary input"

                    File.WriteAllText(path, json true)
                    watcher.TriggerChanged(directory, Config.snippetFilesName)

                    (fun () -> cache |> prediction "git" |> Array.isEmpty)
                    |> expectEventually "should enable identifier-only predictions"

                    File.WriteAllText(path, json false)
                    watcher.TriggerChanged(directory, Config.snippetFilesName)

                    (fun () -> cache |> prediction "git" |> Array.length = 2)
                    |> expectEventually "should restore ordinary predictions"

                    File.WriteAllText(path, json true)
                    watcher.TriggerChanged(directory, Config.snippetFilesName)

                    (fun () -> cache |> prediction "git" |> Array.isEmpty)
                    |> expectEventually "should re-enable identifier-only predictions"

                    File.WriteAllText(path, "{")
                    watcher.TriggerChanged(directory, Config.snippetFilesName)

                    (fun () -> cache |> prediction "parsing" |> Array.isEmpty |> not)
                    |> expectEventually "should expose parse failures through ordinary predictions"

                    File.WriteAllText(path, json true)
                    watcher.TriggerChanged(directory, Config.snippetFilesName)

                    (fun () -> cache.getExactIdentifierSnippetTexts ":snp" |> Array.length = entries.Length)
                    |> expectEventually "should recover valid configuration"

                    cache
                    |> prediction "git"
                    |> Expect.isEmpty "should recover the restricted scope"

                    File.Delete path
                    Directory.CreateDirectory path |> ignore
                    watcher.TriggerChanged(directory, Config.snippetFilesName)

                    (fun () -> cache |> prediction "reading" |> Array.isEmpty |> not)
                    |> expectEventually "should expose read failures through ordinary predictions"

                    Directory.Delete path
                    watcher.TriggerChanged(directory, Config.snippetFilesName)

                    (fun () -> cache |> prediction ":snp" |> Array.isEmpty)
                    |> expectEventually "should clear predictions for a missing file"
                finally
                    (cache :> IDisposable).Dispose()
                    watcher.ReleaseHandles()
            }

            test "returns an empty suggestion package only for ordinary input" {
                use file = new TempFile(Config.snippetFilesName, json true)

                use predictor =
                    new SnippetPredictor(
                        Guid.NewGuid().ToString(),
                        fun () -> file.GetSnippetDirectoryPath(), file.GetSnippetPath()
                    )

                (fun () -> predictor.GetCompletionTexts ":snp" |> Array.length = entries.Length)
                |> expectEventually "should load predictor fixture"

                let client = PredictionClient("scope test", PredictionClientKind.Terminal)

                let suggest input =
                    (predictor :> ICommandPredictor)
                        .GetSuggestion(client, PredictionContext.Create(input), CancellationToken.None)
                        .SuggestionEntries

                suggest "git"
                |> Expect.isNull "should return an empty package for ordinary input"

                suggest ":snp git"
                |> Expect.isNotNull "should retain identifier prediction packages"
            }
        ]
