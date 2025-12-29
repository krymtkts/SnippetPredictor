namespace SnippetPredictor

open System
open System.IO
open System.Text.Json
open System.Text.Json.Serialization

#if DEBUG
[<AutoOpen>]
module Debug =
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

module Option =
    let dispose (d: 'a option when 'a :> IDisposable) = d |> Option.iter _.Dispose()

open System.Text.RegularExpressions

// NOTE: A static let generates unreachable code, so this module is used instead for coverage.
module Group =
    [<Literal>]
    let pattern = "^[A-Za-z0-9]+$"

    let regex = Regex(pattern)

type GroupJsonConverter() =
    inherit JsonConverter<string>()

    override _.Read(reader: byref<Utf8JsonReader>, _typeToConvert: Type, options: JsonSerializerOptions) =
        reader.GetString()
        |> function
            | null -> "" // NOTE: unreachable when JsonIgnoreCondition.WhenWritingNull is used.
            | value when Group.regex.IsMatch(value) -> value
            | value -> JsonException(sprintf "Invalid characters in group: %s" value) |> raise

    override _.Write(writer: Utf8JsonWriter, value: string, options: JsonSerializerOptions) =
        value |> writer.WriteStringValue

type SnippetEntry =
    { Snippet: string
      Tooltip: string
      [<JsonConverter(typeof<GroupJsonConverter>)>]
      [<JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)>]
      Group: string | null }

type SearchCaseSensitiveJsonConverter() =
    inherit JsonConverter<bool>()

    override _.Read(reader: byref<Utf8JsonReader>, _typeToConvert: Type, options: JsonSerializerOptions) =
        if reader.TokenType = JsonTokenType.Null then
            false
        else
            reader.GetBoolean()

    override _.Write(writer: Utf8JsonWriter, value: bool, options: JsonSerializerOptions) =
        value |> writer.WriteBooleanValue

type SnippetConfig =
    { [<JsonConverter(typeof<SearchCaseSensitiveJsonConverter>)>]
      SearchCaseSensitive: bool
      Snippets: SnippetEntry array | null }

module Snippet =
    open System.Collections
    open System.Management.Automation
    open System.Management.Automation.Subsystem.Prediction
    open System.Threading
    open System.Text.Encodings.Web

    [<Literal>]
    let name = "Snippet"

    [<Literal>]
    let snippetFilesName = ".snippet-predictor.json"

    [<Literal>]
    let environmentVariable = "SNIPPET_PREDICTOR_CONFIG"

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
          Tooltip = tooltip
          Group = null }

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
                | json when String.length json = 0 -> ConfigState.Empty
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

    module CaseSensitivity =
        [<Literal>]
        let sensitive = 1

        [<Literal>]
        let insensitive = 0

    module SearchCaseSensitivity =
        let ofBool =
            function
            | true -> CaseSensitivity.sensitive
            | false -> CaseSensitivity.insensitive

        let stringComparison =
            function
            | CaseSensitivity.sensitive -> StringComparison.Ordinal
            | _ -> StringComparison.OrdinalIgnoreCase

    module Disposal =
        [<Literal>]
        let disposed = 1

        [<Literal>]
        let notDisposed = 0

        type Flag() =

            let mutable status = notDisposed

            member __.IsDisposed = Volatile.Read(&status) = disposed

            member __.TryMarkDisposed() =
                Interlocked.Exchange(&status, disposed) = notDisposed

            member __.IfNotDisposed(f: unit -> unit) = if __.IsDisposed then () else f ()
            member __.IfDisposed(f: unit -> unit) = if __.IsDisposed then f () else ()

    type Cache() as __ =

        let mutable caseSensitive = CaseSensitivity.insensitive
        let snippets = Concurrent.ConcurrentQueue<SnippetEntry>()
        let groups = new Concurrent.ConcurrentDictionary<string, unit>()
        let semaphore = new SemaphoreSlim(1, 1)
        let refreshCts = new CancellationTokenSource()
        let mutable watcher: FileSystemWatcher | null = null
        let disposed = Disposal.Flag()

        let disposeWatcher (w: FileSystemWatcher | null) =
            match w with
            | null -> ()
            | w ->
                try
                    w.EnableRaisingEvents <- false
                    w.Dispose()
                with :? ObjectDisposedException ->
                    ()

        let exchangeAndDisposeWatcher (newWatcher: FileSystemWatcher | null) =
            let oldWatcher = Interlocked.Exchange(&watcher, newWatcher)
            disposeWatcher oldWatcher

        let tryRemoveCurrentWatcher (expected: FileSystemWatcher) =
            let removed = Interlocked.CompareExchange(&watcher, null, expected)

            if Object.ReferenceEquals(removed, expected) then
                disposeWatcher expected
                true
            else
                false

        let startRefreshTask (path: string) =
            let cancellationToken = refreshCts.Token

            task {
                let mutable acquired = false

                try
                    try
                        do! semaphore.WaitAsync(cancellationToken)
                        acquired <- true
#if DEBUG
                        Logger.LogFile [ "Refreshing snippets." ]
#endif

                        let! result = parseSnippetFile path
                        snippets.Clear()
                        groups.Clear()

                        result
                        |> function
                            | ConfigState.Empty -> ()
                            | ConfigState.Valid { SearchCaseSensitive = searchCaseSensitive
                                                  Snippets = snps } ->
                                Interlocked.Exchange(
                                    &caseSensitive,
                                    searchCaseSensitive |> SearchCaseSensitivity.ofBool
                                )
                                |> ignore

                                snps
                                |> function
                                    | null -> Array.empty
                                    | snippets -> snippets
                                |> Array.iter (fun s ->
                                    snippets.Enqueue s

                                    match s.Group with
                                    | null -> ()
                                    | g ->
                                        if g |> groups.ContainsKey |> not then
                                            groups.TryAdd(g, ()) |> ignore)
                            | ConfigState.Invalid record -> record |> snippets.Enqueue
#if DEBUG
                        Logger.LogFile [ "Refreshed snippets." ]
#endif
                    with
                    | :? OperationCanceledException
                    | :? ObjectDisposedException ->
#if DEBUG
                        Logger.LogFile [ $"Operation canceled or object disposed while refreshing snippets." ]
#else
                        ()
#endif
                    | e ->
#if DEBUG
                        Logger.LogFile [ $"Unexpected error occurred while refreshing snippets: {e.Message}" ]
#else
                        ()
#endif
                finally
                    if acquired then
                        try
                            semaphore.Release() |> ignore
                        with :? ObjectDisposedException ->
#if DEBUG
                            Logger.LogFile [ $"Semaphore was disposed while releasing." ]
#else
                            ()
#endif

            }
            |> ignore

        let refreshDebounceMs = 200
        let mutable pendingRefreshPath = ""

        let refreshTimer =
            new Timer(
                TimerCallback(fun _ ->
                    try
                        disposed.IfNotDisposed(fun () ->
                            let path = Volatile.Read(&pendingRefreshPath)

                            if not (String.IsNullOrWhiteSpace(path)) then
                                __.OnRefresh path
                                startRefreshTask path)
                    with
                    | :? ObjectDisposedException -> ()
                    | :? OperationCanceledException -> ()
                    | e ->
#if DEBUG
                        Logger.LogFile
                            [ $"Unexpected error occurred while debounced refreshing snippets: {e.Message}" ]
#else
                        ()
#endif
                ),
                null,
                Timeout.Infinite,
                Timeout.Infinite
            )

        let scheduleDebouncedRefresh (path: string) =
            Volatile.Write(&pendingRefreshPath, path)

            try
                refreshTimer.Change(refreshDebounceMs, Timeout.Infinite) |> ignore
            with :? ObjectDisposedException ->
                ()

        let handleRefresh (e: FileSystemEventArgs) =
            disposed.IfNotDisposed(fun () ->
#if DEBUG
                Logger.LogFile
                    [ e.ChangeType.ToString(), sprintf "Snippets are refreshed due to file change: %s" e.FullPath ]
#endif
                scheduleDebouncedRefresh e.FullPath)

        let rec startFileWatchingEvent (directory: string) =
            disposed.IfNotDisposed(fun () ->
                let w = __.CreateWatcher(directory, snippetFilesName)

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
                    // NOTE: Only the currently registered watcher instance may restart.
                    if Object.ReferenceEquals(Volatile.Read(&watcher), w) then
                        if tryRemoveCurrentWatcher w then
                            disposed.IfNotDisposed(fun () -> startFileWatchingEvent directory)

                if disposed.IsDisposed then
                    // NOTE: Dispose immediately if already disposed.
                    disposeWatcher w
                else
                    exchangeAndDisposeWatcher w
#if DEBUG
                Logger.LogFile [ "Started file watching event." ]
#endif
            )

        let snippetToTuple (s: SnippetEntry) =
            s.Group
            |> function
                | null -> s.Snippet, s.Tooltip
                | g -> s.Snippet, $"[{g}]{s.Tooltip}"

        let (|Empty|_|) (input: string) = String.IsNullOrWhiteSpace(input)

        let inputPattern = Regex(":([a-zA-Z0-9]+)\\s*(.*)")

        let (|Prefix|_|) (value: string) =
            // NOTE: Remove the snippet or tooltip symbol from the input.
            // NOTE: These symbols are used to exclude other predictors from suggestions.
            let m = inputPattern.Match(value)

            m.Groups.Count
            |> function
                | 2 -> (m.Groups[1].Value, "") |> Some
                | 3 -> (m.Groups[1].Value, m.Groups[2].Value.TrimEnd()) |> Some
                | _ -> None

        let (|NoPrefix|) (value: string) = value.Trim()

        let chooseSnippets pred =
            snippets
            |> Seq.choose (fun x ->
                if pred x then
                    Some(snippetToTuple x |> PredictiveSuggestion)
                else
                    None)

        [<Literal>]
        let Snp = "snp"

        [<Literal>]
        let Tip = "tip"

        abstract CreateWatcher: directory: string * filter: string -> FileSystemWatcher

        default _.CreateWatcher(directory: string, filter: string) =
            new FileSystemWatcher(directory, filter)

        abstract OnRefresh: path: string -> unit
        default _.OnRefresh(_path: string) = ()

        member __.load getSnippetPath =
            let snippetDirectory, snippetPath = getSnippetPath ()

            if File.Exists(snippetPath) then
                startRefreshTask snippetPath

            startFileWatchingEvent snippetDirectory

        member __.getPredictiveSuggestions(input: string) : Generic.List<PredictiveSuggestion> =
            match input with
            | Empty -> Seq.empty
            | Prefix(groupId, input) ->
#if DEBUG
                Logger.LogFile [ $"group:'{groupId}' input: '{input}'" ]
#endif
                let comparisonType = caseSensitive |> SearchCaseSensitivity.stringComparison

                let pred =
                    match groupId with
                    | Snp -> _.Snippet.Contains(input, comparisonType)
                    | Tip -> _.Tooltip.Contains(input, comparisonType)
                    | groupId -> fun (s: SnippetEntry) -> s.Group = groupId && s.Snippet.Contains(input, comparisonType)

                let groupIds =
                    if String.IsNullOrWhiteSpace(input) then
                        groups.Keys
                        |> Seq.append [ Snp; Tip ]
                        |> Seq.choose (fun g ->
                            if g <> groupId && g.StartsWith(groupId) then
                                ($":{g}", "") |> PredictiveSuggestion |> Some
                            else
                                None)
                    else
                        Seq.empty

                pred |> chooseSnippets |> Seq.append groupIds
            | NoPrefix snippet ->
                _.Snippet.Contains(snippet, caseSensitive |> SearchCaseSensitivity.stringComparison)
                |> chooseSnippets
            |> Linq.Enumerable.ToList

        interface IDisposable with
            member __.Dispose() =
                if disposed.TryMarkDisposed() then
                    refreshCts.Cancel()

                refreshTimer.Dispose()

                exchangeAndDisposeWatcher null
                refreshCts.Dispose()
                semaphore.Dispose()

    let getSnippetPathWith (getEnvironmentVariable: string -> string | null) (getUserProfilePath: unit -> string) =
        let snippetDirectory =
            // NOTE: Split branches to narrow the type (string | null)
            match getEnvironmentVariable environmentVariable with
            | null -> getUserProfilePath ()
            | path when String.length path = 0 -> getUserProfilePath ()
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

    let makeSnippetEntry (snippet: string) (tooltip: string) (group: string | null) =
        { Snippet = snippet
          Tooltip = tooltip
          Group = group }

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
            | ConfigState.Empty ->
                { SearchCaseSensitive = false
                  Snippets = Array.ofSeq snippets }
                |> storeConfig getSnippetPath
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
