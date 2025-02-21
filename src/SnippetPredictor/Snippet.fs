module Snippet

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

let readSnippetFile (path: string) =
    task {
        // NOTE: Open the file with shared read/write access to prevent the file lock error by other processes.
        use fs =
            new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, useAsync = true)

        use sr = new StreamReader(fs)
        return! sr.ReadToEndAsync()
    }

let makeEntry (snippet: string) (tooltip: string) =
    { snippet = $"'{snippet}'"
      tooltip = tooltip }

let parseSnippets (json: string) =
    try
        json
        |> JsonSerializer.Deserialize<Snippets>
        |> function
            | null -> makeEntry $"{snippetFilesName} is null or invalid format." "" |> Error
            | snippets -> Ok snippets
    with e ->
        makeEntry $"An error occurred while parsing {snippetFilesName}" e.Message
        |> Error

let semaphore = new SemaphoreSlim(1, 1)

let startRefreshTask (path: string) =
    let cancellationToken = new CancellationToken()

    task {
        do! semaphore.WaitAsync(cancellationToken)
#if DEBUG
        Logger.LogFile [ "Refreshing snippets." ]
#endif

        try
            try
                let! json = readSnippetFile path
                snippets.Clear()

                json
                |> parseSnippets
                |> function
                    | Ok { snippets = snps } -> snps |> Array.iter snippets.Enqueue
                    | Error record -> record |> snippets.Enqueue
#if DEBUG
                Logger.LogFile [ "Refreshed snippets." ]
#endif
            with e ->
#if DEBUG
                Logger.LogFile [ $"An error occurred while refreshing snippets: {e.Message}" ]
#else
                ()
#endif
        finally
            semaphore.Release() |> ignore
    }
    |> _.WaitAsync(cancellationToken)
    |> ignore

let handleRefresh (e: FileSystemEventArgs) =
#if DEBUG
    Logger.LogFile [ e.ChangeType.ToString(), sprintf "Snippets are refreshed due to file change: %s" e.FullPath ]
#endif
    startRefreshTask e.FullPath

let rec startFileWatchingEvent (directory: string) =
    let w = new FileSystemWatcher(directory, snippetFilesName)

    w.EnableRaisingEvents <- true
    w.IncludeSubdirectories <- false
    w.NotifyFilter <- NotifyFilters.LastWrite

    handleRefresh |> w.Created.Add
    handleRefresh |> w.Changed.Add

    w.Error.Add
    <| fun e ->
#if DEBUG
        Logger.LogFile [ $"Error occurred in file watching event: {e.GetException().Message}" ]
#endif
        watcher |> Option.iter _.Dispose()
        startFileWatchingEvent directory

    watcher <- w |> Some
#if DEBUG
    Logger.LogFile [ "Started file watching event." ]
#endif

let getSnippetPath () =
    let snippetDirectory =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)

    snippetDirectory, Path.Combine(snippetDirectory, snippetFilesName)

let load () =
    let snippetDirectory, snippetPath = getSnippetPath ()

    if File.Exists(snippetPath) then
        startRefreshTask snippetPath

    startFileWatchingEvent snippetDirectory

let getFilter (input: string) =
    // NOTE: Remove the snippet symbol from the input.
    // NOTE: Snippet symbol is used to exclude other predictors from suggestions.
    input.Replace(snippetSymbol, "").Trim()

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
