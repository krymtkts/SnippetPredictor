module SnippetPredictorTest.PredictionFiltering

open System
open System.IO
open System.Text.Json
open Expecto
open Expecto.Flip
open SnippetPredictor
open SnippetPredictorTest.Utility
open SnippetPredictorTest.Utility.PredictionFixture
open SnippetPredictorTest.Snippet.CacheDisposeBehavior

let private both = PredictionContexts.Command ||| PredictionContexts.Pipeline

let private entries =
    [|
        Store.makeSnippetEntry [| PredictionContexts.Command |] "git status" "command prefix" "command"
        Store.makeSnippetEntry [| PredictionContexts.Pipeline |] "git" "pipeline exact" "pipeline"
        Store.makeSnippetEntry [| both |] "digit" "both substring" "both"
        Store.makeSnippetEntry [| PredictionContexts.IdentifierOnly |] "git log" "identifier prefix" "private"
        Store.makeSnippetEntry [| both |] "Write-Host git" "both word" "both"
        Store.makeSnippetEntry [| both |] "git" "both exact" "both"
        Store.makeSnippetEntry [| both |] "git" "duplicate" "both"
        Store.makeSnippetEntry [| PredictionContexts.Pipeline |] "Write-Host ':'" "pipeline colon" "pipeline"
        Store.makeSnippetEntry [| PredictionContexts.IdentifierOnly |] "Write-Host ':'" "identifier colon" "private"
    |]

let private configuration snippets restricted sensitive =
    JsonSerializer.Serialize(
        {|
            Snippets = snippets
            PredictOnlyWithIdentifier = restricted
            SearchCaseSensitive = sensitive
        |}
    )

let private expected leadingInput indices =
    indices
    |> Array.map (fun index ->
        let entry = entries[index]
        leadingInput + entry.Snippet, $"[{entry.Group}]{entry.Tooltip}")

[<Tests>]
let tests =
    testList
        "prediction context filtering"
        [
            test "filters command positions while preserving stable relevance and leading input" {
                withPredictor (configuration entries false false) entries.Length (fun cache suggest ->
                    for leadingInput in
                        [|
                            ""
                            "Write-Host x; "
                            "Write-Host x;"
                            "<# comment #> "
                            "Write-Host '; |'; "
                            "<# ; | #> "
                            "Get-Content file | Select; "
                            "Write-Host x;\n\u3000"
                        |] do
                        suggest (leadingInput + "git")
                        |> Expect.equal
                            "should include only command permissions"
                            (expected leadingInput [| 5; 6; 0; 4; 2 |])

                    cache.getPredictiveSuggestions "git"
                    |> pairs
                    |> Expect.equal "should filter the string-only prediction API" (expected "" [| 5; 6; 0; 4; 2 |])

                    suggest "  git  "
                    |> Expect.equal "should retain whitespace-only prefix trimming" (expected "" [| 5; 6; 0; 4; 2 |]))
            }

            test "filters pipeline positions and keeps command and identifier-only snippets out" {
                withPredictor (configuration entries false false) entries.Length (fun _ suggest ->
                    for leadingInput in
                        [|
                            "Get-Process | "
                            "Get-Process|"
                            "Get-Process | Select | "
                            "Write-Host x; Get-Process | "
                            "Get-Content 'a|b;c' | "
                            "<# ; | #> Get-Process | "
                        |] do
                        suggest (leadingInput + "git")
                        |> Expect.equal
                            "should include only pipeline permissions"
                            (expected leadingInput [| 1; 5; 6; 4; 2 |]))
            }

            test "requires explicit pipeline permission for omitted and null settings" {
                let json =
                    """{"Snippets":[
                        {"Snippet":"git status","Tooltip":"omitted"},
                        {"Snippet":"git status","Tooltip":"null","PredictionContexts":null}
                    ]}"""

                withPredictor json 2 (fun _ suggest ->
                    suggest "git"
                    |> Array.length
                    |> Expect.equal "should retain command predictions" 2

                    suggest "Write-Host x; git"
                    |> Array.length
                    |> Expect.equal "should retain statement predictions" 2

                    suggest "<# note #> git"
                    |> Array.length
                    |> Expect.equal "should retain comment-prefixed predictions" 2

                    suggest "Get-Process | git"
                    |> Expect.isEmpty "should not implicitly allow pipelines")
            }

            for permissions in [| PredictionContexts.Command; PredictionContexts.IdentifierOnly |] do
                testCase $"does not fall back when pipeline permissions exclude {permissions}" (fun () ->
                    let snippets =
                        [|
                            Store.makeSnippetEntry [| permissions |] "Get-Process | git" "whole input trap" null
                        |]

                    withPredictor (configuration snippets false false) 1 (fun _ suggest ->
                        suggest "Get-Process | git"
                        |> Expect.isEmpty "should not expose a whole-line fallback candidate"))

            test "does not expose pipeline-only or identifier-only snippets through unsupported input" {
                for input in [| "git > result"; "& git"; "git $(Write-Host x)"; "if ($true) { git }" |] do
                    let snippets =
                        [|
                            Store.makeSnippetEntry [| PredictionContexts.Command |] input "command" null
                            Store.makeSnippetEntry [| PredictionContexts.Pipeline |] input "pipeline" null
                            Store.makeSnippetEntry [| PredictionContexts.IdentifierOnly |] input "identifier" null
                        |]

                    withPredictor (configuration snippets false false) snippets.Length (fun _ suggest ->
                        suggest input
                        |> Expect.equal
                            "should use command permissions on the existing whole-line search"
                            [| input, "command" |])
            }

            test "keeps identifier predictions completion and accept independent of context permissions" {
                withPredictor (configuration entries false false) entries.Length (fun cache suggest ->
                    suggest ":snp git"
                    |> Expect.equal
                        "should rank every permitted identifier candidate"
                        (expected "" [| 1; 5; 6; 0; 3; 4; 2 |])

                    for input in [| ":snp"; ":tip" |] do
                        suggest input
                        |> Expect.equal "should retain all snippets without a search term" (expected "" [| 0..8 |])

                    suggest ":private"
                    |> Expect.equal "should retain identifier-only group predictions" (expected "" [| 3; 8 |])

                    suggest ":tip identifier"
                    |> Expect.equal "should retain identifier-only tooltip matching" (expected "" [| 3; 8 |])

                    for input in [| ":"; "  :  " |] do
                        suggest input
                        |> Expect.equal "should retain standalone colon predictions" (expected "" [| 7; 8 |])

                    cache.getCompletionTexts ":snp git"
                    |> Expect.equal
                        "should retain completion configuration order"
                        (entries[..6] |> Array.map _.Snippet)

                    cache.getCompletionTexts ":private git"
                    |> Expect.equal "should retain identifier-only completion" [| entries[3].Snippet |]

                    cache.getExactIdentifierSnippetTexts ":private"
                    |> Expect.equal
                        "should retain identifier-only accept candidates"
                        [| entries[3].Snippet; entries[8].Snippet |]

                    cache.getCompletionTexts ":p"
                    |> Expect.equal "should retain identifiers for restricted snippets" [| ":pipeline"; ":private" |]

                    suggest ":p"
                    |> Expect.equal
                        "should retain identifier predictions for restricted snippets"
                        [| ":pipeline", ""; ":private", "" |])
            }

            test "retains global prediction suppression" {
                withPredictor (configuration entries true false) entries.Length (fun _ suggest ->
                    for input in [| "git"; "Write-Host x; git"; "Get-Process | git"; "<# comment #> git" |] do
                        suggest input |> Expect.isEmpty "should suppress ordinary predictions"

                    suggest ":snp git"
                    |> Expect.equal "should retain explicit searches" (expected "" [| 1; 5; 6; 0; 3; 4; 2 |])

                    suggest ":"
                    |> Expect.equal "should retain the colon exception" (expected "" [| 7; 8 |]))
            }

            test "retains case sensitivity when filtering pipeline candidates" {
                withPredictor (configuration entries false true) entries.Length (fun _ suggest ->
                    suggest "Get-Process | GIT"
                    |> Expect.isEmpty "should honor case-sensitive matching"

                    suggest "Get-Process | git"
                    |> Expect.equal
                        "should still filter case-sensitive candidates"
                        (expected "Get-Process | " [| 1; 5; 6; 4; 2 |]))
            }

            test "refreshes permissions and leaves diagnostic candidates visible" {
                let command =
                    Store.makeSnippetEntry [| PredictionContexts.Command |] "git status" "status" null

                use file =
                    new TempFile(Config.snippetFilesName, configuration [| command |] false false)

                let directory = file.GetSnippetDirectoryPath()
                let path = file.GetSnippetPath()
                let watcher = new TestWatcher(directory, Config.snippetFilesName)
                use cache = new CacheForTest((fun _ -> watcher), ignore)

                let commandCount () =
                    cache.getPredictiveSuggestions "git" |> _.Count

                let pipelineCount () =
                    cache.getContextualSuggestions (context "Get-Process | git") |> _.Count

                let refresh (content: string) =
                    File.WriteAllText(path, content)
                    watcher.TriggerChanged(directory, Config.snippetFilesName)

                try
                    cache.load (fun () -> directory, path)

                    (fun () -> commandCount () = 1 && pipelineCount () = 0)
                    |> expectEventually "should initially use command permission"

                    configuration
                        [|
                            { command with
                                PredictionContexts = PredictionContexts.Pipeline
                            }
                        |]
                        false
                        false
                    |> refresh

                    (fun () -> commandCount () = 0 && pipelineCount () = 1)
                    |> expectEventually "should refresh to pipeline permission"

                    configuration
                        [|
                            { command with
                                PredictionContexts = PredictionContexts.IdentifierOnly
                            }
                        |]
                        false
                        false
                    |> refresh

                    (fun () ->
                        commandCount () = 0
                        && pipelineCount () = 0
                        && cache.getExactIdentifierSnippetTexts ":snp" = [| command.Snippet |])
                    |> expectEventually "should refresh to identifier-only mode"

                    "{invalid" |> refresh

                    (fun () -> cache.getPredictiveSuggestions "parsing" |> _.Count = 1)
                    |> expectEventually "should expose diagnostics after restricted configuration"

                    cache.getContextualSuggestions (context "Get-Process | parsing")
                    |> Expect.isEmpty "should not prepend leading input to diagnostics"

                    configuration [| command |] false false |> refresh

                    (fun () -> commandCount () = 1 && pipelineCount () = 0)
                    |> expectEventually "should recover command permission"
                finally
                    (cache :> IDisposable).Dispose()
                    watcher.ReleaseHandles()
            }
        ]
