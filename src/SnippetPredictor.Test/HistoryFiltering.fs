module SnippetPredictorTest.HistoryFiltering

open System
open System.IO
open System.Text.Json
open Expecto
open Expecto.Flip
open SnippetPredictor
open SnippetPredictorTest.Utility
open SnippetPredictorTest.Utility.PredictionFixture
open SnippetPredictorTest.Snippet.CacheDisposeBehavior

let private entry text =
    Store.makeSnippetEntry [| PredictionContexts.Command |] text "tooltip only" "group"

let private configuration sensitive restricted entries =
    JsonSerializer.Serialize
        {
            SearchCaseSensitive = sensitive
            PredictOnlyWithIdentifier = restricted
            Snippets = entries
        }

[<Tests>]
let tests =
    testList
        "history matching"
        [
            test "handles omitted null and empty snippet arrays" {
                for content in [| "{}"; """{"Snippets":null}"""; """{"Snippets":[]}""" |] do
                    withPredictor content 0 (fun cache _ ->
                        cache.isRegisteredSnippet "echo test"
                        |> Expect.isFalse "should not match an empty valid configuration")
            }

            test "matches only complete ordinal snippet text" {
                let texts = [| "git status"; "Write-Host 日本語"; "first\r\nsecond" |]

                withPredictor (configuration false false (Array.map entry texts)) texts.Length (fun cache _ ->
                    for text in texts do
                        cache.isRegisteredSnippet text |> Expect.isTrue "should match the complete text"

                        cache.isRegisteredSnippet (String(text.ToCharArray()))
                        |> Expect.isTrue "should match a different string instance"

                    for text in
                        [|
                            ""
                            "git"
                            "GIT status"
                            " git status"
                            "git status "
                            "first\nsecond"
                            "tooltip only"
                            "group"
                            ":snp"
                            "Write-Host x; git status"
                            "Get-Process | git status"
                        |] do
                        cache.isRegisteredSnippet text
                        |> Expect.isFalse "should not normalize or search substrings")
            }

            test "ignores prediction permissions and search settings" {
                for sensitive in [ false; true ] do
                    for restricted in [ false; true ] do
                        let entries =
                            [|
                                PredictionContexts.Command
                                PredictionContexts.Pipeline
                                PredictionContexts.IdentifierOnly
                            |]
                            |> Array.mapi (fun index permissions ->
                                { entry $"echo {index}" with
                                    PredictionContexts = permissions
                                })

                        withPredictor (configuration sensitive restricted entries) entries.Length (fun cache _ ->
                            for snippet in entries do
                                cache.isRegisteredSnippet snippet.Snippet
                                |> Expect.isTrue "should include every registered context"

                                cache.isRegisteredSnippet (snippet.Snippet.ToUpperInvariant())
                                |> Expect.isFalse "should ignore case-insensitive search settings")
            }

            test "keeps duplicate predictions and matches registered composed text" {
                let texts =
                    [|
                        "git status"
                        "git status"
                        "Write-Host x; git status"
                        "Get-Process | Select-Object Name"
                    |]

                withPredictor (configuration false false (Array.map entry texts)) texts.Length (fun cache suggest ->
                    cache.getCompletionTexts ":snp"
                    |> Expect.equal "should preserve order and duplicates" texts

                    suggest ":snp git status"
                    |> Array.map fst
                    |> Expect.equal "should preserve duplicate predictions" texts[0..2]

                    for text in texts do
                        cache.isRegisteredSnippet text
                        |> Expect.isTrue "should match registered composed text")
            }

            test "returns false before loading and after disposal" {
                use file =
                    new TempFile(Config.snippetFilesName, configuration false false [| entry "echo initial" |])

                use cache = new Suggestion.Cache()

                cache.isRegisteredSnippet "echo initial"
                |> Expect.isFalse "should not match before load"

                cache.load (fun () -> file.GetSnippetDirectoryPath(), file.GetSnippetPath())

                (fun () -> cache.isRegisteredSnippet "echo initial")
                |> expectEventually "should finish initial load"

                (cache :> IDisposable).Dispose()

                cache.isRegisteredSnippet "echo initial"
                |> Expect.isFalse "should not match after disposal"
            }

            test "refreshes the index and never includes diagnostic text" {
                use file =
                    new TempFile(Config.snippetFilesName, configuration false false [| entry "echo old" |])

                let directory = file.GetSnippetDirectoryPath()
                let path = file.GetSnippetPath()
                let watcher = new TestWatcher(directory, Config.snippetFilesName)
                use cache = new CacheForTest((fun _ -> watcher), ignore)

                let refresh (content: string) =
                    File.WriteAllText(path, content)
                    watcher.TriggerChanged(directory, Config.snippetFilesName)

                try
                    cache.load (fun () -> directory, path)

                    (fun () -> cache.isRegisteredSnippet "echo old")
                    |> expectEventually "should load the original index"

                    configuration false false [| entry "echo new" |] |> refresh

                    (fun () ->
                        cache.isRegisteredSnippet "echo new"
                        && not (cache.isRegisteredSnippet "echo old"))
                    |> expectEventually "should replace the index with the new snapshot"

                    "{" |> refresh

                    (fun () -> cache.getPredictiveSuggestions "parsing" |> _.Count = 1)
                    |> expectEventually "should publish invalid configuration"

                    cache.isRegisteredSnippet "echo new"
                    |> Expect.isFalse "should clear the previous index"

                    for diagnostic in cache.getPredictiveSuggestions "parsing" do
                        cache.isRegisteredSnippet diagnostic.SuggestionText
                        |> Expect.isFalse "should not index diagnostic entries"

                    configuration false false [| entry "echo restored" |] |> refresh

                    (fun () -> cache.isRegisteredSnippet "echo restored")
                    |> expectEventually "should recover the index"

                    configuration false false Array.empty |> refresh

                    (fun () -> cache.getExactIdentifierSnippetTexts ":snp" |> Array.isEmpty)
                    |> expectEventually "should load an empty valid configuration"

                    cache.isRegisteredSnippet "echo restored"
                    |> Expect.isFalse "should clear an empty index"

                    configuration false false [| entry "echo removed" |] |> refresh

                    (fun () -> cache.isRegisteredSnippet "echo removed")
                    |> expectEventually "should reload before removing the file"

                    File.Delete path
                    watcher.TriggerChanged(directory, Config.snippetFilesName)

                    (fun () -> not (cache.isRegisteredSnippet "echo removed"))
                    |> expectEventually "should clear the index for a missing file"

                    cache.isRegisteredSnippet ""
                    |> Expect.isFalse "should not match missing configuration"
                finally
                    (cache :> IDisposable).Dispose()
                    watcher.ReleaseHandles()
            }
        ]
