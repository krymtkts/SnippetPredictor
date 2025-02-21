module Snippet

open System
open System.IO
open System.Collections
open System.Management.Automation
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

type SnippetEntry = { Snippet: string; Tooltip: string }
type Config = { Snippets: SnippetEntry[] | null }

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
    { Snippet = $"'{snippet}'"
      Tooltip = tooltip }

[<RequireQualifiedAccess>]
[<NoEquality>]
[<NoComparison>]
type ConfigState =
    | Empty
    | Valid of Config
    | Invalid of SnippetEntry

let parseSnippets (json: string) =
    try
        json.Trim()
        |> function
            | "" -> ConfigState.Empty
            | json ->
                JsonSerializer.Deserialize<Config>(json, JsonSerializerOptions(PropertyNameCaseInsensitive = true))
                |> function
                    | null ->
                        makeEntry $"{snippetFilesName} is null or invalid format." ""
                        |> ConfigState.Invalid
                    | snippets -> ConfigState.Valid snippets
    with e ->
        makeEntry $"An error occurred while parsing {snippetFilesName}" e.Message
        |> ConfigState.Invalid

let parseSnippetFile (path: string) =
    task {
        let! json = readSnippetFile path
        return parseSnippets json
    }

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
                let! result = parseSnippetFile path
                snippets.Clear()

                result
                |> function
                    | ConfigState.Empty -> ()
                    | ConfigState.Valid { Snippets = snps } ->
                        snps
                        |> function
                            | null -> Array.empty
                            | snippets -> snippets
                        |> Array.iter snippets.Enqueue
                    | ConfigState.Invalid record -> record |> snippets.Enqueue
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
    snippets |> Seq.filter _.Snippet.Contains(filter)

let snippetToTuple (s: SnippetEntry) = s.Snippet, s.Tooltip

let getPredictiveSuggestions (input: string) : Generic.List<PredictiveSuggestion> =
    if String.IsNullOrWhiteSpace(input) then
        Seq.empty
    else
        input
        |> (getFilter >> getSnippets)
        |> Seq.map (snippetToTuple >> PredictiveSuggestion)
    |> Linq.Enumerable.ToList

let loadConfig () =
    let snippetPath = getSnippetPath () |> snd

    if snippetPath |> (File.Exists >> not) then
        Error $"The {snippetFilesName} file does not exist."
    else
        snippetPath
        |> parseSnippetFile
        |> _.Result
        |> function
            | ConfigState.Empty -> Ok { Snippets = null }
            | ConfigState.Valid snippets -> Ok snippets
            | ConfigState.Invalid e -> $"{e.Snippet}: {e.Tooltip}" |> Error

let makeErrorRecord (e: string) =
    new ErrorRecord(new Exception(e), "", ErrorCategory.InvalidData, null)

let makeSnippetEntry (snippet: string) (tooltip: string) =
    { Snippet = snippet; Tooltip = tooltip }

let storeConfig (config: Config) =
    let json =
        JsonSerializer.Serialize(config, JsonSerializerOptions(WriteIndented = true))

    let snippetPath = getSnippetPath () |> snd

    try
        File.WriteAllText(snippetPath, json)
        Ok()
    with e ->
        e.Message |> Error

let addSnippets (snippets: SnippetEntry seq) =
    loadConfig ()
    |> function
        | Ok config ->
            let newSnippets =
                config.Snippets
                |> function
                    | null -> Array.ofSeq snippets
                    | snps -> Array.append snps <| Array.ofSeq snippets

            let config = { config with Snippets = newSnippets }
            storeConfig config
        | Error e -> e |> Error

let removeSnippets (snippets: string seq) =
    loadConfig ()
    |> function
        | Ok config ->
            let newSnippets =
                config.Snippets
                |> function
                    | null -> Array.empty
                    | snps ->
                        let removals = set snippets
                        snps |> Array.filter (_.Snippet >> removals.Contains >> not)

            let config = { config with Snippets = newSnippets }
            storeConfig config
        | Error e -> e |> Error
