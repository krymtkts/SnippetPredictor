namespace SnippetPredictor

#if DEBUG
[<AutoOpen>]
module Debug =
    open System
    open System.IO
    open System.Runtime.CompilerServices
    open System.Runtime.InteropServices

    let lockObj = new obj ()

    [<Literal>]
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
type SnippetConfig = { Snippets: SnippetEntry[] | null }

module Snippet =
    open System
    open System.IO
    open System.Collections
    open System.Management.Automation
    open System.Management.Automation.Subsystem.Prediction
    open System.Text.Json
    open System.Threading
    open System.Text.Encodings.Web

    [<Literal>]
    let name = "Snippet"

    [<Literal>]
    let snippetFilesName = ".snippet-predictor.json"

    [<Literal>]
    let snippetSymbol = ":snp"

    [<Literal>]
    let environmentVariable = "SNIPPET_PREDICTOR_CONFIG"

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
        | Valid of SnippetConfig
        | Invalid of SnippetEntry

    let jsonOptions =
        JsonSerializerOptions(
            AllowTrailingCommas = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            WriteIndented = true
        )

    let parseSnippets (json: string) =
        try
            json.Trim()
            |> function
                | "" -> ConfigState.Empty
                | json ->
                    JsonSerializer.Deserialize<SnippetConfig>(json, jsonOptions)
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

    type Cache() =
        let snippets = Concurrent.ConcurrentQueue<SnippetEntry>()

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
            Logger.LogFile
                [ e.ChangeType.ToString(), sprintf "Snippets are refreshed due to file change: %s" e.FullPath ]
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

        let getSnippets (filter: string) : SnippetEntry seq =
            snippets |> Seq.filter _.Snippet.Contains(filter)

        let snippetToTuple (s: SnippetEntry) = s.Snippet, s.Tooltip

        let getFilter (input: string) =
            // NOTE: Remove the snippet symbol from the input.
            // NOTE: Snippet symbol is used to exclude other predictors from suggestions.
            input.Replace(snippetSymbol, "").Trim()

        member __.load getSnippetPath =
            let snippetDirectory, snippetPath = getSnippetPath ()

            if File.Exists(snippetPath) then
                startRefreshTask snippetPath

            startFileWatchingEvent snippetDirectory

        member __.getPredictiveSuggestions(input: string) : Generic.List<PredictiveSuggestion> =
            if String.IsNullOrWhiteSpace(input) then
                Seq.empty
            else
                input
                |> (getFilter >> getSnippets)
                |> Seq.map (snippetToTuple >> PredictiveSuggestion)
            |> Linq.Enumerable.ToList

    let getSnippetPathWith (getEnvironmentVariable: string -> string | null) (getUserProfilePath: unit -> string) =
        let snippetDirectory =
            match getEnvironmentVariable environmentVariable with
            | null
            | "" -> getUserProfilePath ()
            | path -> path

        snippetDirectory, Path.Combine(snippetDirectory, snippetFilesName)

    let getSnippetPath () =
        getSnippetPathWith Environment.GetEnvironmentVariable
        <| fun () -> Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)

    let toError (e: SnippetEntry) =
        if String.IsNullOrEmpty e.Tooltip then
            e.Snippet
        else
            $"{e.Snippet}: {e.Tooltip}"
        |> Error

    let loadConfig (getSnippetPath: unit -> string * string) =
        let snippetPath = getSnippetPath () |> snd

        if snippetPath |> (File.Exists >> not) then
            ConfigState.Empty
        else
            snippetPath |> parseSnippetFile |> _.Result

    let makeErrorRecord (e: string) =
        new ErrorRecord(new Exception(e), "", ErrorCategory.InvalidData, null)

    let makeSnippetEntry (snippet: string) (tooltip: string) =
        { Snippet = snippet; Tooltip = tooltip }

    let storeConfig getSnippetPath (config: SnippetConfig) =
        let json = JsonSerializer.Serialize(config, jsonOptions)
        let snippetPath = getSnippetPath () |> snd

        try
            File.WriteAllText(snippetPath, json)
            Ok()
        with e ->
            e.Message |> Error

    let loadSnippets getSnippetPath =
        loadConfig getSnippetPath
        |> function
            | ConfigState.Empty -> Array.empty |> Ok
            | ConfigState.Valid snippets ->
                snippets
                |> _.Snippets
                |> function
                    | null -> Array.empty |> Ok
                    | snps -> snps |> Ok
            | ConfigState.Invalid e -> e |> toError

    let addSnippets getSnippetPath (snippets: SnippetEntry seq) =
        loadConfig getSnippetPath
        |> function
            | ConfigState.Empty -> { Snippets = Array.ofSeq snippets } |> storeConfig getSnippetPath
            | ConfigState.Valid config ->
                let newSnippets =
                    config.Snippets
                    |> function
                        | null -> Array.ofSeq snippets
                        | snps -> Array.append snps <| Array.ofSeq snippets

                { config with Snippets = newSnippets } |> storeConfig getSnippetPath
            | ConfigState.Invalid e -> e |> toError

    let removeSnippets getSnippetPath (snippets: string seq) =
        loadConfig getSnippetPath
        |> function
            | ConfigState.Empty -> Ok()
            | ConfigState.Valid config ->
                let newSnippets =
                    config.Snippets
                    |> function
                        | null -> Array.empty
                        | snps ->
                            let removals = set snippets
                            snps |> Array.filter (_.Snippet >> removals.Contains >> not)

                { config with Snippets = newSnippets } |> storeConfig getSnippetPath
            | ConfigState.Invalid e -> e |> toError
