module Snippet

open System
open System.IO
open System.Collections
open System.Management.Automation.Subsystem.Prediction
open System.Text.Json
open System.Threading

type SnippetEntry = { snippet: string; tooltip: string }
type Snippets = { snippets: SnippetEntry[] }

[<Literal>]
let snippetFilesName = ".snippet-predictor.json"

[<Literal>]
let snippetSymbol = ":snp"

let snippets = Concurrent.ConcurrentQueue<SnippetEntry>()

let mutable watcher: FileSystemWatcher option = None

let parseSnippets (json: string) =
    try
        json
        |> JsonSerializer.Deserialize<Snippets>
        |> function
            | null -> Array.empty
            | _ as snippets -> snippets.snippets
    with _ ->
        Array.empty

let startRefreshTask (path: string) =
    let cancellationToken = new CancellationToken()

    task {
        let! json = path |> File.ReadAllTextAsync
        snippets.Clear()
        json |> parseSnippets |> Array.iter snippets.Enqueue
    }
    |> _.WaitAsync(cancellationToken)
    |> ignore

let handleRefresh (e: FileSystemEventArgs) = startRefreshTask e.FullPath

let handleClear (e: FileSystemEventArgs) = snippets.Clear()

let startFileWatchingEvent (directory: string) =
    let w = new FileSystemWatcher(directory, snippetFilesName)

    w.EnableRaisingEvents <- true
    w.IncludeSubdirectories <- false
    w.NotifyFilter <- NotifyFilters.LastWrite ||| NotifyFilters.FileName ||| NotifyFilters.Size

    handleRefresh |> w.Created.Add
    handleRefresh |> w.Changed.Add
    handleClear |> w.Deleted.Add
    handleClear |> w.Renamed.Add

    watcher <- w |> Some

let load () =
    let snippetDirectory =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)

    let snippetPath = Path.Combine(snippetDirectory, snippetFilesName)

    if File.Exists(snippetPath) then
        startRefreshTask snippetPath

    startFileWatchingEvent snippetDirectory

let getFilter (input: string) =
    // NOTE: Remove the snippet symbol from the input.
    // NOTE: Snippet symbol is used to exclude other predictors from suggestions.
    input.Replace(snippetSymbol, "")

let getSnippets (filter: string) : SnippetEntry seq =
    snippets |> Seq.filter _.snippet.Contains(filter)

let snippetToTuple (s: SnippetEntry) = s.snippet, s.tooltip

let getPredictiveSuggestions (input: string) : Generic.List<PredictiveSuggestion> =
    if String.IsNullOrWhiteSpace(input) then
        Seq.empty
    else
        input
        |> (getFilter >> getSnippets)
        |> Seq.map (snippetToTuple >> PredictiveSuggestion)
    |> Linq.Enumerable.ToList
