﻿module Snippet

open System
open System.IO
open System.Collections
open System.Management.Automation.Subsystem.Prediction
open System.Text.Json
open System.Threading

#if DEBUG
[<AutoOpen>]
module Debug =
    open System.Runtime.CompilerServices
    open System.Runtime.InteropServices

    let lockObj = new obj ()

    let logPath = "./debug.log"

    [<AbstractClass; Sealed>]
    type Logger =
        static member LogFile
            (
                res,
                [<Optional; DefaultParameterValue(""); CallerMemberName>] caller: string,
                [<CallerFilePath; Optional; DefaultParameterValue("")>] path: string,
                [<CallerLineNumber; Optional; DefaultParameterValue(0)>] line: int
            ) =

            // NOTE: lock to avoid another process error when dotnet test.
            lock lockObj (fun () ->
                use sw = new StreamWriter(logPath, true)

                res
                |> List.iter (
                    fprintfn
                        sw
                        "[%s] %s at %d %s <%A>"
                        (DateTimeOffset.Now.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz"))
                        path
                        line
                        caller
                ))
#endif

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
#if DEBUG
        Logger.LogFile [ "Refreshed snippets." ]
#endif
    }
    |> _.WaitAsync(cancellationToken)
    |> ignore

let handleRefresh (e: FileSystemEventArgs) =
#if DEBUG
    Logger.LogFile [ e.ChangeType.ToString(), sprintf "Snippets are refreshed due to file change: %s" e.FullPath ]
#endif
    startRefreshTask e.FullPath

let startFileWatchingEvent (directory: string) =
    let w = new FileSystemWatcher(directory, snippetFilesName)

    w.EnableRaisingEvents <- true
    w.IncludeSubdirectories <- false
    w.NotifyFilter <- NotifyFilters.LastWrite ||| NotifyFilters.FileName ||| NotifyFilters.Size

    handleRefresh |> w.Created.Add
    handleRefresh |> w.Changed.Add

    watcher <- w |> Some
#if DEBUG
    Logger.LogFile [ "Started file watching event." ]
#endif

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
